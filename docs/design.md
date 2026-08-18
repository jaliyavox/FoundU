# FoundU — Web Design & UI Conventions

How we build screens in `/web`, so four people produce one coherent product instead of four
different-looking dashboards. Read this before writing your first component.

**Stack:** React 19 · Vite · TypeScript · Tailwind CSS v4 · shadcn/ui (`base-nova` style, built
on Base UI) · lucide-react icons · TanStack Query · React Router v7 · sonner (toasts).

---

## 1. Golden rules

1. **Never hardcode a colour.** Use semantic tokens (`bg-background`, `text-muted-foreground`),
   never raw palette classes like `bg-blue-500` or hex values.
2. **Never hand-roll a component that shadcn already has.** Check the registry first.
3. **Every list, form and detail view needs loading, empty and error states.** Not optional —
   it is in the definition of done.
4. **Every input needs a `<Label htmlFor>`.** No placeholder-only fields.
5. **Import through `@/`**, never `../../..`.

---

## 2. Brand

### Palette

Four greens. They are defined once in [`web/src/index.css`](../web/src/index.css) as
`--brand-*` and exposed to Tailwind as `bg-brand-mist`, `text-brand-green` and so on.

| Name | Hex | OKLCH | Where it goes |
|---|---|---|---|
| **Mist** | `#E7F3EA` | `oklch(0.953 0.017 153.6)` | Section bands, icon chips, badge fills |
| **Sage** | `#A6D6A6` | `oklch(0.830 0.083 144.8)` | Hairline borders, dark-mode accent text |
| **Green** | `#64BC6D` | `oklch(0.721 0.141 146.1)` | Accents, focus rings, hover, eyebrow text |
| **Forest** | `#1F5D20` | `oklch(0.424 0.113 143.5)` | Primary buttons, CTA band, the logo tile |

**Forest is the actionable colour, not the mid green.** White on `#64BC6D` measures about
2.3:1 — well under the 4.5:1 this document requires. Forest gives roughly 8:1. The mid green
does accent work, where contrast minimums do not apply.

Dark mode inverts that pairing: on a dark ground the mid green becomes the readable one, so
`--primary` is green with a near-black foreground. Both themes derive from these same four
values — you never need to pick a green yourself.

**Use `brand-*` utilities only when a colour is genuinely "the brand"** — a logo tile, an
accent rule, an eyebrow. Everything else uses the semantic tokens in the next section, which
already resolve to brand greens.

### Logo

[`FoundUMark` and `FoundULogo`](../web/src/components/brand/foundu-logo.tsx). A crate of
handed-in items with a magnifier asking whose they are.

```tsx
<FoundULogo markClassName="size-9" />          // mark + "FoundU" wordmark
<FoundUMark decorative className="size-8" />   // mark alone, beside existing text
```

The mark is a **fixed lockup — mist artwork on a forest tile in both themes**, the way a
printed logo behaves. It deliberately ignores the semantic tokens so it stays recognisable
anywhere. The lens is knocked out with `fill-rule="evenodd"` so the tile shows through rather
than being painted a third colour.

Pass `decorative` whenever a visible "FoundU" wordmark sits next to it, so screen readers do
not hear the name twice. The same artwork is `web/public/favicon.svg`.

### Typography

**Geist Variable**, loaded from `@fontsource-variable/geist` and applied globally through
`font-sans`. Don't import another font.

| Role | Classes |
|---|---|
| Hero heading | `text-4xl font-semibold tracking-tight text-balance sm:text-6xl lg:text-7xl` |
| Page heading | `text-2xl font-semibold tracking-tight` |
| Section heading | `text-3xl font-semibold tracking-tight text-balance sm:text-4xl` |
| Card title | `text-base font-medium` |
| Body | `text-sm` (`text-base` on marketing pages) |
| Secondary | `text-sm text-muted-foreground` |
| Eyebrow | `text-sm font-medium text-brand-green` |

Use `text-balance` on headings and `text-pretty` on paragraphs — both cost nothing and stop
ragged last lines.

---

## 3. Design tokens

All colours are CSS custom properties defined in [`web/src/index.css`](../web/src/index.css),
in `oklch()`. Light values sit on `:root`, dark on `.dark`. Tailwind exposes each one as a
utility through the `@theme inline` block.

| Token | Utility | Use for |
|---|---|---|
| `--background` / `--foreground` | `bg-background` `text-foreground` | Page surface and body text |
| `--card` / `--card-foreground` | `bg-card` `text-card-foreground` | Cards, panels |
| `--primary` / `--primary-foreground` | `bg-primary` `text-primary-foreground` | Primary actions |
| `--secondary` | `bg-secondary` | Secondary actions |
| `--muted` / `--muted-foreground` | `bg-muted` `text-muted-foreground` | Subdued surfaces, helper text |
| `--accent` | `bg-accent` | Hover/active states |
| `--destructive` | `bg-destructive` `text-destructive` | Delete, reject, validation errors |
| `--border` / `--input` / `--ring` | `border-border` `ring-ring` | Borders and focus rings |
| `--sidebar-*` | `bg-sidebar` etc. | Sidebar chrome only |
| `--radius` | `rounded-lg` etc. | Corner radius scale |

**Why it matters:** dark mode, and any future theming, works automatically if you use tokens
and breaks silently if you don't. `text-gray-500` stays dark grey on a dark background.

```tsx
// Good
<p className="text-sm text-muted-foreground">No items yet</p>
<Button variant="destructive">Reject claim</Button>

// Bad
<p className="text-sm text-gray-500">No items yet</p>
<button className="bg-red-600 text-white px-4 py-2 rounded">Reject claim</button>
```

**Spacing:** stick to Tailwind's scale (`gap-2`, `p-4`, `p-6`). Page content is `p-6`;
stacked sections use `flex flex-col gap-4`. No arbitrary values like `p-[13px]`.

---

## 4. Components

Components live in `web/src/components/ui/` and are **copied into our repo**, not imported from
a package. They are ours to edit — but edit deliberately, since `shadcn add --overwrite` will
replace them.

Add one with:

```bash
cd web
npx shadcn@latest add <component>     # e.g. table, dialog, select, tabs
```

Already installed: `avatar` `badge` `button` `card` `dropdown-menu` `input` `label` `separator`
`sheet` `sidebar` `skeleton` `sonner` `tooltip`.

### ⚠️ Base UI is not Radix — three traps

Our shadcn style is built on **Base UI**. Almost every shadcn tutorial online is written
against Radix, and these three differences will bite you. Two of them compile fine and only
fail at runtime.

**1. Composition uses `render`, not `asChild`.**

```tsx
<Button variant="outline" render={<Link to="/" />}>Back</Button>   // correct
<Button asChild><Link to="/">Back</Link></Button>                  // will not compile
```

**2. A button rendering a link needs `nativeButton={false}`.**
`Button` asserts a real `<button>`; rendering an `<a>` without this logs an accessibility
error on every render.

```tsx
<Button nativeButton={false} render={<Link to="/login" />}>Sign in</Button>
```

**3. Group labels must sit inside their group.**
A bare `DropdownMenuLabel` throws `MenuGroupContext is missing` and takes the whole subtree
down with it.

```tsx
<DropdownMenuGroup>
  <DropdownMenuLabel>Signed in as…</DropdownMenuLabel>
</DropdownMenuGroup>
```

**Watch the browser console, not just the build.** All three of these passed `tsc` and `vite
build`; only the console showed them.

### Class merging

Use `cn()` from [`@/lib/utils`](../web/src/lib/utils.ts) whenever you combine conditional
classes — it makes later classes win over earlier conflicting ones.

```tsx
<div className={cn('rounded-md p-4', isActive && 'bg-accent', className)} />
```

### Icons

`lucide-react` only, at default size (`size-4` inside buttons). Decorative icons need
`aria-hidden="true"`; an icon-only button needs an accessible name.

```tsx
<Button size="icon" aria-label="Delete item"><TrashIcon aria-hidden="true" /></Button>
```

---

## 5. Motion

Animation is opt-in through classes defined in [`web/src/index.css`](../web/src/index.css).
Don't write bespoke keyframes in components.

| Class | Effect | Use for |
|---|---|---|
| `fu-reveal` + `is-visible` | Fades and rises into place | Sections scrolling into view |
| `fu-draw` + `is-visible` | Strokes draw themselves in | Monoline illustrations |
| `animate-fu-float` | Slow vertical drift | Hero artwork |
| `fu-aurora-a/b/c` | Drifting gradient blobs | Hero background only |
| `fu-flow` | Dashes travelling along a path | Hero connector lines |
| `fu-ping` | Expanding ring | Live indicators |
| `fu-swap-in` | Fades up on content change | Rotating ticker entries |

Trigger the scroll-based ones with [`useInView`](../web/src/hooks/use-in-view.ts), which fires
once and then stops observing — re-animating on every scroll pass is distracting.

```tsx
const { ref, isVisible } = useInView<HTMLDivElement>()
<div ref={ref} className={cn('fu-reveal', isVisible && 'is-visible')}>…</div>
```

Stagger with `style={{ '--fu-delay': '120ms' }}`, roughly 80–120ms between siblings.

**Every one of these is disabled under `prefers-reduced-motion`**, with revealed elements
falling back to visible rather than staying hidden. If you add an animation, add it to that
media query too. `useInView` also falls back to visible where `IntersectionObserver` is
missing — content must never be permanently invisible because an effect did not run.

---

## 6. Folder structure

Feature-first. A feature owns its components, hooks and API calls.

```
web/src/
  components/
    layout/        app shell (sidebar, header)
    ui/            shadcn primitives - do not put app logic here
  features/
    auth/          context, provider, hook, api, pages
    <feature>/     same shape per slice (items, claims, notifications, admin)
  lib/
    api/           transport: types, tokens, client - no React imports
    utils.ts       cn()
  pages/           simple standalone screens (404, forbidden, placeholders)
  routes/          router table and guards
  hooks/           cross-feature hooks only
```

**Naming:** files `kebab-case.tsx`, components `PascalCase`, hooks `useThing`, types
`PascalCase`. One primary export per file.

`lib/api` must stay React-free so it can be tested without a renderer.

---

## 7. Data fetching

TanStack Query for every server read. Never `useEffect` + `fetch`.

```tsx
const { data, isLoading, isError, error } = useQuery({
  queryKey: ['items', { page }],
  queryFn: () => api.get<PagedResult<Item>>(`/api/items?page=${page}`),
})
```

- **Query keys** are arrays, most general first: `['items']`, `['items', id]`, `['items', { page }]`.
- **Mutations** invalidate what they changed:
  `onSuccess: () => queryClient.invalidateQueries({ queryKey: ['items'] })`
- Retries are configured globally in [`App.tsx`](../web/src/App.tsx): 4xx never retries
  (a 403 will not become a 200), 5xx retries twice.

All requests go through [`@/lib/api/client`](../web/src/lib/api/client.ts), which attaches the
JWT, refreshes once on 401, and throws `ApiError`. Don't call `fetch` directly.

---

## 8. The three states — required on every screen

```tsx
if (isLoading) return <Skeleton className="h-32 w-full" />          // shape of real content
if (isError)   return <ErrorState error={error} onRetry={refetch} /> // message + retry
if (!data?.items.length) return <EmptyState … />                     // what it is + next action
```

- **Loading** — use `<Skeleton>` matching the real layout, not a centred spinner.
- **Empty** — say what would appear here and give the action that creates it. Never a bare
  "No data".
- **Error** — show `error.message` (the API's `ProblemDetails.detail` is written to be
  client-safe) plus a retry control. Never a raw stack trace.

---

## 9. Forms and validation

The API returns field errors in the `ProblemDetails.errors` dictionary, keyed by **PascalCase**
field name (`Email`, `Password`) — matching the C# DTO property, not the JSON camelCase.

```tsx
catch (error) {
  if (error instanceof ApiError) {
    setFieldErrors(error.fieldErrors)          // Record<string, string[]>
    toast.error(error.status === 401 ? 'Incorrect email or password.' : error.message)
  }
}
```

Bind each error to its input — see [`login-page.tsx`](../web/src/features/auth/login-page.tsx)
for the reference implementation:

```tsx
<Input id="email" aria-invalid={Boolean(fieldErrors.Email)}
       aria-describedby={fieldErrors.Email ? 'email-error' : undefined} />
{fieldErrors.Email && <p id="email-error" className="text-sm text-destructive">…</p>}
```

Disable the submit button while submitting and show a spinner. Field-level errors go
**next to the field**; only whole-request failures become toasts.

---

## 10. Toasts

`sonner`, via `toast` from `sonner`. Mounted once in `App.tsx`.

```tsx
toast.success('Claim approved')
toast.error('Could not reach the server.')
```

Use for the outcome of an action the user just took. Not for validation errors (those belong on
the field) and not for routine page loads.

---

## 11. Accessibility

Non-negotiable, and part of the A4 definition of done:

- **Keyboard** — everything reachable and operable by Tab/Enter/Escape. Never `onClick` on a
  `<div>`; use `<button>` or `<Link>`.
- **Focus** — never remove focus rings. The tokens already provide `focus-visible:ring-ring/50`.
- **Labels** — every input has a `<Label htmlFor>`; icon-only buttons have `aria-label`.
- **Contrast** — 4.5:1 minimum for body text. The tokens satisfy this; hand-picked colours may not.
- **Live regions** — async status messages need `aria-live="polite"`.
- **Landmarks** — one `<main>` per page, `<nav aria-label="…">` for navigation.
- **Images** — meaningful ones need real `alt`; decorative ones `alt=""`.

Quick check: unplug your mouse and complete the flow.

---

## 12. Routing and roles

Routes are declared in [`routes/router.tsx`](../web/src/routes/router.tsx). Wrap role-restricted
branches in `<ProtectedRoute allow={['Staff', 'Admin']} />`.

> **The client guard is usability, not security.** Role comes from `localStorage` and a user can
> edit it. The API's authorization policies are the real enforcement point. Always assume a
> screen can be reached by someone who shouldn't see it, and let the API return 403.

Sidebar nav items are filtered by role in
[`app-layout.tsx`](../web/src/components/layout/app-layout.tsx) — add new screens to `NAV_ITEMS`
with the roles that may see them.

---

## 13. Project setup gotchas

- **The `@/` alias must be declared twice** — in `tsconfig.app.json` (for the compiler) *and*
  `tsconfig.json` (for the shadcn CLI). Without the root one, `shadcn add` silently writes
  components into a literal `./@` folder.
- **No `baseUrl`** in `tsconfig.app.json` — TypeScript 6 deprecates it and resolves `paths`
  relative to the file. The root `tsconfig.json` keeps it only for the CLI.
- **Tailwind v4 has no config file.** No `tailwind.config.js`, no PostCSS setup — just
  `@import "tailwindcss"` plus the `@theme` block in `index.css`. Ignore v3 tutorials.
- **`.env.local`** holds `VITE_API_BASE_URL` and is gitignored. Copy it from `.env.example`.

---

## 14. Before you open a PR

- [ ] `npm run build` passes (runs `tsc -b` too)
- [ ] `npm run lint` clean, apart from known shadcn `only-export-components` warnings
- [ ] Loading, empty and error states present on every list/form/detail
- [ ] Keyboard-only pass completed
- [ ] No hardcoded colours, no `../../` imports
- [ ] Checked in both light and dark mode
