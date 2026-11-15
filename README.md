# Pantrio

A shared household meal planner. Add a dish (AI drafts its ingredients, you edit and save them once), fill the week, and the shopping list builds itself: quantities scaled, duplicates merged, grouped by aisle.

- **Backend:** .NET 10 minimal API, EF Core + PostgreSQL 18, Google sign-in with cookie sessions, OpenAI for ingredient drafts.
- **Frontend:** Next.js 16, Tailwind 4, shadcn/ui, TanStack Query, a typed client generated from the API's OpenAPI document.
- **Hosting:** frontend on Vercel; API, PostgreSQL and a Caddy edge on a small VPS. See [deploy/README.md](deploy/README.md).

## Run it locally

Needs Docker. Nothing else has to be installed on the host for the app itself.

```bash
cp .env.example .env      # optional: Google and OpenAI keys; without them you get a dev sign-in and a fake AI
docker compose up -d
```

Open http://localhost:3000 and use the development sign-in.

Tests need the .NET 10 SDK and Docker (integration tests start their own PostgreSQL):

```bash
cd backend && dotnet test
```

More commands and conventions: [CLAUDE.md](CLAUDE.md). Product brief: [docs/PANTRIO_CONTEXT.md](docs/PANTRIO_CONTEXT.md).
