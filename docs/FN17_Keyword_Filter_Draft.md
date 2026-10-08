# FN17 - Keyword Filter Draft (v0.1)

## 1. Scope

This document is a review draft for the Sprint 2 comment moderation rule.

- The first version uses a deterministic keyword and phrase filter only.
- No Gemini, Perspective API, Hugging Face inference API, or locally trained AI model is required in Sprint 2.
- AI moderation can be added as a second layer in Sprint 3.
- The filter must not moderate normal vegan food names such as `thit chay`, `bo lat chay`, `ga chay`, or `com suon chay`.

## 2. Recommended data source

Use a curated project-owned list as the source of truth:

1. Start with terms agreed by the team and mentor for the Vietnamese community.
2. Add new terms only after a moderator confirms that they are abusive in context.
3. Open-source Vietnamese profanity lists on GitHub or Hugging Face may be used as supplementary references, but review their license and remove false positives before adding them.
4. Do not copy an entire public list directly into production. A smaller reviewed list is safer for this project.

The first implementation can keep the list in a configuration file. If Admin needs to edit it from the website later, move the approved rules into a database table.

## 3. Moderation behavior for Sprint 2

When a comment is submitted:

1. Trim leading and trailing whitespace.
2. Normalize Unicode, lowercase, and create an accent-insensitive comparison key (`đ` becomes `d`); keep the original text for display.
3. Collapse repeated whitespace and check both exact tokens and approved phrases.
4. Do not use a raw substring search for short terms such as `cc`, `cl`, or `dm`; this creates many false positives.
5. If a rule has action `hide_pending_review`, create the comment as hidden and mark it for Admin review.
6. If a rule has action `block_rejected`, reject the request immediately, do not save the comment, and do not create an Admin review item.
7. If an ambiguous rule matches, mark it for review only when the team decides that the comment should not be displayed immediately.
8. If no rule matches, create the comment normally.

Suggested internal rule fields for a future implementation:

```text
keyword, normalized_keyword, category, severity, action, is_active
```

Suggested actions:

- `hide_pending_review`: not visible to normal users; Admin can approve or remove it.
- `block_rejected`: reject the comment immediately; it is not saved and does not enter the Admin queue.
- `review_only`: keep the comment visible or temporarily hidden according to the final team decision.
- `allow`: explicitly exclude a known safe phrase.

## 4. Initial high-confidence list

These entries are examples for the first team review. They should be stored in lowercase after normalization.

### 4.1 Sexual profanity and explicit abuse

```text
dit
du
deo
lon
cac
buoi
diem
dit me
dit ma
du me
du ma
con di
do con di
thang cho
do cho
suc vat
```

Recommended default action: `hide_pending_review`.

The corresponding accented and full-form variants should also be included in the review list:

```text
địt
đụ
đéo
lồn
cặc
buồi
đĩ
địt mẹ
địt má
đụ mẹ
đụ má
con đĩ
đồ con đĩ
thằng chó
đồ chó
súc vật
địt con mẹ mày
đụ con mẹ mày
```

The comparison key should be accent-insensitive, so both `địt mẹ` and `dit me` match the same rule. Keep the accented forms in the review document so Admin can recognize what the rule means.

### 4.2 Direct harassment or threats

Use phrases instead of isolated words so normal cooking discussions are not blocked.

```text
tao giet may
giet may di
danh chet may
chem may
dot nha may
tu tu di
```

Recommended default action: `hide_pending_review`.

Accented variants:

```text
tao giết mày
giết mày đi
đánh chết mày
chém mày
đốt nhà mày
tự tử đi
```

### 4.3 Insults that are blocked immediately

These terms are direct insults that can provoke conflict. They are blocked immediately and do not require Admin review.

```text
ngu
oc cho
vo hoc
mat day
khon nan
```

Recommended default action: `block_rejected`.

Accented variants:

```text
ngu
óc chó
vô học
mất dạy
khốn nạn
```

### 4.4 Abbreviations that are blocked immediately

These abbreviations are explicitly requested for the project's hard-filter list:

```text
đmm
dmm
đm
dm
đcm
dcm
cl
cc
```

Recommended default action: `block_rejected`.

Match these as complete tokens or phrases, not as arbitrary substrings. For example, `cc` must not be searched inside every longer word or URL.

## 5. Ambiguous abbreviations and evasion variants

The following terms should not be hard-blocked by default because they can be used as casual emphasis or have non-abusive meanings:

```text
vcl
vl
vãi
```

For the current project rule, a sentence such as `Cat qua nay kho vcl` should not be automatically hidden. These terms may be logged or sent to `review_only` if the team later wants stricter moderation.

Common evasion forms to normalize or review later:

```text
d.i.t
d-i-t
d1t
d* m
d m
```

Do not continuously expand this list manually. Prefer a small normalization function and a reviewed phrase list; AI moderation in Sprint 3 can handle more contextual variations.

## 6. Safe allowlist examples

These phrases must remain allowed because they are valid vegan food terms:

```text
thit chay
bo lat chay
ga chay
com suon chay
nuoc mam chay
suon non chay
```

An allowlist should be applied only to complete approved phrases. It must not override a separate high-confidence threat phrase in the same comment.

## 7. Test cases for the Backend

| Input | Expected result |
|---|---|
| `Cắt quả này khó vcl` | Allowed; no automatic hiding |
| `Món này ngon quá` | Allowed and visible |
| `Cơm sườn chay với nấm` | Allowed and visible |
| `Địt mẹ mày` | Hidden and marked for review |
| `ĐMM`, `đcm`, `cl`, or `cc` as complete tokens | Rejected immediately; comment is not saved |
| `Óc chó` or `mất dạy` | Rejected immediately; comment is not saved |
| `Tao giết mày` | Hidden and marked for review |
| `  món ngon  ` | Trimmed and allowed |
| 501 characters | Rejected by the 1-500 character validation rule |
| only spaces | Rejected by the 1-500 character validation rule |

## 8. Sprint 3 AI moderation scope

AI is a second moderation layer after the deterministic keyword filter. AI can call a pretrained moderation API or a local pretrained model; the project does not need to train a model for this feature.

### 8.1 Content that AI should flag for Admin review

- English or multilingual profanity, harassment, and abusive phrases that are not in the local keyword list.
- Spelling variations, leetspeak, inserted punctuation, and intentionally separated characters.
- Indirect insults, sarcasm, bullying, personal attacks, or comments that only become offensive when read in context.
- Threats or encouragement of violence that do not match an exact keyword phrase.
- Sexual harassment, hate speech, or discriminatory attacks.
- Repeated provocation or comments that are likely to start a personal conflict.
- Optional: spam, scams, or clearly off-topic promotional comments.

Examples of cases suitable for AI review:

- An English insult written without any Vietnamese blocked word.
- A sentence that appears neutral alone but attacks the author when read with the parent comment.
- A disguised abusive phrase using numbers, punctuation, or mixed languages.
- A sarcastic threat that should be reviewed by a human instead of being automatically rejected.

### 8.2 Content that AI should not decide alone

- Normal vegan food terms such as `com suon chay`, `bo lat chay`, or `thit chay`.
- Casual emphasis such as `vcl` or `vl` when it is not directed at another person.
- A user's account ban or suspension. Account penalties require a separate repeated-violation policy.
- Automatic deletion of a comment based only on an AI score.

### 8.3 AI moderation workflow

1. Run the keyword filter first. A `block_rejected` rule rejects the comment immediately.
2. Send only comments that pass the hard filter, or comments selected for contextual review, to the AI moderation provider.
3. If AI detects a possible violation, set the comment to `flagged` or `pending_review` and hide it temporarily.
4. Store the category, confidence score, short reason, model/provider, and timestamp.
5. Admin approves the comment or removes it. AI does not ban or permanently delete it.
6. If the AI provider is unavailable, do not convert a normal comment into a hard ban solely because of the outage; use the project's configured pending-review fallback.

Suggested moderation result fields:

```text
is_flagged, category, confidence_score, reason, provider, model, reviewed_by, reviewed_at
```

The team should choose the confidence threshold only after testing sample comments. Do not hard-code a threshold before selecting the provider.

## 9. Team decisions still required

- Confirm the exact Vietnamese terms and their spelling variants.
- Decide whether `review_only` comments are visible while waiting for Admin.
- Decide the database status names (`flagged`, `pending_review`, or another existing status).
- Review this draft with the mentor before enabling automatic hiding in production.
