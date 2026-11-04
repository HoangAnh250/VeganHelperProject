using System.Net;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Npgsql;
using VeganHelper.DAL.Persistence;

const string projectRef = "brjltkherhhqqkryhsuq";
var apply = args.SequenceEqual(["--apply"]);
if (args.Length > 0 && !apply)
    throw new ArgumentException("Use no arguments to preview or --apply to upload and assign images.");

var root = Directory.GetCurrentDirectory();
var manifestPath = Path.Combine(root, "docs", "data", "meal-images", "curated.json");
var images = JsonSerializer.Deserialize<List<MealImage>>(File.ReadAllText(manifestPath),
    new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
var seedNames = File.ReadAllLines(Path.Combine(root, "docs", "data", "meal-plan-seed", "meals.csv"))
    .Skip(1).Select(line => line.Split(',')[1].Trim('"')).ToHashSet(StringComparer.Ordinal);
if (images.Count != seedNames.Count || images.Select(image => image.MealName).Distinct(StringComparer.Ordinal).Count() != images.Count ||
    !seedNames.SetEquals(images.Select(image => image.MealName)))
    throw new InvalidOperationException("The image manifest is empty or contains duplicate meal names.");
foreach (var image in images)
{
    if (!seedNames.Contains(image.MealName) || image.PhotoId <= 0 || image.License != "Pexels License" ||
        !Uri.TryCreate(image.DownloadUrl, UriKind.Absolute, out var source) ||
        source is null || source.Scheme != Uri.UriSchemeHttps || source.Host != "images.pexels.com" ||
        !Uri.TryCreate(image.SourcePage, UriKind.Absolute, out var page) ||
        page is null || page.Scheme != Uri.UriSchemeHttps || page.Host != "www.pexels.com" ||
        !image.DownloadUrl.Contains($"/photos/{image.PhotoId}/", StringComparison.Ordinal) ||
        !image.SourcePage.EndsWith($"-{image.PhotoId}/", StringComparison.Ordinal) ||
        string.IsNullOrWhiteSpace(image.Photographer))
        throw new InvalidOperationException($"Invalid curated source for {image.MealName}.");
}

var connectionString = DatabaseConnection.ReadPostgresConnection();
var connectionInfo = new NpgsqlConnectionStringBuilder(connectionString);
if (connectionInfo.Host?.Contains(projectRef, StringComparison.OrdinalIgnoreCase) != true &&
    connectionInfo.Username?.Contains(projectRef, StringComparison.OrdinalIgnoreCase) != true)
    throw new InvalidOperationException("The database connection does not identify the expected Supabase project.");

await using var db = new NpgsqlConnection(connectionString);
await db.OpenAsync();
await using (var column = new NpgsqlCommand("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='meals' AND column_name='image_url'", db))
    if ((long)(await column.ExecuteScalarAsync() ?? 0L) != 1L)
        throw new InvalidOperationException("Apply AddMealImages before importing image URLs.");

await using (var coverage = new NpgsqlCommand("SELECT count(*), count(*) FILTER (WHERE image_url IS NOT NULL) FROM public.meals", db))
await using (var counts = await coverage.ExecuteReaderAsync())
{
    if (!await counts.ReadAsync()) throw new InvalidOperationException("Could not inspect meal image coverage.");
    Console.WriteLine($"Database meals: {counts.GetInt64(0)}; already with image: {counts.GetInt64(1)}.");
}

var pending = new List<(MealImage Image, long Id, string? ExistingUrl)>();
foreach (var image in images)
{
    await using var lookup = new NpgsqlCommand("SELECT id, image_url FROM public.meals WHERE combo_name=@name", db);
    lookup.Parameters.AddWithValue("name", image.MealName);
    await using var rows = await lookup.ExecuteReaderAsync();
    if (!await rows.ReadAsync()) throw new InvalidOperationException($"Meal not found: {image.MealName}");
    var id = rows.GetInt64(0);
    var existing = rows.IsDBNull(1) ? null : rows.GetString(1);
    if (await rows.ReadAsync()) throw new InvalidOperationException($"Ambiguous meal name: {image.MealName}");
    if (existing is null ||
        (image.ReplacesPhotoId is > 0 && existing.Contains($"/pexels-{image.ReplacesPhotoId}-", StringComparison.Ordinal)))
        pending.Add((image, id, existing));
    else Console.WriteLine($"Already has an image: {image.MealName}");
}

Console.WriteLine($"Curated meals: {images.Count}; source photos: {images.Select(image => image.PhotoId).Distinct().Count()}; pending meals: {pending.Count}; expected project: {projectRef}.");
if (!apply) return;

var apiConfig = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "VeganHelper.API", "appsettings.json")));
var r2Config = apiConfig.RootElement.GetProperty("CloudflareR2");
var secretsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Microsoft", "UserSecrets", "VeganHelper-API-Development", "secrets.json");
var secrets = JsonDocument.Parse(File.ReadAllText(secretsPath));
string setting(string name) => r2Config.GetProperty(name).GetString() ?? throw new InvalidOperationException($"CloudflareR2:{name} is missing.");
string secret(string name) => secrets.RootElement.GetProperty($"CloudflareR2:{name}").GetString()
    ?? throw new InvalidOperationException($"CloudflareR2:{name} is missing from user secrets.");
var bucket = setting("BucketName");
var publicBase = setting("PublicUrl").TrimEnd('/');
using var storage = new AmazonS3Client(new BasicAWSCredentials(secret("AccessKey"), secret("SecretKey")),
    new AmazonS3Config { ServiceURL = setting("ServiceUrl") });
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

foreach (var photoGroup in pending.GroupBy(entry => entry.Image.PhotoId))
{
    var entries = photoGroup.ToList();
    var image = entries[0].Image;
    foreach (var (_, _, existingUrl) in entries)
        if (existingUrl is not null && !existingUrl.StartsWith($"{publicBase}/meals/", StringComparison.Ordinal))
            throw new InvalidOperationException($"Existing image is outside the managed R2 path: {image.MealName}");

    var content = await http.GetByteArrayAsync(image.DownloadUrl);
    if (content.Length is < 1_000 or > 3_000_000 || content[0] != 0xff || content[1] != 0xd8)
        throw new InvalidOperationException($"Image is not a supported JPEG: {image.MealName}");

    var key = $"meals/pexels-{image.PhotoId}-{Guid.NewGuid():N}.jpg";
    var publicUrl = $"{publicBase}/{key}";
    var uploaded = false;
    await using var transaction = await db.BeginTransactionAsync();
    try
    {
        using var stream = new MemoryStream(content, writable: false);
        await storage.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket, Key = key, InputStream = stream,
            ContentType = "image/jpeg", DisablePayloadSigning = true
        });
        uploaded = true;
        HttpStatusCode? status = null;
        var verified = false;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await http.GetAsync(publicUrl, HttpCompletionOption.ResponseHeadersRead);
            status = response.StatusCode;
            if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "image/jpeg")
            {
                verified = true;
                break;
            }
            if (attempt < 2) await Task.Delay(1_000);
        }
        if (!verified)
            throw new InvalidOperationException($"R2 public URL is not readable for {image.MealName} (HTTP {(int?)status}).");

        foreach (var (entryImage, id, existingUrl) in entries)
        {
            await using var update = new NpgsqlCommand(
                "UPDATE public.meals SET image_url=@url WHERE id=@id AND image_url IS NOT DISTINCT FROM @existing RETURNING id", db, transaction);
            update.Parameters.AddWithValue("url", publicUrl);
            update.Parameters.AddWithValue("id", id);
            update.Parameters.AddWithValue("existing", NpgsqlTypes.NpgsqlDbType.Text,
                existingUrl is null ? DBNull.Value : existingUrl);
            if (await update.ExecuteScalarAsync() is null)
                throw new InvalidOperationException($"Meal image changed concurrently: {entryImage.MealName}");
        }
        await transaction.CommitAsync();
        uploaded = false;
        foreach (var (entryImage, _, existingUrl) in entries)
        {
            Console.WriteLine($"Assigned {entryImage.MealName}: {publicUrl}");
            if (existingUrl is null) continue;
            try
            {
                await storage.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucket, Key = existingUrl[(publicBase.Length + 1)..]
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Old image needs manual cleanup for {entryImage.MealName}: {ex.Message}");
            }
        }
    }
    catch
    {
        await transaction.RollbackAsync();
        if (uploaded) await storage.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key });
        throw;
    }
}

public sealed record MealImage(string MealName, long PhotoId, string SourcePage,
    string Photographer, string License, string DownloadUrl, long? ReplacesPhotoId = null);
