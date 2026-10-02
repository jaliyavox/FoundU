# FoundU React shell

## Local API configuration

The shell calls the ASP.NET Core API directly; it does not use a Vite proxy.
From `web`, create the ignored local configuration from the tracked template:

```powershell
Copy-Item .env.example .env.local
```

On macOS/Linux, use `cp .env.example .env.local`. Set `VITE_API_BASE_URL` in
`.env.local` to your API's origin. The template uses the repository's HTTP launch
profile (`http://localhost:5292`). Keep the web origin in the API's allowed CORS
origins, and restart Vite after changing environment variables.

Verify that local configuration is ignored with `git check-ignore .env.local`.
Only `.env.example` belongs in source control. Never put passwords, tokens, or
private keys in a `VITE_` variable: Vite includes those values in browser code.
Deployment builds must supply their own `VITE_API_BASE_URL` at build time.

```text
npm ci
npm run dev
```

## Playwright end-to-end tests

The browser suite lives in `e2e/` and runs independently of Vitest. By default it starts Vite
on port 5174 and uses the real ASP.NET API at `http://localhost:5292`; start the API and
database, then seed the development accounts using the repository's evaluation guide before
running it. Set `FOUNDU_E2E_BASE_URL` to target an already-running deployment; this skips local
Vite startup, and `VITE_API_BASE_URL` must point to that deployment's API.

```powershell
npx playwright install chromium
npm run test:e2e
```

The default student and staff credentials are the seeded demo accounts from
`docs/testing/evaluation-guide.md`. Set `FOUNDU_E2E_ADMIN_PASSWORD` (or `DEV_ADMIN_PASSWORD`)
to include admin tests; otherwise those cases are skipped. Account emails and the API/web URLs
can be overridden with the `FOUNDU_E2E_*_EMAIL`, `FOUNDU_E2E_*_PASSWORD`,
`VITE_API_BASE_URL`, and `FOUNDU_E2E_BASE_URL` environment variables. Set
`FOUNDU_E2E_CLAIM_ID` and `FOUNDU_E2E_ITEM_ID` to crawl seeded claim/item detail routes too.
Coverage includes role access and redirects, invalid login and duplicate registration, report
form date ordering/future-date rules, and description length boundaries. The report boundary
test posts one valid 1,000-character report, so use a development/test database that can be
reseeded. Do not run the full suite against production. The public visitor flow is read-only and
can be selected with `--grep "visitor can reach public feeds"`. A health preflight checks the
API before the browser suite starts.

## Session behavior

The shell validates stored tokens through `/api/auth/me` before restoring a user.
Startup validation fails closed: if the API is unavailable, sign in again when it
returns. Login and refresh return the full user DTO, while `/me` returns only
`id`, `email`, `name`, and `role`; the shared auth identity uses only those fields.

Logout clears local authentication and the shared React Query cache immediately,
then attempts refresh-token revocation. Existing private query keys are not scoped
to accounts, so the whole cache is cleared on logout/account changes. Request
versions prevent responses from an older session from restoring tokens or returning
private data into a newer session. Refresh requests share one promise within a tab;
this does not coordinate refresh-token rotation between multiple tabs.

Tokens remain in localStorage to match the current API transport. Any successful
XSS attack could read them; this patch does not introduce HttpOnly cookie transport.

## Vite template notes

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend enabling type-aware lint rules by installing `oxlint-tsgolint` and editing `.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "options": {
    "typeAware": true
  },
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  }
}
```

See the [Oxlint rules documentation](https://oxc.rs/docs/guide/usage/linter/rules) for the full list of rules and categories.
