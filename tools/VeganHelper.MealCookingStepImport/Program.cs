using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;
using VeganHelper.DAL.Persistence;

const string expectedProjectRef = "brjltkherhhqqkryhsuq";
var apply = args.SequenceEqual(["--apply"], StringComparer.Ordinal);
if (args.Length > 0 && !apply)
    throw new ArgumentException("Run without arguments to preview, or pass --apply to add cooking steps.");

var root = Directory.GetCurrentDirectory();
var payloadPath = Path.Combine(root, "docs", "data", "meal-plan-seed", "cooking-steps.json");
var seedPath = Path.Combine(root, "docs", "data", "meal-plan-seed", "seed-data.json");
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
var payload = JsonSerializer.Deserialize<CookingStepsPayload>(await File.ReadAllTextAsync(payloadPath), options)
    ?? throw new InvalidOperationException("Cooking step payload is empty.");
var seed = JsonSerializer.Deserialize<MealSeed>(await File.ReadAllTextAsync(seedPath), options)
    ?? throw new InvalidOperationException("Meal seed is empty.");

var expectedNames = seed.Meals.Select(meal => meal.Name).ToHashSet(StringComparer.Ordinal);
var recipes = payload.Recipes;
if (!string.Equals(payload.Language, "en", StringComparison.OrdinalIgnoreCase) ||
    recipes.Count != 200 || recipes.Select(recipe => recipe.MealName).Distinct(StringComparer.Ordinal).Count() != 200 ||
    !expectedNames.SetEquals(recipes.Select(recipe => recipe.MealName)))
    throw new InvalidOperationException("Expected English instructions for the exact 200 unique seeded meals.");

foreach (var recipe in recipes)
{
    if (recipe.Steps.Count is < 4 or > 7 || recipe.Steps.Any(string.IsNullOrWhiteSpace) ||
        recipe.Steps.Any(step => step.Length > 4000))
        throw new InvalidOperationException($"Invalid cooking steps for '{recipe.MealName}'.");
}

var connectionString = DatabaseConnection.ReadPostgresConnection();
var connectionInfo = new NpgsqlConnectionStringBuilder(connectionString);
if (connectionInfo.Host?.Contains(expectedProjectRef, StringComparison.OrdinalIgnoreCase) != true &&
    connectionInfo.Username?.Contains(expectedProjectRef, StringComparison.OrdinalIgnoreCase) != true)
    throw new InvalidOperationException("The configured PostgreSQL connection does not identify the expected Supabase project.");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await RequireMigrationAsync(connection);

var names = recipes.Select(recipe => recipe.MealName).ToArray();
var meals = await ReadMealsAsync(connection, names, null);
if (meals.Count != 200 || meals.Values.Distinct().Count() != 200)
    throw new InvalidOperationException("Supabase does not contain exactly one matching row for every seeded meal name.");

var current = await ReadStepsAsync(connection, meals.Values, null);
var newRecipes = new List<CookingRecipe>();
var alreadyPresent = 0;
foreach (var recipe in recipes)
{
    var mealId = meals[recipe.MealName];
    if (!current.TryGetValue(mealId, out var existing) || existing.Count == 0)
    {
        newRecipes.Add(recipe);
        continue;
    }

    if (!existing.SequenceEqual(recipe.Steps, StringComparer.Ordinal))
        throw new InvalidOperationException($"Cooking instructions already exist and differ for '{recipe.MealName}'. Nothing was changed.");
    alreadyPresent++;
}

var pendingSteps = newRecipes.Sum(recipe => recipe.Steps.Count);
Console.WriteLine($"Supabase project: {expectedProjectRef}; seeded meals: {recipes.Count}; meals already matching: {alreadyPresent}; meals to add: {newRecipes.Count}; steps to add: {pendingSteps}.");
if (!apply)
{
    Console.WriteLine("Preview only. Pass --apply to save the missing instructions.");
    return;
}

await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
var lockedMeals = await ReadMealsAsync(connection, names, transaction, forUpdate: true);
if (lockedMeals.Count != 200 || !lockedMeals.OrderBy(pair => pair.Key).SequenceEqual(meals.OrderBy(pair => pair.Key)))
    throw new InvalidOperationException("The seeded meal set changed during import; the transaction was canceled.");

var transactionSteps = await ReadStepsAsync(connection, meals.Values, transaction);
foreach (var recipe in recipes)
{
    var mealId = meals[recipe.MealName];
    if (!transactionSteps.TryGetValue(mealId, out var existing) || existing.Count == 0)
        continue;
    if (!existing.SequenceEqual(recipe.Steps, StringComparer.Ordinal))
        throw new InvalidOperationException($"Cooking instructions changed during import for '{recipe.MealName}'. The transaction was canceled.");
}

await using (var insert = new NpgsqlCommand(
    "INSERT INTO public.meal_cooking_steps (meal_id, step_number, instruction) VALUES (@meal_id, @step_number, @instruction)",
    connection, transaction))
{
    var mealParameter = insert.Parameters.Add("meal_id", NpgsqlDbType.Bigint);
    var stepParameter = insert.Parameters.Add("step_number", NpgsqlDbType.Smallint);
    var instructionParameter = insert.Parameters.Add("instruction", NpgsqlDbType.Text);
    foreach (var recipe in recipes)
    {
        var mealId = meals[recipe.MealName];
        if (transactionSteps.TryGetValue(mealId, out var existing) && existing.Count > 0)
            continue;
        for (var index = 0; index < recipe.Steps.Count; index++)
        {
            mealParameter.Value = mealId;
            stepParameter.Value = checked((short)(index + 1));
            instructionParameter.Value = recipe.Steps[index];
            await insert.ExecuteNonQueryAsync();
        }
    }
}

await transaction.CommitAsync();
var verifiedSteps = await ReadStepsAsync(connection, meals.Values, null);
var verifiedMealCount = verifiedSteps.Count(pair => pair.Value.Count >= 4);
var verifiedStepCount = verifiedSteps.Values.Sum(steps => steps.Count);
if (verifiedMealCount != 200 || verifiedStepCount != recipes.Sum(recipe => recipe.Steps.Count))
    throw new InvalidOperationException("The post-import coverage check did not match the expected 200 meals and step count.");
Console.WriteLine($"Imported and verified {verifiedStepCount} ordered steps for {verifiedMealCount} meals.");

static async Task RequireMigrationAsync(NpgsqlConnection connection)
{
    await using var command = new NpgsqlCommand("""
        SELECT c.relrowsecurity,
               NOT has_table_privilege('anon', c.oid, 'SELECT'),
               NOT has_table_privilege('authenticated', c.oid, 'SELECT')
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relname = 'meal_cooking_steps' AND c.relkind = 'r'
        """, connection);
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        throw new InvalidOperationException("The AddMealCookingSteps migration must be applied before importing instructions.");
    if (!reader.GetBoolean(0) || !reader.GetBoolean(1) || !reader.GetBoolean(2))
        throw new InvalidOperationException("The cooking-step table must have RLS enabled and deny direct SELECT to anon/authenticated roles.");
}

static async Task<Dictionary<string, long>> ReadMealsAsync(
    NpgsqlConnection connection, string[] names, NpgsqlTransaction? transaction, bool forUpdate = false)
{
    var sql = "SELECT id, combo_name FROM public.meals WHERE combo_name = ANY (@names) ORDER BY combo_name";
    if (forUpdate) sql += " FOR UPDATE";
    await using var command = new NpgsqlCommand(sql, connection, transaction);
    command.Parameters.Add("names", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = names;
    await using var reader = await command.ExecuteReaderAsync();
    var meals = new Dictionary<string, long>(StringComparer.Ordinal);
    while (await reader.ReadAsync())
    {
        var name = reader.GetString(1);
        if (!meals.TryAdd(name, reader.GetInt64(0)))
            throw new InvalidOperationException($"Multiple database meals have the same name: '{name}'.");
    }
    return meals;
}

static async Task<Dictionary<long, List<string>>> ReadStepsAsync(
    NpgsqlConnection connection, IEnumerable<long> mealIds, NpgsqlTransaction? transaction)
{
    await using var command = new NpgsqlCommand("""
        SELECT meal_id, step_number, instruction
        FROM public.meal_cooking_steps
        WHERE meal_id = ANY (@meal_ids)
        ORDER BY meal_id, step_number
        """, connection, transaction);
    command.Parameters.Add("meal_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = mealIds.ToArray();
    await using var reader = await command.ExecuteReaderAsync();
    var result = new Dictionary<long, List<string>>();
    while (await reader.ReadAsync())
    {
        var mealId = reader.GetInt64(0);
        var stepNumber = reader.GetInt16(1);
        var steps = result.GetValueOrDefault(mealId);
        if (steps is null)
        {
            steps = [];
            result.Add(mealId, steps);
        }
        if (stepNumber != steps.Count + 1)
            throw new InvalidOperationException($"Cooking step order is not contiguous for meal id {mealId}.");
        steps.Add(reader.GetString(2));
    }
    return result;
}

sealed class CookingStepsPayload
{
    public string Language { get; set; } = "";
    public List<CookingRecipe> Recipes { get; set; } = [];
}

sealed class CookingRecipe
{
    [JsonPropertyName("meal_name")]
    public string MealName { get; set; } = "";
    public List<string> Steps { get; set; } = [];
}

sealed class MealSeed
{
    public List<SeedMeal> Meals { get; set; } = [];
}

sealed class SeedMeal
{
    public string Name { get; set; } = "";
}
