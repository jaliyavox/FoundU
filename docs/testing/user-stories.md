# FoundU — User Stories

Stories are written from the build as it stands, not from the original plan, so each one can be
demonstrated. Every story names its **owner** (M1 to M4, see the
[evaluation guide](evaluation-guide.md)), its **priority** (MoSCoW) and **how it was verified**
in the final testing pass ([test report](test-report.md)).

**Roles:** *Visitor* (not signed in) · *Student* (owner or finder) · *Staff* (lost-and-found desk)
· *Admin* (Staff access plus administration) · *System* (the AI service, background rules).

**Verified by:** **A** = automated test · **L** = live API check or end-to-end script · **B** =
browser crawl, logged in as the role · **M** = manual demo step in the evaluation guide.

---

## Epic 1 — Accounts, profile and settings · owner M1

| ID | Story | Acceptance criteria | Pri | Verified |
|---|---|---|---|---|
| US-01 | As a **visitor**, I want to create a student account with my university email, so that I can report lost items. | Password ≥ 8 characters with the Identity rules. A duplicate email returns 409. A duplicate student number returns 409, not a 500. The account is always created with the Student role. | Must | A, L |
| US-02 | As a **user**, I want to sign in and stay signed in, so that I don't log in on every visit. | 15-minute access token, 14-day refresh token that rotates. A reused refresh token is rejected. Five wrong passwords lock the account for 15 minutes. | Must | A, L |
| US-03 | As a **user**, I want to sign in with Google, so that I don't need another password. | The button shows only when the server has Google configured. First use creates the account; an existing email is linked when Google has verified it. | Should | A |
| US-04 | As a **user**, I want to edit my name, email and student number, so that the desk can identify me. | Changing the email checks it is free. The change shows immediately without signing in again. | Must | A, B |
| US-05 | As a **user**, I want to change my password, so that I can secure my account. | The current password is required when one exists. Every other session is signed out, and this device receives new tokens. | Must | A, L |
| US-06 | As a **user**, I want to sign out, so that nobody else uses my session. | The refresh token is revoked on the server. On mobile the device stops receiving push notifications. | Must | A |
| US-07 | As a **visitor**, I want a landing page that explains FoundU, so that I understand it before signing up. | `/` shows how it works, features and FAQ, and links to the public lost feed and found board. | Could | B |
| US-08 | As a **user**, I want a light and dark theme and an accessible UI, so that I can use it comfortably. | The theme toggle is remembered. Forms have labels and error messages linked with `aria-describedby`. | Could | B |

## Epic 2 — Lost items and listings · owner M1

| ID | Story | Acceptance criteria | Pri | Verified |
|---|---|---|---|---|
| US-10 | As a **student**, I want to report a lost item with category, type, colour, place and a *time window*, so that it can be matched with found items. | The item type must belong to the chosen category. The window's end must not be before its start, and the window cannot be in the future. Description is 1 to 1000 characters. Staff and Admin cannot file reports (403). | Must | A, L |
| US-11 | As a **student**, I want to attach up to 2 photos to my report, so that people recognise my item. | JPEG, PNG or WebP, at most 5 MB each, owner only. On mobile, a failed upload keeps the report and does not create a duplicate. | Should | A |
| US-12 | As a **student**, I want the description to fill in the type and colour for me, so that reporting is faster. | The Description Parser suggests type, colour and up to 5 features. If the AI is down, the form still works without suggestions. | Could | A |
| US-13 | As a **student**, I want to see all my reports and their stage (Active → Matched → Resolved or Withdrawn), so that I know where my item is. | "My reports" lists and filters them. Each card shows how many people said they found the item, and when. | Must | A, B |
| US-14 | As a **student**, I want to edit a report while it is still Active, so that I can add details. | Only the owner can edit, and only while Active. | Must | A |
| US-15 | As a **student**, I want to withdraw a report I no longer need, so that it leaves the feed. | Only the owner can withdraw. Any finder's in-progress handover is cancelled and its code stops working. | Must | A, L |
| US-16 | As a **student**, I want to mark "I got it back", so that the report closes and the person who helped is thanked. | Closes the report and notifies every finder. Points go only to a finder who actually helped. It cannot be closed twice. | Must | A, L |
| US-17 | As **anyone**, I want to browse a public lost-items feed with search and paging, so that I can check whether I have someone's item. | `/feed` works without signing in. Page size is capped at 100. Private details never appear. | Must | L, B |
| US-18 | As a **student**, I want to see possible matches for my report, so that I can claim my item. | Only the owner or Staff can see them (403 for anyone else). | Must | A, L |
| US-19 | As a **student**, I want to flag a report that looks wrong, so that moderators can review it. | The reason is 5 to 500 characters. The flag appears in Admin → Moderation. | Should | A |

## Epic 3 — Claims and verification, student side · owner M4

| ID | Story | Acceptance criteria | Pri | Verified |
|---|---|---|---|---|
| US-20 | As a **student**, I want to claim a found item from a match suggestion, so that I can get it back. | One open claim per item per student, and none once an item is approved for the report (one lost item is one found item). The report moves to Matched. | Must | A, L |
| US-21 | As a **student**, I want to answer verification questions, so that I can prove the item is mine. | Questions never reveal the private details. Answers are at most 500 characters. The claim moves to UnderReview. | Must | A, L |
| US-22 | As a **student**, I want to see my claims and their status, and cancel one, so that I stay informed. | "My claims" is paginated. I can cancel only while the claim is open. The collection code shows only to me once the claim is approved. | Must | A, B |
| US-23 | As a **student**, I want to dismiss a suggestion that isn't mine, so that my list stays clean. | The suggestion is marked Dismissed. | Should | A |

## Epic 4 — Found items (student finders) · owner M2

| ID | Story | Acceptance criteria | Pri | Verified |
|---|---|---|---|---|
| US-30 | As a **student finder**, I want to post an item I found, with a photo and where and when, so that its owner can see it. | Posted from the feed header, the sidebar or the found board, on web and mobile. The public text is at most 500 characters. Proof of ownership goes only to the desk. | Must | A, B |
| US-31 | As **anyone**, I want to browse a Found board and a "Fresh finds" strip, so that I can spot my item. | `/found` is public. Posting, withdrawing or handing in updates the board, the strip and My reports straight away. | Must | B |
| US-32 | As a **student**, I want to press "This is mine" on a found post, so that the finder and the desk know. | I must pick one of my own reports. It works even when the AI already paired them. The finder is notified. | Must | L |
| US-33 | As a **student finder**, I want to press "I found this" on someone's lost report and message them, so that we can arrange the return. | The owner is notified. Messages are at most 1000 characters. Only the two people in the thread can read it. | Must | A |
| US-34 | As a **finder**, I want to choose "give it to security" and get a handover code, so that the owner can collect it safely. | A 6-digit code, shown only to the finder and the owner. The report is paused for 48 hours, then returns to the feed on its own. The finder can cancel. | Must | A |
| US-35 | As a **finder**, I want to mark "I gave it to security", so that the post shows it is on its way to the desk. | Tracked on the post until the desk confirms. | Should | B |
| US-36 | As a **finder**, I want honor points and a "Help to find" page, so that my help is recognised. | +10 when a desk confirms a hand-in, +25 when the item gets home. Each outcome pays once. Staff cannot award points to themselves. | Should | A |

## Epic 5 — Desk work with codes (Staff found-item features) · owner M2

| ID | Story | Acceptance criteria | Pri | Verified |
|---|---|---|---|---|
| US-40 | As **staff**, I want to log a found item handed in at the desk, with storage location and private verification details, so that it can be claimed later. | Description at most 1000 characters. It can be linked to a lost report's hand-in code. Private details are never shown to students. | Must | A, L |
| US-41 | As **staff**, I want to search and filter found items, so that I can manage storage. | Paginated. Filter by status (only real statuses are accepted). | Must | B |
| US-42 | As **staff**, I want to look up a finder's post by its code and confirm it arrived, so that it becomes a stored item. | The status becomes Unclaimed and the finder earns +10 and is notified. | Must | A |
| US-43 | As **staff**, I want to enter a handover code and **receive** the item into storage, so that the owner is told where to collect it. | A storage location is required and an optional note is kept in the history. A code for a closed report is refused. | Must | A, L |
| US-44 | As **staff**, I want to **release** an item by code after checking the collector's ID, so that it goes to the right person. | The ID-check box is required (400 without it). A code works only once (404 the second time). The finder earns +25. | Must | A, L |
| US-45 | As **staff**, I want to hand over an approved claim by its collection code, so that the owner leaves with the item. | The desk looks the code up and sees the owner's name first. Hand-over needs the ID-check tick (400 without). The lost report becomes Resolved and the found item Returned. The code works only once. | Must | A, L, B |

## Epic 6 — AI agents and matching · owner M3

| ID | Story | Acceptance criteria | Pri | Verified |
|---|---|---|---|---|
| US-50 | As **staff**, I want to ask the AI whether a lost report and a found item match, so that I can suggest the match to the owner. | Score = 40% type + 20% colour + 25% public-description token overlap + 15% location. Missing evidence earns zero. Different type always scores zero. Only scores >=0.75 create candidates; >=0.50 and <0.75 is manual_review; below 0.50 is no_match. If the AI is down, staff can still create a manual suggestion. | Must | A, L |
| US-51 | As the **system**, I want to check each new found post against recent open reports, so that owners hear about likely matches without waiting for staff. | Runs when a post is created. Each match creates a suggestion and a "possible match" notification. | Should | L |
| US-52 | As **staff**, I want the AI to draft verification questions from the private details, so that claimants must prove ownership. | Fixed safe templates. Any AI wording that leaks a private value is rejected. There is a deterministic fallback. | Must | A |
| US-53 | As **staff**, I want the AI to recommend a verdict on a claimant's answers, so that reviews are faster. | The recommendation is advisory only. Scores are never shown to the claimant. | Should | A |
| US-54 | As a **student**, I want to describe my loss in the "Ask FoundU" chat, so that it searches for me and drafts my report. | Picks up item, colour (the first one mentioned), place and time. Searches found items and offers to claim or draft a report. It also helps finders reach the owner. | Should | A |
| US-55 | As the **team**, I want the AI service to run without a GPU or model download, so that the demo and CI are reliable. | `LLM_PROVIDER=fake` is fully deterministic. With Ollama, LLM output is always checked against the source text. | Must | A |
| US-56 | As the **system**, I want AI endpoints protected by a shared service key, so that only the API can call them. | No key or a wrong key → 401 (including non-ASCII keys). No key configured → 503. The browser and phone never see the key. | Must | A |
| US-57 | As **staff**, I want a coordinator workflow that pauses for human approval, so that the AI never makes the final decision. | The workflow state is stored in PostgreSQL. Staff approve or reject in the claim's approval panel. | Should | A |

## Epic 7 — Notifications and push · owner M3

| ID | Story | Acceptance criteria | Pri | Verified |
|---|---|---|---|---|
| US-60 | As a **user**, I want an in-app notification inbox with an unread badge, so that I don't miss updates. | 15 event types. Mark one or all as read. Each user sees only their own (404 for anyone else's). | Must | A, L |
| US-61 | As a **user**, I want tapping a notification to open the right screen, so that I can act on it. | Claim → claim page. Report → My reports. A finder's return or confirmation → feed. Support → ticket. A message → inbox (mobile push). | Must | A |
| US-62 | As a **student**, I want push notifications on my phone, so that I hear about matches and approvals straight away. | FCM is sent only after the database save commits. The payload is only title, body, type and ids, never private data. Invalid device tokens are deactivated. | Should | A |
| US-63 | As a **student**, I want my device registered when I sign in and removed when I sign out or am suspended, so that pushes reach only me. | Re-registering moves the token to the new user. Suspension deactivates the user's devices. | Should | A |

## Epic 8 — Staff and Admin panel · owner M4

| ID | Story | Acceptance criteria | Pri | Verified |
|---|---|---|---|---|
| US-70 | As **staff**, I want an overview of queues (claims to review, items in storage, open tickets, flags), so that I know what needs doing today. | Staff see the figures; only Admin see links to admin-only pages. | Must | B |
| US-71 | As **staff**, I want a claim queue and claim detail with questions, answers, AI runs and decision buttons, so that I can verify ownership. | Approve, Reject (reason required) or Request revision. Approving closes rival claims on the same item. Staff never see the collection code. | Must | A, L |
| US-72 | As an **admin**, I want to overturn a wrongful rejection, so that mistakes can be corrected. | A reason of at least 10 characters. Both decisions are kept for audit. The report goes back to Matched. Refused if the item has gone or the report is closed. | Should | L |
| US-73 | As an **admin**, I want to search users and suspend or reinstate them with a reason, so that I can stop abuse. | I cannot suspend myself or another admin. Suspension blocks login and refresh immediately, and a token already issued stops on its next request. Sessions and devices are revoked. | Must | A, L |
| US-78 | As an **admin**, I want to make a registered user Staff or Admin, so that I can staff the desk without touching the database. | Never my own role. Only real roles. The user is signed out and returns with the new access. | Must | A, L, B |
| US-74 | As an **admin**, I want to manage places, categories, item types and storage locations, so that the pickers match the campus. | Add, rename, retire or restore. Delete only what no record uses (409 otherwise), and never the last available one. | Must | A, L |
| US-75 | As an **admin**, I want analytics (returns, mean days to return, storage, 30-day chart), so that I can report on the service. | Counts come from real records. There is a "show as table" view for accessibility. | Should | B |
| US-76 | As an **admin**, I want a moderation page for flagged reports and rejected claims, so that I can act on them. | Clear a flag in one click (the API also allows Staff). Paged, so every flag can be reached. | Should | B |
| US-77 | As **staff**, the pages I can't use should be blocked, so that access is enforced on the server and in the browser. | The API enforces Student, Staff and Admin policies. Web routes redirect to `/forbidden`. | Must | L, B |

## Epic 9 — Support tickets · owner M4

| ID | Story | Acceptance criteria | Pri | Verified |
|---|---|---|---|---|
| US-80 | As a **user**, I want to open a support ticket with a category and message, so that the desk can help me. | Category must be a real one (a numeric string is rejected). Subject is at most 200 characters; the message is 10 to 4000. | Must | A, L |
| US-81 | As a **user**, I want to see my tickets and reply in the thread, so that I can follow up. | Only the owner or Staff can read or write it. A closed ticket takes no more messages. | Must | A, L |
| US-82 | As **staff**, I want a support queue with stats, assignment and status changes, so that tickets get answered. | Open, Waiting, resolved-today and unassigned counts. Assignment only to Staff or Admin. The user is notified on reply or resolve. | Must | A |

---

### Coverage

| Epic | Stories | Owner |
|---|---|---|
| 1 Accounts and profile | 8 | M1 |
| 2 Lost items and listings | 10 | M1 |
| 3 Claims (student side) | 4 | M4 |
| 4 Found items (finders) | 7 | M2 |
| 5 Desk work with codes | 6 | M2 |
| 6 AI agents and matching | 8 | M3 |
| 7 Notifications and push | 4 | M3 |
| 8 Staff and Admin panel | 9 | M4 |
| 9 Support tickets | 3 | M4 |
| **Total** | **59** | M1 18 · M2 13 · M3 12 · M4 16 |
