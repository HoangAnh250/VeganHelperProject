# FN30 AI Nutrition Chatbot

## Configuration

Copy `.env.example` to `.env` for local development and set `DeepSeekChat__ApiKey`.
The key is used only by the Backend. Do not put it in Frontend code or commit `.env`.

AI Genie uses `deepseek-flash` through DeepSeek Chat Completions. Set `DeepSeekChat__Model=deepseek-flash`. The chatbot uses non-thinking mode to reduce latency and token cost. Post moderation remains on its separate Gemini configuration. The public chat API contract does not change.

## Send a message

```http
POST /api/ai/chat
Content-Type: application/json
Authorization: Bearer <access-token>   # optional; omit for guest chat
```

```json
{
  "sessionId": null,
  "guestSessionId": null,
  "message": "Suggest a quick plant-based lunch"
}
```

For a guest, the Frontend creates a persistent UUID before the first request and
sends it as `guestSessionId`. If omitted, the Backend generates one in the first
response. The Frontend keeps the UUID and sends it with the returned `sessionId`
in later requests. For a logged-in member, the API associates the session with
the JWT user and ignores a guest session identifier.

Successful response:

```json
{
  "sessionId": 42,
  "guestSessionId": "00000000-0000-0000-0000-000000000000",
  "answer": "...",
  "createdAt": "2026-10-15T00:00:00Z",
  "model": "deepseek-flash",
  "guestQuestionsRemaining": 2,
  "recommendedPosts": []
}
```

## FN32: Find real VeganHelper recipe posts

When the user asks for meal or recipe suggestions, DeepSeek may call the single
`search_vegan_posts` function. The Backend validates its structured criteria and
queries only published, non-deleted posts outside moderation hold. A response
contains at most three cards with `postId`, `title`, and `thumbnailUrl`:

```json
{
  "recommendedPosts": [
    { "postId": 42, "title": "Mushroom tofu", "thumbnailUrl": null }
  ]
}
```

The Frontend links guests to `/posts/{postId}/guest` and members to
`/posts/{postId}`. DeepSeek receives only the selected public post IDs and titles;
it cannot issue SQL or read private data. If no post remains, the assistant may
suggest a general vegan dish but must say it is not a VeganHelper post.

The selected post IDs are saved with the AI chat message. History responses add
`recommendedPosts` to each message and resolve IDs against current public
visibility, so removed or hidden posts no longer appear as cards. The
`AddChatPostRecommendations` migration adds a nullable column to `chat_messages`.

For an authenticated member, `guestQuestionsRemaining` is `null`. Guests may
complete three chatbot requests per `guestSessionId`; successful responses
report the remaining count (`2`, `1`, then `0`). The fourth request is rejected
before calling DeepSeek. Guest questions are reserved atomically before calling
DeepSeek, so simultaneous requests cannot exceed the three-question trial.
Failed DeepSeek requests do not consume a guest question. Anonymous POST traffic
is also limited to 12 requests per remote IP per hour (`GUEST_CHAT_RATE_LIMITED`).
The guest client must retain and reuse the returned `guestSessionId`.

For authenticated requests, the Backend supplies available profile context to
the chatbot: stored meal type, current BMI (calculated from height and weight
when available), activity level, estimated maintenance calories (when enough
profile data exists), and declared allergies. The current chat request can override
the saved meal-type preference, but declared allergies remain safety
constraints. Missing profile data is not inferred.

Guest limit response (`HTTP 429`):

```json
{
  "error": "Guest chatbot limit reached. Please register or sign in to continue.",
  "code": "GUEST_CHAT_LIMIT_REACHED",
  "requiresRegistration": true
}
```

## Read history

```http
GET /api/ai/chat/{sessionId}?guestSessionId=<guest-session-id>
Authorization: Bearer <access-token>   # required for member-owned sessions
```

The API checks that the session belongs to the authenticated member or that the supplied guest session identifier matches the anonymous session.

## Main errors

| Status | Meaning |
|---|---|
| 400 | Empty/overlong message or missing guest session identifier |
| 404 | Session does not exist or is not owned by the caller |
| 422 | DeepSeek returned no answer |
| 429 | Guest trial limit (`GUEST_CHAT_LIMIT_REACHED`), anonymous IP rate limit (`GUEST_CHAT_RATE_LIMITED`), or DeepSeek quota/rate limit |
| 503 | DeepSeek is disabled, misconfigured, timed out, or unavailable |

Successful requests store the user message, AI message, and completed `AiUsage` record. Failed DeepSeek calls are recorded as failed usage and do not count toward the guest trial. Existing `ChatSession`, `ChatMessage`, `AiUsage`, `UserProfile`, and `UserAllergies` tables are reused; FN31/FN35 do not require a new migration.
