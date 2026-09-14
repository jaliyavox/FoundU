# FoundU API conventions

The rules every endpoint follows, so a client written against one feature works against the
next. Code comments point here by section name: **Auth flow**, **Error envelope**,
**Validation**, **Pagination**. Everything below is taken from the code as it stands, not from
a plan.

Base URL in development: `http://localhost:5292`. Swagger at `/swagger`.

---

## Auth flow

JWT bearer tokens with refresh-token rotation, on ASP.NET Core Identity.

| Route | Auth | Purpose |
| --- | --- | --- |
| `POST /api/auth/register` | none | Creates a **Student** account and signs it in - one round trip, no second login |
| `POST /api/auth/login` | none | Email + password |
| `POST /api/auth/refresh` | none | Trades a refresh token for a new pair. The old refresh token is **revoked** - each one works once |
| `POST /api/auth/logout` | none | Revokes the given refresh token |
| `GET /api/auth/me` | bearer | The signed-in user, from the token |

Login, register and refresh all return the same shape:

```json
{
  "accessToken": "eyJ...",
  "refreshToken": "…",
  "user": { "id": "…", "fullName": "…", "email": "…", "role": "Student" }
}
```

- Access tokens live **15 minutes**, refresh tokens **14 days** (`Jwt:AccessTokenMinutes`,
  `Jwt:RefreshTokenDays`).
- Send `Authorization: Bearer <accessToken>`. On a `401`, refresh once and retry once. A second
  `401` means the session is over - clear tokens and go to login. The web client does this in
  `web/src/lib/api/client.ts`, single-flight so parallel requests share one refresh.
- **Roles** are `Student`, `Staff`, `Admin`. Three policies gate endpoints:
  `Student` (students only - Admin is deliberately *not* a student), `Staff` (Staff **or**
  Admin - an admin can work the desk they administer), `Admin`.
- Password policy: 8+ characters with an uppercase letter, a lowercase letter and a digit.
  Five failed logins lock the account for 15 minutes.
- A **suspended** account is refused at login *and* at refresh, so suspension takes effect
  within one access-token lifetime even for someone already signed in.
- Public endpoints that are richer for a signed-in caller (the lost feed) accept an optional
  bearer token. A missing or expired token there is not an error - it is anonymous. The web
  client marks these `optionalAuth` so an expired token does not sign a reader out mid-browse.

---

## Error envelope

Every non-2xx response is RFC 9457 `ProblemDetails`, produced by one handler
(`GlobalExceptionHandler`) from the application exceptions:

| Exception | Status | When |
| --- | --- | --- |
| `ValidationAppException` | **400** | A request failed a rule (see Validation) |
| `UnauthorizedAppException` | **401** | No usable identity |
| `ForbiddenAppException` | **403** | Signed in, but not allowed this |
| `NotFoundAppException` | **404** | The thing does not exist - **or is not yours to know about** (see below) |
| `ConflictAppException` | **409** | The request is well-formed but the state refuses it: withdrawing a resolved report, deciding a claim twice, flagging a flagged report |
| anything else | **500** | Logged with a trace id; `detail` is withheld outside Development |

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "Conflict",
  "status": 409,
  "detail": "This claim has already been decided.",
  "instance": "/api/claims/…/decision",
  "traceId": "00-…"
}
```

`detail` is written to be shown to a person as-is. The web client surfaces it in a toast.

**404 versus 403.** Where the difference would confirm that something exists - reading another
student's notification, for instance - the API answers **404**, not 403. Ownership checks on
things whose existence is not secret (a claim, a report) answer 403 with a sentence saying
whose it is.

---

## Validation

FluentValidation runs against every request body **before the action executes**
(`ValidationFilter`, registered globally). A failure is a 400 with an `errors` map keyed by
**the C# property name**, PascalCase, each holding one or more messages:

```json
{
  "title": "Validation failed",
  "status": 400,
  "errors": {
    "Description": ["Describe the item in at least 10 characters so it can be matched."],
    "EstimatedLostToAt": ["The end of the time window must be on or after the start."]
  }
}
```

Bind these to fields by the same PascalCase key (`Email`, `Password`, `Reason`) - not the
JSON camelCase you sent. The web client exposes them as `ApiError.fieldErrors`.

Rules that are about *state* rather than *shape* (is this report still active? has this claim
been decided?) are not validators - they are `ConflictAppException`s from the service, so
they carry the same envelope with a 409.

Every request DTO has a validator. If you add a DTO, add its validator in the same commit;
`AddValidatorsFromAssembly` picks it up and an empty body no longer reaches `.Trim()`.

---

## Pagination

Every list endpoint takes the same query string and returns the same envelope.

**Query** (`PaginationQuery`):

| Parameter | Default | Notes |
| --- | --- | --- |
| `page` | 1 | 1-based; values below 1 are clamped to 1 |
| `pageSize` | 20 | Clamped to 1-100; anything else falls back to 20 |
| `search` | - | Free text; what it matches is per endpoint and documented on it |
| `sortBy` | per endpoint | **Allow-listed** column names only. Unknown names fall back to the endpoint's default sort - never to raw SQL |
| `sortDirection` | `desc` | `asc` or `desc` |

Feature filters (`status`, `categoryId`, `flagged`, …) sit alongside these on the endpoint's
own query class.

**Response** (`PagedResult<T>`):

```json
{
  "items": [ … ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 57,
  "totalPages": 3,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

Default orderings are chosen per endpoint and say why in code: the public feed is newest
first, the staff claim queue is **oldest** first (the longest wait is worked next), a
student's own lists are newest first.

---

## Two shapes worth knowing about

**The student-safe item.** `FoundReportSummaryDto` is the only shape of a found item that
reaches a student, and it carries no `PrivateVerificationDetails`. The claim's verification
questions are written from that hidden detail and the claimant answers from memory - which
is the entire basis of proving ownership. Any new student-facing surface that shows a found
item **must** project through this shape. `ClaimAnswer.IsCorrect` is likewise withheld from
students: telling a claimant which answers passed hands a false one a feedback loop.

**The public feed.** `LostReportFeedItemDto` carries the poster's display name and nothing
else identifying - no email, no student number, no user id. `isMine` is computed
server-side so the client can hide "I found this" on your own post without the payload
carrying who "you" are.

---

## Writes that tell someone

Notifications are queued onto the **same unit of work** as the event they describe
(`INotificationService.Queue` adds the row; the caller's `SaveChanges` commits both). A
service that changes something a person is waiting on - a claim decided, an item suggested,
a message sent - queues the notification in the same method, before saving. Do not send
notifications from a controller or after the fact.

---

## Dates

All timestamps are UTC and serialised as ISO-8601 with a `Z`. Clients convert for display.
`<input type="datetime-local">` values are local wall time - convert to UTC before sending
(`toUtcIso` in `web/src/features/reports/reports-api.ts`).
