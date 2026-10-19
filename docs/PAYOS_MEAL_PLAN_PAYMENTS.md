# PayOS weekly meal-plan access

## What is implemented

- Package catalog: `monthly` (1 month / 100,000 VND), `half_year` (6 months / 500,000 VND), and `yearly` (12 months / 1,000,000 VND).
- An authenticated user creates a checkout order. The Backend chooses the amount from the database; the client cannot set or override the price.
- A package grants access only after a verified PayOS webhook is received. The webhook handler is idempotent.
- Renewing before expiry starts the next period at the current access-chain expiry. Renewing after expiry starts a new period at the verified payment time.
- Active access permits generating and viewing weekly plans. For seven days after expiry, existing plans remain read-only. After that, both viewing and generation require renewal.
- Payment orders retain the user, package/price snapshots, PayOS order/link identifiers, state, provider reference, and timestamps.

## Configuration

Copy the PayOS keys into the local, ignored `.env` (or use the hosting provider's secret settings). Keep all credentials Backend-only:

```dotenv
PayOS__ClientId=...
PayOS__ApiKey=...
PayOS__ChecksumKey=...
PayOS__ReturnUrl=http://localhost:3000/meal-plans/payment/success
PayOS__CancelUrl=http://localhost:3000/meal-plans/payment/cancel
```

The local return/cancel pages can be replaced after Frontend routes are finalized. Production requires public HTTPS URLs. Do not put the PayOS API key or checksum key in Frontend environment variables.

## API routes

- `GET /api/meal-plan-payments/packages` — public package catalog.
- `POST /api/meal-plan-payments/checkout` — authenticated; JSON body `{ "packageCode": "monthly" }`; returns an order ID, order code, and checkout URL.
- `GET /api/meal-plan-payments/orders/{orderId}` — authenticated; only returns the current user's order.
- `GET /api/meal-plan-payments/access` — authenticated; returns `none`, `active`, `grace_period`, `scheduled`, or `expired`, plus generation/view flags.
- `POST /api/meal-plan-payments/webhook` — public PayOS callback; the SDK verifies the signed payload before any order is changed.

Meal-plan routes `POST /api/MealPlans/generate`, `GET /api/MealPlans`, and `GET /api/MealPlans/{id}` enforce the returned entitlement rules. A blocked request receives HTTP 402 with an `errorCode`.

## Database migration

The local EF migration is `AddMealPlanPayments`. It creates `meal_plan_packages`, `meal_plan_payment_orders`, and `user_meal_plan_access`, plus a PostgreSQL sequence for PayOS integer order codes. The generated migration is under `src/VeganHelper.DAL/PostgresMigrations/`. To check that the EF model and migration snapshot still agree:

```powershell
dotnet ef migrations has-pending-model-changes --project src/VeganHelper.DAL/VeganHelper.DAL.csproj --startup-project src/VeganHelper.API/VeganHelper.API.csproj --context AppDbContext
```

The migration enables RLS on all three payment tables and revokes direct table and sequence privileges from `PUBLIC`, `anon`, and `authenticated`. Package and payment data are served through the Backend API; the server's database-owner connection remains able to access them. Verify these protections after applying the migration to any shared database.

## Testing

The integration tests replace the PayOS adapter with a deterministic fake. They test the API contract, webhook idempotency, package grants, and expiry/grace behavior without making a real payment or calling PayOS. This does not validate live merchant credentials, a bank transfer, or a real PayOS callback. Configure the webhook URL in PayOS only when the deployed Backend has a public HTTPS endpoint.
