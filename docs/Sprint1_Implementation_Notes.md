# Sprint 1 implementation notes

The Sprint 1 branch implements the authentication and profile requirements in the existing API -> BLL -> DAL solution.

## Endpoints

- `POST /api/auth/register`
- `POST /api/auth/resend-verification`
- `POST /api/auth/verify-email`
- `POST /api/auth/login`
- `POST /api/auth/google`
- `POST /api/auth/google/link` (authenticated)
- `DELETE /api/auth/google/link` (authenticated)
- `POST /api/auth/set-password` (authenticated Google-first account)
- `POST /api/auth/refresh`
- `POST /api/auth/logout` (authenticated)
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`
- `GET /api/users/me` (authenticated)
- `PUT /api/users/me` (authenticated multipart form; optional `avatar` file)

Registration OTP and password reset tokens are sent by the SendGrid adapter when `SendGrid:ApiKey` and `SendGrid:FromEmail` are configured. Development without those settings uses a log fallback so the flow remains testable. Only SHA-256 token hashes are persisted.

## Google and local account rules

- A local account becomes verified only after the registration OTP is accepted.
- A first-time Google login creates a verified Google-backed account and a `user_identities` row.
- Google login never silently merges with an existing local email. It returns `409 Conflict`; the user must log in locally and call `POST /api/auth/google/link` with the validated Google ID token.
- `DELETE /api/auth/google/link` requires the current local password, so unlinking cannot remove the user's last login method or be performed with a stolen access token alone.
- A Google-first user can establish a local password through `POST /api/auth/set-password` before unlinking.

## External provider configuration

```powershell
dotnet user-secrets set "Google:ClientId" "<google-web-client-id>" --project src/VeganHelper.API
dotnet user-secrets set "SendGrid:ApiKey" "<sendgrid-api-key>" --project src/VeganHelper.API
dotnet user-secrets set "SendGrid:FromEmail" "<verified-sender-email>" --project src/VeganHelper.API
dotnet user-secrets set "SendGrid:FromName" "VeganHelper" --project src/VeganHelper.API
```

The Frontend obtains a Google ID token using Google Identity Services and sends it to `POST /api/auth/google` or `POST /api/auth/google/link`. The Backend validates the token audience, signature, issuer, expiry and verified-email claim before using it.

## Local configuration

Set a signing key outside tracked files before running the API:

```powershell
dotnet user-secrets set "Jwt:SigningKey" "<at-least-32-character-secret>" --project src/VeganHelper.API
```

Or set `Jwt__SigningKey` as an environment variable. The committed `appsettings.json` deliberately contains an empty signing key.

The avatar MVP stores validated JPG/PNG files below `wwwroot/uploads/avatars`; the storage interface can later be replaced by Cloudinary or another object store without changing the profile API contract.

## Schema

The Code First migration is `Sprint1AuthAndProfile`. It is pending until the team runs `dotnet ef database update`. The full SQL script and the ERD metadata were updated from 27 to 30 tables by adding `email_verification_tokens`, `password_reset_tokens`, and `refresh_tokens`, plus `users.phone_number`, `users.failed_login_attempts`, and `users.locked_until`.
