# FN30 AI Nutrition Chatbot

## Configuration

Copy `.env.example` to `.env` for local development and set `Gemini__ApiKey`.
The key is used only by the Backend. Do not put it in Frontend code or commit `.env`.

The default model is `gemini-2.5-flash-lite`. If the Google project does not have access to that model, change `Gemini__Model` in the local environment without changing the API contract.

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
  "message": "Gợi ý món trưa nhanh gọn"
}
```

For a guest, the first response contains a generated `guestSessionId`. The Frontend should keep that value and send it together with the returned `sessionId` in later requests. For a logged-in member, the API associates the session with the JWT user and ignores a guest session identifier.

Successful response:

```json
{
  "sessionId": 42,
  "guestSessionId": "00000000-0000-0000-0000-000000000000",
  "answer": "...",
  "createdAt": "2026-10-15T00:00:00Z",
  "model": "gemini-2.5-flash-lite",
  "guestQuestionsRemaining": 2
}
```

For an authenticated member, `guestQuestionsRemaining` is `null`. Guests may
complete three chatbot requests per `guestSessionId`; successful responses
report the remaining count (`2`, `1`, then `0`). The fourth request is rejected
before calling Gemini. Failed Gemini requests do not consume a guest question.
The guest client must retain and reuse the returned `guestSessionId`.

For authenticated requests, the Backend supplies available profile context to
the chatbot: stored meal type, current BMI (calculated from height and weight
when available), and declared allergies. The current chat request can override
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
| 422 | Gemini blocked the request or returned no answer |
| 429 | Guest trial limit (`code: GUEST_CHAT_LIMIT_REACHED`) or Gemini quota/rate limit |
| 503 | Gemini is disabled, misconfigured, timed out, or unavailable |

Successful requests store the user message, AI message, and completed `AiUsage` record. Failed Gemini calls are recorded as failed usage and do not count toward the guest trial. Existing `ChatSession`, `ChatMessage`, `AiUsage`, `UserProfile`, and `UserAllergies` tables are reused; FN31/FN35 do not require a new migration.
