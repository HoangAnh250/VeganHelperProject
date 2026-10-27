# Video transcription and recipe summary

## Flow

1. A signed-in viewer opens a published post with one or more ready video attachments and calls
   `POST /api/posts/{postId}/summary`.
2. The API stores one pending summary per post and queues a Hangfire job. Repeated requests reuse
   the same job state. The response is HTTP 202.
3. The job processes the post's video attachments in display order. It extracts mono audio with
   FFmpeg when installed; otherwise Groq transcribes the approved public R2 video URL.
4. Groq Whisper produces one transcript per clip. The Backend joins them with clip labels and
   sends the combined transcript to the configured Groq chat model for a concise recipe. Long
   transcripts are summarized in ordered chunks before a final combined summary.
5. The FE polls `GET /api/posts/{postId}/summary` while status is `pending` or `processing`.
   A completed response contains `summary` and `transcript`, which the user can copy.

## States and safeguards

- `not_requested`, `pending`, `processing`, `completed`, `failed`, `stale`.
- Only published, visible posts are accessible. POST requires authentication; GET can read a
  completed summary for a public post.
- POST is limited to six requests per account per hour, including cached requests.
- The source video IDs and post content revision are stored with the summary. If the post changes,
  the old result is hidden and a new request regenerates it.
- `post_media` upload behavior is unchanged. The migration replaces the single
  `post_summaries.source_media_id` reference with `source_media_ids_json` and
  `content_revision`. Existing summary source IDs are preserved during migration.
- The Groq key stays in the Backend's ignored `.env` or deployment environment variables.
  Set `GroqSummary__Enabled=true`, `GroqSummary__ApiKey`, and optionally
  `GroqSummary__FfmpegPath`. Run the PostgreSQL EF migration before enabling the feature.
- Groq's direct audio attachment limit on the free tier is 25 MB. Extracting 48 kbps MP3 audio
  keeps ordinary short clips below this limit. The public URL fallback depends on Groq's remote
  URL support and on the R2 media URL being reachable by Groq.
