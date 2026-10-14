# Verification — auth and recipe form regressions

## Root causes

- Axios globally redirected every 401, including incorrect-login responses, to `/login`. Browser reproduction showed the login page rebuilding without displaying the field error.
- Post calls read only localStorage and could reuse a legacy token; sessionStorage logins sent an empty bearer token. My Posts then substituted the public feed on 401. Create/edit/delete also treated 401 as success in the UI.
- React sent `IngredientsJson` entries containing `Name`, whereas BLL inserted their default `IngredientId=0`. A regression test reproduced the wrong ID. Replacing tracked steps/ingredient joins on edit could also conflict with their keys.

## Verified

- `node node_modules/react-scripts/bin/react-scripts.js test --watchAll=false --runInBand`: 7 passed. Covers both remember-me modes, clearing previous-account tokens, no feed fallback on 401, preserving failed-login errors, and create/edit multipart ingredients/steps.
- `dotnet test tests/VeganHelper.UnitTests --no-restore -v minimal`: 24 passed. Includes named ingredients, nonexistent IDs, malformed arrays, empty names, invalid quantities and duplicate/invalid step numbers.
- `dotnet test tests/VeganHelper.IntegrationTests -v minimal` with `VEGANHELPER_TEST_POSTGRES` configured privately: 10 passed. The new recipe test uses real PostgreSQL repository writes and validators; only media upload and outer transaction ownership are substituted. Saves Unicode names, resolves existing names ignoring case, updates quantity/step descriptions without duplicate keys, refuses another author's update and excludes another author's post from My Posts. All fixture writes rollback.
- FE production build and BE build succeeded. An initial BE build failed because the existing running API held DLLs; stopping that exact checkout process resolved the environmental failure.
- Current BE HTTP: databaseAvailable=true; incorrect login returns 401 with the original error contract; My Posts without JWT returns 401.
- Browser: invalid credentials leave inline errors visible with inputs intact and no full-page redirect. Proof: `.local-data/bug-evidence/login-error.png` (ignored by Git).
- Scoped review: auth storage is shared by Axios and all protected post requests; saving a new login clears previous persistent/session/legacy credentials; public feed is never used as private data. Ingredient resolution runs within the post transaction. Invalid recipe payloads throw ArgumentException, which existing API middleware maps to HTTP 400. Steps/category/ingredient joins are reconciled by their keys during edit. No database migration is required for this fix.

## Limits and existing data

Successful sign-in to the user's accounts and an actual R2 upload were not performed; storage/request behavior is covered by frontend tests, ownership and persistence by PostgreSQL tests. The browser wrong-credentials path was exercised against the live .NET API.

Comparing today's database with the previous migration snapshot reported an existing post title change and additional application rows. That snapshot is no longer an exact baseline after normal user activity; those values were left intact. Test ingredient rows were absent after rollback. No attempt was made to restore or overwrite user changes.

Both repositories retain local, uncommitted changes. No commit, push or publication was performed.
