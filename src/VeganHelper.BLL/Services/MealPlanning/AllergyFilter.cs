using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services.MealPlanning;

public static class AllergyFilter
{
    // Explicit conservative families, not a clinical allergen certification.
    private static readonly string[][] Families =
    [
        ["peanut", "peanuts", "groundnut", "groundnuts", "dau phong", "lac"],
        ["soy", "soya", "soybean", "soybeans", "tofu", "tempeh", "edamame", "miso", "dau nanh"],
        ["milk", "dairy", "cheese", "yogurt", "yoghurt", "butter", "cream", "whey", "casein", "sua"],
        ["egg", "eggs", "albumen", "trung"],
        ["gluten", "wheat", "barley", "rye", "bread", "pasta", "couscous", "seitan", "soy sauce", "bot mi"],
        ["tree nut", "tree nuts", "nuts", "nut", "almond", "almonds", "walnut", "walnuts", "cashew", "cashews", "hazelnut", "hazelnuts", "pecan", "pecans", "pistachio", "pistachios", "peanut", "peanuts", "cac loai hat"],
        ["sesame", "tahini", "vung", "me"],
        ["fish", "seafood", "shellfish", "shrimp", "prawn", "crab", "lobster", "clam", "mussel", "oyster", "hai san"],
        ["mustard"], ["celery"], ["lupin", "lupine"], ["sulfite", "sulphite", "sulfites", "sulphites"]
    ];

    private static string Normalize(string value)
    {
        var decomposed=value.ToLowerInvariant().Replace('đ','d').Normalize(NormalizationForm.FormD);
        var clean=new string(decomposed.Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(clean,"[^a-z0-9]+"," ").Trim();
    }

    private static bool Contains(string name, string token) => (" "+name+" ").Contains(" "+token+" ",StringComparison.Ordinal);

    public static HashSet<long> ExcludedIngredients(IReadOnlyList<AllergyProjection> allergies,
        IReadOnlyList<Ingredient> ingredients, IReadOnlySet<long> recipeIngredientIds)
    {
        var excluded=new HashSet<long>();
        foreach (var allergy in allergies)
        {
            excluded.Add(allergy.IngredientId);
            var name=Normalize(allergy.Name);
            var families=Families.Where(f=>f.Any(token=>Contains(name,token))).ToArray();
            var matches=ingredients.Where(i=>
            {
                var candidate=Normalize(i.Name);
                if (candidate==name || Contains(candidate,name)) return name.Length>0;
                return families.Any(f=>f.Any(token=>Contains(candidate,token)));
            }).ToList();
            // Global ingredients include arbitrary user-created aliases. Submission method and
            // another unused alias row cannot establish that recipes are being filtered.
            if (families.Length==0 && !matches.Any(i=>recipeIngredientIds.Contains(i.Id)))
                throw new ConflictException($"Cannot reliably match allergy '{allergy.Name}'. Select a recipe ingredient or a supported allergy family before generating.");
            excluded.UnionWith(matches.Select(i=>i.Id));
        }
        return excluded;
    }
}
