# Frontend is organised in feature slices with lint-enforced one-way dependencies

`src/` is split by feature slice named after glossary concepts (`features/requests`, `features/credits`, `features/members`, `features/auth`), not by technical layer. Layers (`components/`, `hooks/`, `api/`) make one feature change fan out across many folders, which is costly for agents; a slice keeps a change local and makes boundaries lintable.

Dependencies run one way: `routes → features → shared` (`components/ui`, `components/`, `hooks/`, `lib/`). Routes (TanStack Router, file-based, in `src/routes/`) stay thin: params, search params, loader prefetch, and one page component imported from a feature. Features never import from `routes/`, never import another feature's internals, and cross-feature use goes through the other feature's `index.ts`. ESLint enforces this (`eslint-plugin-boundaries` or `import/no-restricted-paths`) with zero warnings, plus a fixture test so the rule cannot silently stop working.

Other rules:

- `components/ui/` is vanilla shadcn output, edited only for theme tokens, so `shadcn add` and upgrades stay painless. Composed app-level components live in `components/`; feature components and hooks live in their slice; only genuinely cross-feature hooks go in `hooks/`.
- API: one thin typed `fetch` wrapper in `lib/api/` (base URL, JSON, ProblemDetails into a typed `ApiError`). Each feature has an `api.ts` with its TanStack Query hooks and query-key factories. DTO types are generated from the backend OpenAPI document (generator chosen in the REST contract ticket). Zod is for forms only, not for re-validating responses.
- State: server state only in TanStack Query, shareable UI state in the URL, local state in `useState`. No `useEffect` for data fetching or derived state.
- Tests: colocated `Foo.test.tsx`; MSW handlers per feature in `features/<x>/mocks.ts` with a shared server and render helper in `src/test/` (QueryClient with retries off, router); accessible queries (`getByRole`, `getByLabelText`) rather than test ids; happy path plus main failure per feature; no snapshots, no coverage threshold.
