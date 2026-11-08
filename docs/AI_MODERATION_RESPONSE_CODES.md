# AI moderation response diagnostics (post-moderation.v2.8)

Moderation policy is evaluated by the providers. The backend no longer classifies spam titles or animal-food endorsements using keyword/regex overrides. The system prompt covers those cases, adult sexual content, intimate nudity, obscenity/profanity, sensitive content requiring review, and the existing vegan-community policy. Neutral non-graphic health education is not automatically an adult-content violation.

## Provider coordination

With a TwelveLabs key and video media, Gemini reviews text/images while a separate task validates and reviews videos with Pegasus. Videos within that task are processed sequentially, bounding fan-out to two provider branches. Without a TwelveLabs key, Gemini continues to review all media through its existing Files API path.

- Either valid FAIL returns immediately and cancels pending work. Pending exceptions are observed; branch-owned cleanup still disposes temporary files.
- SAFE requires Gemini PASS and every Pegasus video PASS.
- A technical error cannot become PASS. The coordinator still waits for the other branch, because a valid FAIL is sufficient even if one branch failed technically.
- Caller cancellation propagates; the shared deadline bounds remaining work. Cancellation is best effort at the remote provider.
- Reports still never trigger automatic hiding. Posts edited after Admin hiding still go directly to manual approval without an AI scan.

## Response errors

| Code | Meaning |
| --- | --- |
| `response_json_invalid` | Outer HTTP response is not valid JSON. |
| `response_envelope_invalid` | Wrong outer object shape. |
| `response_status_missing` | Missing or incorrectly typed completion status. |
| `response_steps_missing` | Missing/incorrect Gemini steps array. |
| `response_step_invalid` | Invalid or unsupported step shape/type. |
| `response_output_count` | Multiple final model outputs. |
| `response_output_missing` | Missing/empty final output or content array. |
| `response_part_invalid` | Output content is not an expected text part. |
| `response_verdict_json_invalid` | Text output cannot be parsed as verdict JSON. |
| `response_fields_missing` | Required verdict fields absent. |
| `response_fields_extra` | Unexpected verdict fields present. |
| `response_fields_duplicate` | Duplicate JSON field names. |
| `response_field_type` | Expected a string but received another type. |
| `response_field_empty` | Required text is blank. |
| `response_field_too_long` | Field exceeds its size bound. |
| `response_reason_too_long` | Reason exceeds 200 characters. |
| `response_verdict_invalid` | Result is not exactly PASS or FAIL. |
| `response_checks_missing` | Coverage omits expected title/content/media targets. |
| `response_checks_extra` | Coverage includes unknown targets. |
| `response_checks_invalid` | Coverage has wrong structure or a value other than PASS/FAIL. |
| `response_model_invalid` | Returned model identifier is invalid. |
| `response_usage_invalid` | Token usage has an invalid type/value. |
| `response_incomplete` | Pegasus did not finish with stop. |
| `response_too_large` | Response exceeds the bounded payload size. |
| `response_upload_url_missing` | Gemini Files upload URL was omitted. |
| `response_upload_url_invalid` | Upload URL is ambiguous, invalid or untrusted. |
| `response_file_json_invalid` | Gemini Files metadata is not valid JSON. |
| `response_file_invalid` | Invalid file resource fields/URI/type. |
| `response_file_mismatch` | Poll returned a different file identity. |
| `response_file_state_invalid` | Unexpected file-processing state. |
| `response_result_invalid` | Processor rejected the normalized client result independently. |

HTTP/provider errors such as `provider_quota`, `provider_timeout`, `provider_error` and `provider_blocked` remain separate. Never treat malformed output, safety refusal, or a partial successful review as a safe post.

The HTTP response boundary logs provider, code, expected field where available, post ID, revision and attempt. It never logs raw provider output, user content or API keys. Missing-field diagnostics list only expected schema keys, never arbitrary provider-supplied property names.

Known response-format failures receive at most two provider attempts within the existing overall timeout. If both attempts fail, the scan keeps the specific code in `error_code` and becomes `manual_required`; it does not automatically hide a public post. Legacy `invalid_response` values in historical scans are retained; no shared-data rewrite/requeue is performed by this code change.

No database migration is needed for these codes (all fit the existing 50-character field). The unrelated `RemoveUnusedSchemaObjects` migration remains deferred pending deployment of compatible BE instances.

## Verification

The final BE solution build/test run passed 398 unit tests and 374 integration tests. Tests cover both providers starting concurrently, either provider returning FAIL while the other stalls, sibling cancellation, both-PASS gating, malformed responses alongside FAIL/PASS, exact response-code persistence and removal of local semantic overrides. Provider calls in these tests are simulated; no live paid API requests were made. This validates orchestration and error handling, not the real model's classification accuracy.
