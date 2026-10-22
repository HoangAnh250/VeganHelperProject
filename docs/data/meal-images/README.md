# Curated meal images

Only visually checked images representing the named dish go in `curated.json`. A missing match remains without an image in the app. The source page, photographer, and license are recorded for each image. The current entries are from [Pexels](https://www.pexels.com/license/), which permits use on a website; these images are not nutrition or ingredient evidence.

The `AddMealImages` migration adds nullable `meals.image_url`. The importer uploads each curated image to the project's existing Cloudflare R2 bucket under `meals/`, confirms its public URL, then updates the matching Supabase seed meal. It requires a unique seed name and deletes a newly uploaded object if the database update fails. The import is repeatable; already assigned meals are skipped. An explicitly curated `replacesPhotoId` permits replacing only that prior imported photo, then removes the old R2 object. Credentials come from the existing local API configuration and user secrets, not this manifest.

Only two of the 200 sample dishes have been curated so far. Do not attach a merely similar food photograph to a different recipe. Add more entries only after checking the source license and image visually. The frontend already renders `imageUrl`; saved plan snapshots are enriched from the current meal catalog by the backend.
