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
  "model": "gemini-2.5-flash-lite"
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
| 429 | Gemini quota/rate limit |
| 503 | Gemini is disabled, misconfigured, timed out, or unavailable |

Every request stores the user message, AI message, and `AiUsage` record. Existing `ChatSession`, `ChatMessage`, and `AiUsage` tables are reused; FN30 does not require a new migration.
