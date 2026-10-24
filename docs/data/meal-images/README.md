# Meal image catalog

The 200 seeded meals use 26 free Pexels photos, selected as representative illustrations by dish family. The image does not certify the exact recipe, ingredients, nutrition, or allergen content. Each manifest entry records its Pexels source page, photographer, and the [Pexels License](https://www.pexels.com/license/). The photo list and assignments can be regenerated from the meal seed with `tools/VeganHelper.MealImageImport/BuildImageManifest.ps1`.

The `AddMealImages` migration adds nullable `meals.image_url`. The importer uploads each distinct photo once to the existing Cloudflare R2 bucket under `meals/`, verifies the public URL, and assigns it to every mapped Supabase seed meal in one transaction per photo. It validates that the manifest covers every seeded meal, skips meals that already have an image, and removes a newly uploaded object if the database assignment fails. Credentials come from the existing local API configuration and user secrets, not this manifest.

The frontend already renders `imageUrl`; saved meal-plan snapshots receive the current catalog image when returned by the backend. These images are illustrative only and should not be used to validate recipe ingredients or support nutrition claims.
