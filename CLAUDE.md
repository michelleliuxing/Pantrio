# Pantrio

Shared household meal planner: plan the week, the shopping list builds itself. Product brief and Stage 1 scope: `docs/PANTRIO_CONTEXT.md`.

## Layout

- `backend/` .NET 10 solution (`Pantrio.slnx`)
  - `src/Pantrio.Domain` pure logic: units, scaling, merging, list rebuild planning. No EF Core, no HTTP.
  - `src/Pantrio.Infrastructure` EF Core (Npgsql, snake_case), migrations, seed data, OpenAI client.
  - `src/Pantrio.Api` minimal API endpoints, auth, rate limits. OpenAPI doc written to `backend/openapi/pantrio.json` on every build.
  - `tests/Pantrio.Domain.Tests` unit tests; `tests/Pantrio.Api.Tests` integration tests against real PostgreSQL via Testcontainers.
- `frontend/` Next.js 16 (App Router, client components), Tailwind 4, shadcn/ui (base-nova), TanStack Query. Deployed to Vercel.
- `deploy/` production compose, Caddy edge config, backup scripts.

## Commands

Local stack (db + api with `dotnet watch` + Next dev server), all in Docker:

```bash
docker compose up -d          # web http://localhost:3000, api :5080, db 127.0.0.1:5433
docker compose logs -f api
docker compose down           # add -v to wipe the database
```

Host Node is too old for Next 16; run frontend tooling in the container:

```bash
docker compose run --rm --no-deps web npm run lint
docker compose run --rm --no-deps web npm run typecheck
docker compose run --rm --no-deps web npm run gen:api   # after any API change
docker compose run --rm --no-deps web npx shadcn@latest add <component>
```

Backend (host .NET 10 SDK; Docker must be running for integration tests):

```bash
cd backend && dotnet build && dotnet test
cd backend && dotnet tool restore && dotnet ef migrations add <Name> -p src/Pantrio.Infrastructure -s src/Pantrio.Api
```

## Conventions

- Next.js 16 differs from older versions: read `frontend/node_modules/next/dist/docs/` before using a Next API. Cache Components is on, so client components that read `useSearchParams`/`useParams` need a `<Suspense>` boundary.
- The browser only talks to the frontend origin. `next.config.ts` rewrites `/api/*` to `API_ORIGIN`. Never call the API host directly from the browser.
- Frontend types come only from `src/lib/api/schema.d.ts` (generated). Never hand-write DTOs. After changing an endpoint: `dotnet build` (rewrites `openapi/pantrio.json`), then `npm run gen:api`, and commit both. CI fails if either is stale.
- Every unsafe API request must send `X-Pantrio-Csrf: 1` (the shared client does).
- All user data is household-scoped: entities implement `IHouseholdScoped`; a global query filter and a save interceptor enforce it. Never filter by household by hand in endpoints.
- Quantities are `decimal` / `numeric`, never floating point. Store exact values, round only for display.
- Pin ASP.NET Core / EF packages to the 10.0.x line (NuGet's latest is .NET 11).
- Never deploy to or change the VPS without the owner's explicit go-ahead.
