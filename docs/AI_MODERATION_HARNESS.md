# AI Moderation Response Harness

The backend validates provider responses before accepting any moderation verdict. This contract applies to DeepSeek text/image review and TwelveLabs Pegasus video review.

## Accepted contract

DeepSeek Chat Completions uses JSON mode with a single assistant choice. The backend checks the verdict and every expected coverage key locally.

- The HTTP response must be successful and its complete body at most 256 KiB.
- Provider envelopes must indicate successful completion. Error envelopes, incomplete generation, missing outputs and malformed JSON are rejected.
- The verdict contains exactly `result` and `reason`; DeepSeek coverage mode also requires exactly the expected `checks` keys. Duplicate or unexpected fields are rejected.
- `result` must be the exact string `PASS` or `FAIL`. Reasons must be nonempty strings. The parser does not extract JSON from arbitrary prose; a single outer Markdown JSON fence is tolerated.
- A valid reason is whitespace-normalized and bounded to 200 characters for storage/display. An overlong reason does not erase a valid FAIL or trigger another provider request.
- Pegasus generation uses a supported string pattern to request a one-line reason of at most 200 characters. It does not send unsupported `maxLength` constraints.
- DeepSeek checks every expected text/image target. A failing coverage check overrides an inconsistent overall PASS.

## Concurrent review

When video review is delegated to TwelveLabs, DeepSeek receives an explicit trusted text-and-images scope and the delegated video IDs. DeepSeek must not report delegated videos as missing. If the TwelveLabs key is missing, a text/image FAIL can still flag the post, but the scan cannot pass and records `video_provider_unavailable`. The entire post is safe only after both branches pass, including every video. A valid FAIL from either branch returns immediately and cancels unfinished sibling work. A technical failure is never interpreted as PASS and does not suppress a valid FAIL from the other branch.

## Diagnostics and recovery

HTTP failures retain sanitized codes such as `deepseek_http_400` and `twelvelabs_http_401`; response contract failures retain granular `response_*` codes. Rate limiting remains `provider_quota`. Worker logs include post ID, revision, scan ID, attempt and machine name. Logs do not print API keys, raw provider bodies or media URLs.

Transient failures use the existing bounded retry policy; malformed responses receive one bounded provider retry. Exhausted or nonretryable failures require manual review and never count as safe. Existing scans already in `manual_required` are not automatically requeued by this change. Reprocessing those scans is a separate operational action.

A parser can guarantee an accepted data shape and conservative state transitions, not provider availability or perfect semantic classification. Changes must be deployed/restarted on the worker that claims the shared queue before they take effect. This change does not require a database migration.

## Verification

Unit tests cover malformed envelopes, incomplete outputs, duplicate/missing/extra fields, invalid enums/types, oversized bodies, bounded reasons, coverage, concurrent cancellation and all-provider PASS requirements. PostgreSQL integration tests verify persisted failure codes, publication holds and notification behavior using an isolated local test database.
