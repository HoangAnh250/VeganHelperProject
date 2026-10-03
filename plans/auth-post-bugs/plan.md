# Auth and recipe form regression fixes

Status: complete (2026-10-02)

Scope: show invalid-login errors without reloading; use the current account's token for both remember-me modes; never substitute the public feed or fake success for failed protected operations; accept named ingredients and cooking steps in create/update through the existing DTO/service/repository layers.

Acceptance: failed login stays on the form with an error; local/session tokens are mutually exclusive after login and all protected post calls read both modes; My Posts excludes other authors; PostgreSQL saves named ingredients and steps, updates existing relationships without duplicate-key errors, and invalid ingredient/step payloads return a validation error rather than reaching database constraints.

Validation: frontend service/storage/interceptor regressions and production build; backend service tests and real PostgreSQL transaction-rollback probes; browser verification of failed login. Preserve existing UI colors and local migration changes. Do not commit or push.

Result: 7 frontend regressions, 24 backend unit tests and 10 PostgreSQL integration tests passed. FE production build and BE build succeeded. The old checkout API process was stopped to release locked DLLs and restarted on 7180/5180 with the repaired code. HTTP probes confirmed databaseAvailable=true, invalid login=401 and unauthenticated My Posts=401. Browser login stayed on the same page with inline "Incorrect email or password" messages, preserving inputs. The original named-ingredient unit regression failed with ID 0 before the repair and passed after resolving the name. PostgreSQL integration saved named Unicode ingredients/steps, reused an ingredient on edit, kept the original step ID, enforced edit ownership, and isolated My Posts from another author; fixture writes rolled back. No commits or pushes.
