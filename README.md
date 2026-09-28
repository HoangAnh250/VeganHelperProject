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
