# Sprint 1 implementation notes

The Sprint 1 branch implements the authentication and profile requirements in the existing API -> BLL -> DAL solution.

## Endpoints

- `POST /api/auth/register`
- `POST /api/auth/verify-email`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout` (authenticated)
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`
- `GET /api/users/me` (authenticated)
- `PUT /api/users/me` (authenticated multipart form; optional `avatar` file)

Registration OTP and password reset tokens are logged by the development flow because no SMTP provider is committed to the repository. Replace that adapter with the team's email provider before deployment. Only SHA-256 token hashes are persisted.

## Local configuration

Set a signing key outside tracked files before running the API:

```powershell
dotnet user-secrets set "Jwt:SigningKey" "<at-least-32-character-secret>" --project src/VeganHelper.API
```

Or set `Jwt__SigningKey` as an environment variable. The committed `appsettings.json` deliberately contains an empty signing key.

The avatar MVP stores validated JPG/PNG files below `wwwroot/uploads/avatars`; the storage interface can later be replaced by Cloudinary or another object store without changing the profile API contract.

## Schema

The Code First migration is `Sprint1AuthAndProfile`. It is pending until the team runs `dotnet ef database update`. The full SQL script and the ERD metadata were updated from 27 to 30 tables by adding `email_verification_tokens`, `password_reset_tokens`, and `refresh_tokens`, plus `users.phone_number`, `users.failed_login_attempts`, and `users.locked_until`.
