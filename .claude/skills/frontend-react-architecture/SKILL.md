---
name: frontend-react-architecture
description: Architecture, patterns, and conventions for the React + TypeScript frontend at My-Tarot-Reader/Frontend. Use when writing, reviewing, or editing frontend code.
metadata:
  project: My-Tarot-Reader
  stack: React 19, TypeScript 6, Vite 8, Ant Design 6, TanStack Query 5, Zustand 5, i18next 17, react-router-dom 7
---

# Frontend Architecture — My Tarot Reader

## Project structure

```
src/
├── api/           — Axios client + API URL constants (non-React)
├── assets/        — Static images (tarot cards for future feature)
├── components/    — Shared UI components
│   ├── home/      — HomePage hero section
│   └── layouts/MainLayout/ — Header, Footer, MobileDrawer, CoinBadge, UserDropdown
├── constants/     — App-wide constants (theme palettes, colors, language modes, events)
├── hooks/
│   ├── api/       — React Query hooks (useLogin, useLogout, useGetCurrentUser)
│   ├── custom/    — Custom hooks (useDocumentTitle)
│   └── stores/    — Zustand stores (language, theme)
├── i18n/          — i18next setup + locale files (vi, en)
├── pages/
│   ├── auth/      — Protected (authenticated) pages
│   └── guest/     — Public pages (home, login)
├── routes/        — React Router v7 config + route guards
│   └── components/ — ProtectedRoute, PublicRoute, SessionExpiredHandler, RouteTitle
├── types/         — TypeScript types + DTOs + enums
└── utils/         — Utility functions
```

## Key architectural decisions

### State Management

- **Server state**: TanStack Query (React Query) v5 for all API data. Single global QueryClient created at module scope in `App.tsx` (not inside component).
- **Client state**: Zustand with `persist` middleware for language and theme preferences. Store pattern: `useLanguageStore`, `useThemeStore`.
- **Auth state**: Derived from `useGetCurrentUser()` query (`["auth"]` key). No separate auth context — `ProtectedRoute` gates on this query.

### Authentication Flow

- **HttpOnly cookies** for accessToken/refreshToken (no client-side token storage).
- **Google OAuth**: `@react-oauth/google` `<GoogleOAuthProvider>` wraps entire app in `App.tsx`.
- **Axios interceptor** (`config.api.ts`): handles 401 → refresh token flow with request queuing (`isRefreshing` + `failedQueue` pattern).
- **Session expiration**: dispatched as a `window` event (`auth:session-expired`) from the axios interceptor when refresh fails. `SessionExpiredHandler` component (inside `BrowserRouter`) listens and does `queryClient.removeQueries({ queryKey: ["auth"] })` + `navigate("/guest", { replace })` client-side — never uses `window.location.href` (avoids full SPA reload).
- **Logout scope**: `useLogout` calls `queryClient.removeQueries({ queryKey: ["auth"] })` — only clears auth-scoped cache, not all queries.
- **Login freshness**: `useLogin` calls `queryClient.invalidateQueries({ queryKey: ["auth"] })` on success to avoid stale previous-user data.

### Routing

- **react-router-dom v7** with `BrowserRouter` in `AppRouter.tsx`.
- **Lazy loading** via `React.lazy()` for all page components.
- **Route guards**: `PublicRoute` (wraps in `MainLayout`, renders via `<Outlet />`), `ProtectedRoute` (redirects unauthenticated users to `/guest`).
- **`SessionExpiredHandler`** is a render-`null` component mounted inside `<BrowserRouter>` before `<Suspense>` to listen for auth events.

### Styling & Theming

- **Ant Design v6** with `ConfigProvider` + custom theme per user role (`getThemeByRole` in `theme.constants.ts`).
- **No CSS modules / Tailwind** — inline styles + AntD token system.
- **Font**: `Cormorant Garamond` is the primary font, set in `theme.constants.ts` (`FONT_BODY`) as `token.fontFamily` for all AntD components. `index.css` body rule mirrors the same stack for non-AntD elements.
- **3 palettes** defined in `COLOR_PALETTES`: `guest` (purple), `registered` (purple), `pro` (gold). Each has `dark`/`light` surface variants.

### API Layer

- **Axios client** (`config.api.ts`) with:
  - Response interceptor unwraps `response.data.data` (all callers receive the unwrapped payload directly).
  - Request interceptor attaches `X-Device-Id` via FingerprintJS (best-effort, cached, non-blocking).
  - Auth whitelist: `login`, `refresh`, `logout` endpoints skip the 401 retry logic.
- **API URL constants** in `url.api.ts` (`as const`).
- **React Query hooks** in `hooks/api/` wrap all mutations/queries.

## Conventions

### File naming

- **Components**: `PascalCase.tsx` (e.g. `HeroSection.tsx`, `LayoutFooter.tsx`)
- **Hooks**: `camelCase.hooks.ts` (e.g. `auth.hooks.ts`, `useDocumentTitle.hooks.ts`)
- **Constants**: `camelCase.constants.ts` (e.g. `theme.constants.ts`)
- **Types**: `camelCase.type.ts` (e.g. `enums.type.ts`)
- **API**: `camelCase,api.ts` with comma (existing pattern in `auth,api.ts`) or `camelCase.api.ts`

### Barrel exports

- Each directory has an `index.ts` barrel. Components use `export { default as Name } from "./Name"` pattern.
- `src/components/index.ts` re-exports from `./layouts` and `./home`.
- `src/hooks/api/index.ts` re-exports hooks.

### Query keys

- Defined in `hooks/api/queryKey.ts` as `readonly` tuples.
- **Partial prefix matching**: TanStack v5 `removeQueries`/`invalidateQueries` match by prefix — `["auth"]` removes all queries starting with `"auth"`.
- Pattern: `export const AUTH_QUERY_KEY = ["auth"] as const;`

### TypeScript

- `verbatimModuleSyntax: true` — use `import type` for type-only imports.
- `noUnusedLocals: true` — no unused imports allowed (build fails).
- Path alias: `@/*` → `./src/*`.

### Constants

- `APP_NAME`, `AUTH_SESSION_EXPIRED_EVENT`, `COIN_COLORS` in `common.constants.ts`.
- `THEME_MODE`, `COLOR_PALETTES`, `FONT_BODY`, `getThemeByRole`, `getPaletteByRole` in `theme.constants.ts`.
- `LANGUAGE_MODE` in `language.constants.ts`.
- Constants barrel-exported via `constants/index.ts`.

### Events for cross-layer communication

- For non-React modules (e.g. axios interceptors) to communicate with React components, use `window.dispatchEvent(new Event(EVENT_NAME))` with event constants defined in `@/constants`.
- React components listen via `useEffect` + `addEventListener`, cleanup on unmount.
- **Never** use `window.location.href` for in-app navigation from non-React code — dispatch an event and let a React handler navigate via `useNavigate`.

## Common gotchas

1. **AntD v6 CSS-in-JS**: AntD does NOT set `body` font-family globally. `ConfigProvider` only applies `token.fontFamily` to AntD components. For consistent fonts, `index.css` body rule must match `FONT_BODY` in theme.constants.ts.
2. **`queryClient.clear()` is nuclear**: Use `removeQueries({ queryKey: [...] })` to scope cache wipes. `clear()` wipes ALL query data including unrelated cached data.
3. **`invalidateQueries` vs `removeQueries`**: Use `invalidateQueries` when you want to refetch active queries (e.g. login success). Use `removeQueries` when you want to evict without refetching (e.g. session expiration / logout).
4. **`window.location.href` in axios interceptor**: Causes full SPA reload, losing zustand state and React context. Use event-based communication with React Router instead.
5. **FingerprintJS blocking render**: `getVisitorId()` is async and can be slow. Never gate rendering on it — warm it up in background, let the request interceptor handle header attachment best-effort.
6. **`<Suspense>` boundary**: Page components are lazy-loaded. Route fallback wraps in `<MainLayout><Spin fullscreen /></MainLayout>` so the layout stays stable during navigation.
7. **Google Fonts**: Only load fonts actually used in code. Remove unused font families from the `<link>` URL to avoid unnecessary network requests.
