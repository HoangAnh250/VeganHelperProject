# VeganHelperProject

ASP.NET Core .NET 10 / EF Core SQL Server / three-project 3-layer starter.

Open `VeganHelper.sln` in Visual Studio, set `VeganHelper.API` as startup project and run the https profile. Swagger: https://localhost:7180/swagger.

## Project structure

The solution contains three projects:

- `VeganHelper.API`: ASP.NET Core Web API, controllers, middleware and dependency injection.
- `VeganHelper.BLL`: class library containing DTOs and business services.
- `VeganHelper.DAL`: class library containing entities, repositories, `AppDbContext` and migrations.

Project references follow `API -> BLL -> DAL`; API also references DAL to compose dependency injection. Controllers call services, and services call repositories.

`GET /health/live` tests API availability without SQL. Development-only `GET /api/status` exercises Controller -> Service -> Repository and returns 503 if SQL is unavailable.

## Database

The 27-table schema matches the approved revised design, with member/admin role seed data. No database is created or migrated on application startup.

```powershell
dotnet restore
dotnet build
dotnet ef database update --project src/VeganHelper.DAL/VeganHelper.DAL.csproj --startup-project src/VeganHelper.API/VeganHelper.API.csproj
```

Only apply InitialCreate to a new database. If VeganHelperSystem already has tables/data, use a separate database or plan a baseline; do not apply blindly. Configure connection strings using user-secrets or environment variables, never commit credentials. The EF migrations history table is infrastructure, not an extra business table.

## Sprint 1 status

Sprint 1 authentication and profile flows are implemented. This includes JWT login, local email verification, Google ID-token login, explicit Google link/unlink, refresh/revocation, password reset, lockout and profile endpoints. The existing `user_identities` table stores Google identities; no additional business table is required for these flows.

### External authentication and email setup

The API validates Google ID tokens with the configured Google OAuth Web Client ID. Configure it outside tracked files:

```powershell
dotnet user-secrets set "Google:ClientId" "<google-web-client-id>" --project src/VeganHelper.API
```

Registration verification codes and password reset tokens are sent through SendGrid when configured:

```powershell
dotnet user-secrets set "SendGrid:ApiKey" "<sendgrid-api-key>" --project src/VeganHelper.API
dotnet user-secrets set "SendGrid:FromEmail" "<verified-sender-email>" --project src/VeganHelper.API
dotnet user-secrets set "SendGrid:FromName" "VeganHelper" --project src/VeganHelper.API
```

When the API runs in Development without SendGrid settings, the email adapter logs the message as a safe local fallback. Never commit API keys, app passwords or OAuth secrets.

Google-first users are verified from Google's validated `email_verified` claim. If the email already belongs to a local account, Google login returns a conflict; the user must authenticate locally and call the link endpoint. Google unlink requires the current local password and is rejected if no local password exists.

The original architecture guide is preserved as reference. Its sample Recipe model, string Role, embedded JWT key and Controller-to-Repository shortcut are not the authoritative implementation. Use the approved schema, store secrets outside Git, and route business calls through services.

## Meal planning datasets and provenance

FN26 backend endpoints, generation rules, migration and test instructions are documented in [FN26 API](docs/FN26_API.md).

The meal-planning data survey and sample import were completed on 12 October 2026. The table below distinguishes the dataset actually used for the imported ingredient energy values from datasets downloaded for evaluation only.

| Dataset | Version evaluated | Use in this project | Official source and download |
|---|---|---|---|
| USDA FoodData Central — SR Legacy | April 2018; 7,793 food records | Selected food references provide ingredient kcal/100 g for the generated sample dishes. | [USDA downloads](https://fdc.nal.usda.gov/download-datasets/) · [JSON archive](https://fdc.nal.usda.gov/fdc-datasets/FoodData_Central_sr_legacy_food_json_2018-04.zip) |
| USDA FoodData Central — Foundation Foods | April 2026; 363 valid food records in the downloaded JSON | Downloaded and evaluated; not used for the current sample import. | [USDA downloads](https://fdc.nal.usda.gov/download-datasets/) · [JSON archive](https://fdc.nal.usda.gov/fdc-datasets/FoodData_Central_foundation_food_json_2026-04-30.zip) |
| USDA FoodData Central — FNDDS | 2021–2023; October 2024 release; 5,432 food records | Downloaded and evaluated; not used for the current sample import. | [USDA downloads](https://fdc.nal.usda.gov/download-datasets/) · [JSON archive](https://fdc.nal.usda.gov/fdc-datasets/FoodData_Central_survey_food_json_2024-10-31.zip) |
| SMILING Food Composition Table for Vietnam | 2013; 164 food rows | Downloaded from FAO for local evaluation only. Application redistribution rights and energy units were not verified; no values were imported. This is not the 2017 Vietnamese food composition book. | [FAO source page](https://www.fao.org/food-composition/tables-and-databases/detail/%28vietnam--2013%29-smiling-food-composition-table-for-vietnam/en) · [XLSX workbook](https://www.fao.org/fileadmin/templates/food_composition/documents/FCT_SMILING_PROJECT_ASIA/D3_5a_SMILING_FCT_Vietnam_180713_protected.xlsx) |

USDA FoodData Central data is published under CC0; see the [official API guide and data licensing information](https://fdc.nal.usda.gov/api-guide/). Selected ingredient references retain the USDA FDC identifier, original food description, preparation state and source URL in the local seed provenance files. Raw and cooked food references are treated separately. Missing energy values are not replaced with zero.

### Recipe and weekly menu provenance

The 200 dishes and 30 weekly templates are original generated sample data prepared for this project. They are **not recipes or meal plans extracted from the datasets above, and have not been reviewed or approved by a dietitian**. All names and descriptions are in English. Each breakfast, lunch and dinner slot contains one dish.

The shared database import added 90 ingredients, reused 11 existing ingredient references, and added 200 dishes (140 vegan and 60 lacto-ovo vegetarian), 1,144 meal–ingredient links, 30 weekly templates (20 vegan and 10 lacto-ovo vegetarian), and 630 scheduled meal slots. Existing ingredient records were preserved.

Dish calories are estimates calculated from ingredient quantities in grams and reference kcal/100 g. They do not account for cooking yield, oil loss, nutrient retention or product-specific variation. USDA references alone do not certify a product as vegan or its cheese as using vegetarian rennet. Weekly templates are unreviewed examples, not personalized nutrition plans or guarantees of nutritional adequacy.

The following recipe/menu resources were also surveyed, but their recipes and schedules were **not copied into the database**:

- [PCRM Universal Meals](https://www.pcrm.org/universalmeals) and its [recipe collection](https://www.pcrm.org/universalmeals/recipes): culinary and nutrition reference; application reuse permission was not established. See the [PCRM disclaimer](https://www.pcrm.org/disclaimer).
- [PCRM 21-Day Vegan Kickstart](https://www.pcrm.org/vegankickstart/week1): weekly menu reference; no expert-approval claim was transferred to the project's generated templates.
- [Eat This Much vegan meal-plan examples](https://www.eatthismuch.com/diet-plan/vegan): menu reference only; consult its [terms of use](https://www.eatthismuch.com/terms) before reuse.

The Vietnamese Food Composition Table (2017) was considered during the survey, but no official downloadable dataset was obtained or used. Original source archives remain outside Git; the README links to the publishers rather than redistributing those archives.
