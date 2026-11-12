# AI moderation response diagnostics (post-moderation.v2.9)

Moderation policy is evaluated by the providers. The backend no longer classifies spam titles or animal-food endorsements using keyword/regex overrides. The system prompt covers those cases, adult sexual content, intimate nudity, obscenity/profanity, sensitive content requiring review, and the existing vegan-community policy. Neutral non-graphic health education is not automatically an adult-content violation.

## Provider coordination

DeepSeek reviews text and images. A separate task validates and reviews videos with TwelveLabs Pegasus. Without a TwelveLabs key, a video post cannot pass moderation and records `video_provider_unavailable` unless DeepSeek returns a valid FAIL.

- Either valid FAIL returns immediately and cancels pending work. Pending exceptions are observed; branch-owned cleanup still disposes temporary files.
- SAFE requires DeepSeek PASS and every Pegasus video PASS.
- A technical error cannot become PASS. The coordinator still waits for the other branch, because a valid FAIL is sufficient even if one branch failed technically.
- Caller cancellation propagates; the shared deadline bounds remaining work. Cancellation is best effort at the remote provider.
- Reports still never trigger automatic hiding. Posts edited after Admin hiding still go directly to manual approval without an AI scan.

## Response errors

| Code | Meaning |
| --- | --- |
| `response_json_invalid` | Outer HTTP response is not valid JSON. |
| `response_envelope_invalid` | Wrong outer object shape. |
| `response_output_count` | DeepSeek returned an unexpected number of choices. |
| `response_output_missing` | Missing or empty assistant output. |
| `response_verdict_json_invalid` | Text output cannot be parsed as verdict JSON. |
| `response_fields_missing` | Required verdict fields absent. |
| `response_fields_extra` | Unexpected verdict fields present. |
| `response_fields_duplicate` | Duplicate JSON field names. |
| `response_field_type` | Expected a string but received another type. |
| `response_field_empty` | Required text is blank. |
| `response_field_too_long` | Field exceeds its size bound. |
| `response_verdict_invalid` | Result is not exactly PASS or FAIL. |
| `response_checks_missing` | Coverage omits expected title/content/media targets. |
| `response_checks_extra` | Coverage includes unknown targets. |
| `response_checks_invalid` | Coverage has wrong structure or a value other than PASS/FAIL. |
| `response_model_invalid` | Returned model identifier is invalid. |
| `response_usage_invalid` | Token usage has an invalid type/value. |
| `response_incomplete` | Provider did not finish with stop. |
| `response_too_large` | Response exceeds the bounded payload size. |
| `response_result_invalid` | Processor rejected the normalized client result independently. |

Provider HTTP failures use `deepseek_http_*` or `twelvelabs_http_*`. HTTP/provider errors such as `provider_quota`, `provider_timeout`, `provider_error` and `provider_blocked` remain separate. Never treat malformed output, safety refusal, or a partial successful review as a safe post.

The HTTP response boundary logs provider, code, expected field where available, post ID, revision and attempt. It never logs raw provider output, user content or API keys. Missing-field diagnostics list only expected schema keys, never arbitrary provider-supplied property names.

Known response-format failures receive at most two provider attempts within the existing overall timeout. If both attempts fail, the scan keeps the specific code in `error_code` and becomes `manual_required`; it does not automatically hide a public post. Legacy `invalid_response` values in historical scans are retained; no shared-data rewrite/requeue is performed by this code change.

No database migration is needed for these codes (all fit the existing 50-character field). The unrelated `RemoveUnusedSchemaObjects` migration was applied on 2026-11-08 after the compatible BE deployment was verified.

## Verification

Unit and integration tests use simulated providers to check coverage, concurrent review, failure handling, and stored error codes.
