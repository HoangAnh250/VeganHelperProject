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

This is a scaffold, NOT completed authentication. JWT, Google login, register/OTP, refresh/revocation, password reset, lockout and profile endpoints remain to implement. Sprint 1 requires schema additions (hashed expiring single-use verification/reset tokens, sessions/refresh revocation, lockout state and optional phone); these are not silently added to the approved 27-table model.

The original architecture guide is preserved as reference. Its sample Recipe model, string Role, embedded JWT key and Controller-to-Repository shortcut are not the authoritative implementation. Use the approved schema, store secrets outside Git, and route business calls through services.
