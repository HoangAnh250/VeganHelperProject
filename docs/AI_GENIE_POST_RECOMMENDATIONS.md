# AI Genie post recommendations

The existing `POST /api/ai/chat` response contract is unchanged. The frontend
continues rendering `answer` and up to three `recommendedPosts` cards.

## Search behavior

- A general request such as “gợi ý 3 post ăn chay” searches public posts without
  requiring ingredients, meal type or a preparation-time limit.
- Explicit post requests invoke the search tool instead of allowing the model
  to claim it searched without calling the backend.
- `searchTerms` supplies optional dish/topic terms matched against title or
  content. Broad topics have bounded Vietnamese/English aliases; dish names
  are not treated as mandatory ingredients.
- Existing declared allergies, structured ingredient checks and public-post
  visibility remain enforced. Missing ingredient data is not proof of allergy
  suitability. Update real recipe data rather than disabling those checks.
- `new` searches a fresh request/topic, `more` excludes previously recommended
  IDs in the conversation, and `repeat` restricts the search to previously
  recommended IDs while reapplying current eligibility filters.
- Results contain at most three real cards. With one or two cards, the backend
  states the exact count and does not append general-knowledge dishes. Only an
  empty search result permits the model to suggest dishes explicitly identified
  as general knowledge rather than platform posts.

No vector database or schema migration is required. The main nutrition system
instruction is preserved; only its search-specific instructions are updated.
Search terms use escaped, parameterized patterns with bounded expansion.

## Manual checks

1. Ask “gợi ý mình 3 post ăn chay”: expect a database lookup and up to three
   eligible cards without a follow-up question requiring an ingredient.
2. Ask for “post bánh ngọt”: verify eligible English cake/dessert titles can
   match as well. An account with allergies must not receive posts lacking
   verified structured ingredient data.
3. Ask “tìm thêm bài khác”: earlier card IDs should be excluded, with the
   prior topic and explicit constraints carried by the tool criteria.
4. Ask “gửi lại các bài trước”: eligible earlier cards can reappear; hidden,
   deleted or allergy-excluded posts cannot be restored through this mode.
5. Exercise datasets containing 0, 1, 2 and at least 3 eligible posts. The text
   count must match the returned cards; general-knowledge fallback is only for
   an empty result.

Automated coverage uses mocked provider responses for tool selection and
payloads, service mocks for card counts/history handling, and isolated Docker
PostgreSQL for topic queries, visibility, allergies and result limits. It does
not spend real AI quota or mutate the shared Supabase database.
