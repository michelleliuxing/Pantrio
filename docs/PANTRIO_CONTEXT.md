# Pantrio: project context for Stage 1

This file briefs a Claude Code session on Pantrio so it can plan and then build Stage 1. It records what the product is, what has already been decided, what Stage 1 must deliver, and where there are still choices to make.

## 1. How to use this file

1. Read the whole file before proposing anything.
2. Raise the open questions in section 16 (and any others you find) with the owner.
3. Produce a written plan: architecture, project layout, schema, and a milestone breakdown. Wait for the owner's approval before writing code.
4. Build milestone by milestone. Each milestone ends with tests passing and an app that runs locally.
5. Once the repo is scaffolded, write a short `CLAUDE.md` with the build, test and run commands and the conventions you settled on. Keep this file as `docs/PANTRIO_CONTEXT.md`.

Items marked **Decided** are settled; do not reopen them without a strong reason. Items marked **Suggested** are a starting point; change them if you find something better and say why.

The owner is a software engineer building this solo as a side project. Explanations can be technical. Library versions and APIs move quickly, so check current documentation for .NET, EF Core, shadcn/ui, Tailwind and the AI provider rather than relying on memory.

## 2. What Pantrio is

Pantrio is a shared household meal planner. A family plans the week's meals together and the grocery list builds itself.

The problem: deciding what to cook every day is tiring, family members plan in their heads or in group chats, ingredients get forgotten, and things already at home get bought twice.

The core loop:

1. Add a dish (a name is enough). AI drafts its ingredients.
2. Edit and save the ingredients once. They are reused every time the dish is picked.
3. Pick dishes and fill the week (breakfast, lunch and dinner).
4. The shopping list builds itself: quantities scaled, duplicates merged, items grouped by aisle, plus extras like milk and cleaning supplies added by hand.
5. Shop from the list on a phone, ticking items off.

The focus is weekly planning and the grocery list. Recipes exist to feed the list. Cooking instructions are optional and secondary.

The AI is called only when a recipe's ingredients are drafted or regenerated. Picking a saved dish reads from the database: instant and free.

## 3. Product decisions already made

| Topic | Decision |
| --- | --- |
| Name | Pantrio |
| Users | One household per account group. The owner plus invited family members share recipes, plans, the list and (later) the inventory |
| Roles | Owner, editor, viewer. Editors may delete recipes and inventory items only if the owner grants a "can delete" permission |
| Meals | Breakfast, lunch and dinner slots for each day. A slot can hold several dishes. Dishes can also sit unassigned in a "meal cart" |
| Shopping list | One live list per household. Holds recipe-derived items and free-typed extras with no recipe |
| Inventory (Stage 2) | Quantity for countable items; status (Plenty, Low, Out) for sauces and spices |
| Inventory deduction (Stage 2) | Proposed automatically once a planned meal's day has passed; a household member confirms before it applies |
| Cooking method | Optional. None, your own text, or (later) an AI-generated default |
| Platform | Web app, responsive, used on a phone in the supermarket. A mobile app may follow later on the same API |
| Locale | Australia: metric units, Australian ingredient names (capsicum, coriander, spring onion), 20 ml tablespoon |
| Cost | Keep hosting cost near zero. Self-host on the owner's existing VPS |
| Future | May open to the public later. Nothing in Stage 1 should make that hard |

## 4. Stage 1 scope

Stage 1 is "plan and shop": one person can sign in via Google SSO (potential other Sign in methods to be added in the future), build a recipe library with AI-drafted ingredients, plan a week, and shop from a merged list. The goal that ends Stage 1 is that a week can be planned and its list produced in under 5 minutes.

### In scope

- **Accounts.** Sign up, sign in, sign out. Signing up creates a household with the user as owner.
- **Recipe library.** Create, edit, duplicate, delete, search, tag and favourite recipes. Fields: name, default servings, tags, prep time, notes, optional method text, source URL.
- **AI ingredient suggestions.** From a dish name, servings and optional notes, return an editable draft list. Regenerate all, or ask for more items.
- **Editable ingredients.** Inline edit of name, quantity, unit and aisle category; add, delete, reorder; mark optional or staple. Saved ingredients are the source of truth.
- **Ingredient catalogue.** Recipe ingredients link to catalogue entries so the same ingredient merges across recipes.
- **Meal planner.** Week view of 7 days by breakfast, lunch and dinner, plus a meal cart of unassigned dishes. Per-entry servings override. Move between weeks.
- **Shopping list.** Build from a week's plan. Scale, merge, group by aisle. Add extras. Tick off. Show which dishes each item comes from. Rebuilding keeps extras and ticks.
- **Deployment.** Running on the owner's VPS with HTTPS and nightly database backups.

### Nice to have in Stage 1, only if cheap

- Copy last week's plan.
- Hide a meal row the household never plans (for example breakfast).
- Autocomplete extras from previously added extras.

### Out of scope (later stages, see section 17)

Family invites and shared live editing, inventory, recipe import from web pages or videos, AI auto-planning, "decide for me", cooking log, AI-generated cooking methods, offline support, native mobile app.

### Design for later without building it

The schema is household-scoped from day one. Every table that holds user data carries `household_id`, and membership has a role and a `can_delete` flag, even though Stage 1 has only one member per household. This keeps Stage 2 (family sharing) an additive change.

## 5. Tech stack

### Decided

- **Database:** PostgreSQL.
- **Backend:** .NET (ASP.NET Core Web API). Caddy. 
- **Frontend:** TypeScript, NextJS.
- **UI components:** shadcn/ui.

### Suggested

- **.NET version:** .NET 10 (the current LTS release).
- **Data access:** EF Core with the Npgsql provider, code-first migrations, snake_case naming in the database.
- **Auth:** ASP.NET Core Identity with cookie sessions. The SPA and API share one origin in production, so cookies stay simple and no tokens live in browser storage.
- **API contract:** generate an OpenAPI document from the API and generate the TypeScript client and types from it, so the frontend never hand-writes DTOs.
- **Frontend:** React with Vite, Tailwind CSS, shadcn/ui, TanStack Query for server state, a client-side router, react-hook-form with zod for forms. (shadcn/ui implies React; confirm with the owner.)
- **Serving the SPA:** in production the API container serves the built frontend as static files, so there is one container and one origin. In development, Vite proxies `/api` to the backend.
- **AI provider:** Claude through the Anthropic API, behind an interface so it can be swapped or faked. A small, fast model is enough. Check the provider's current docs for the SDK and model ID; keep the model ID in configuration.
- **Tests:** xUnit. Integration tests run against a real PostgreSQL (for example via Testcontainers), not an in-memory substitute.
- **Local dev:** Docker Compose for PostgreSQL; backend and frontend run on the host.
- **IDs and time:** UUID primary keys (time-ordered if convenient). Timestamps stored as `timestamptz` in UTC. Plan days are calendar dates, not timestamps. Quantities are `numeric`, never floating point.

### Suggested repository layout

```
pantrio/
  backend/
    src/Pantrio.Api/             ASP.NET Core host: endpoints, auth, DI
    src/Pantrio.Domain/          entities and pure logic: units, scaling, merging
    src/Pantrio.Infrastructure/  EF Core, migrations, AI client
    tests/Pantrio.Domain.Tests/
    tests/Pantrio.Api.Tests/     integration tests against PostgreSQL
  frontend/                      Vite + NextJS + TypeScript + Tailwind + shadcn/ui
  deploy/                        production compose file, proxy config, backup script
  docker-compose.yml             local PostgreSQL
  docs/PANTRIO_CONTEXT.md        this file
  CLAUDE.md                      commands and conventions (write after scaffolding)
```

Simplify the project split if you prefer. The one firm requirement is that unit conversion, scaling and merging live in code with no EF Core or HTTP dependencies, so they can be unit-tested exhaustively.

## 6. Hosting constraints

Pantrio will run on the owner's existing VPS. These limits shape the build.

- **Server:** Vultr, Sydney, 1 vCPU, 1 GB RAM, 25 GB SSD, Ubuntu 22.04.
- **It is shared.** Another small production app ("pawfectbite") already runs there. Do not break it. Its reverse proxy and port setup are unknown to this file: ask the owner or inspect before proposing any proxy configuration. If something already listens on ports 80 and 443, Pantrio is added to it rather than replacing it.
- **Memory budget (suggested):** PostgreSQL at most about 256 MB, the API at most about 256 MB, each with a container memory limit. Configure PostgreSQL for a small machine (modest `shared_buffers`, low `max_connections`). Run the API with workstation garbage collection rather than server GC.
- **Do not build on the server.** Build container images in CI or locally, push them to a registry, and have the server only pull and run them.
- **Packaging:** Docker Compose, all settings through environment variables, no secrets in the repository. PostgreSQL is never exposed to the internet.
- **Backups:** a nightly `pg_dump` copied off the server. Include a documented restore procedure and test it once.
- **Portability:** the same images and a database dump must be enough to move to a bigger server or a managed PostgreSQL service later. No state on the server's disk other than the database volume.

Owner's server tasks, outside this build but worth a reminder in the deployment milestone: enable provider backups, switch SSH to keys only, add a firewall (22, 80, 443), add a swap file.

**Never deploy to or change the server without the owner's explicit go-ahead.**

## 7. Domain model

A starting schema. Names are suggestions; the relationships matter.

| Table | Key fields | Notes |
| --- | --- | --- |
| household | id, name, timezone (default Australia/Sydney), dietary_prefs (json), hidden_meal_slots | Root of all user data |
| user | id, email, display_name | Identity user |
| household_member | household_id, user_id, role (owner, editor, viewer), can_delete | One row per user in Stage 1 |
| ingredient | id, household_id, name, aliases[], default_unit, category, is_staple | The catalogue. See open question on scoping |
| recipe | id, household_id, name, servings, tags[], prep_mins, notes, method_text, method_source (none, custom, ai), source_url, is_favourite, created_by | |
| recipe_ingredient | id, recipe_id, ingredient_id, quantity, unit, quantity_text, optional, note, sort_order | quantity is null when the amount is vague |
| meal_plan | id, household_id, week_start (date, Monday) | One per household per week, created on first use |
| meal_plan_entry | id, meal_plan_id, recipe_id, day (null = in cart), meal_slot (breakfast, lunch, dinner; null in cart), servings_override, added_by | |
| shopping_list_item | id, household_id, ingredient_id (null for extras), name, dimension, unit_label, category, state (needed, bought, have_it), is_extra, extra_quantity_text, added_by, checked_by | One row per ingredient per dimension |
| list_item_source | id, list_item_id, meal_plan_entry_id, quantity (base unit), quantity_text | Provenance: one row per plan entry contributing to an item |

Stage 2 will add `invite`, `inventory_item` and `activity`. Do not create them now, but do not choose names or structures that collide with them.

### Aisle categories (suggested fixed list)

Produce; meat and seafood; dairy and eggs; bakery; pantry; sauces and condiments; spices; international; frozen; drinks; snacks; household; other.

## 8. Core domain rules

This is the part of the app most likely to have subtle bugs. Treat these as requirements and cover each with unit tests.

### Units and dimensions

| Dimension | Units accepted | Base unit |
| --- | --- | --- |
| Mass | g, kg | g |
| Volume | ml, l, tsp, tbsp, cup | ml |
| Count | each, plus labelled counts such as bunch, clove, can, pack, slice | the label itself |
| Text | no unit; a phrase such as "to taste" or "a pinch" | none |

- Australian measures: 1 tsp = 5 ml, 1 tbsp = 20 ml, 1 cup = 250 ml.
- Never convert between mass and volume. There is no density table.
- Counts add only when the label matches: 2 cloves + 3 cloves = 5 cloves, but cloves and bunches stay separate.
- Text amounts are never summed. They are listed as written, de-duplicated.

### Scaling

- Factor = (entry's servings override, else the recipe's servings) divided by the recipe's servings.
- Scale mass, volume and count quantities. Leave text amounts alone.
- Store exact scaled values. Round only for display.

### Merging into the list

- Two contributions merge when they have the same catalogue ingredient and the same dimension (and, for counts, the same label).
- The same ingredient in two dimensions gives two list lines, for example "200 g flour" and "250 ml flour".
- Optional recipe ingredients are left off the list by default.
- Staple ingredients (salt, oil) appear in a separate "Check you have" group instead of the to-buy groups. The user can move one to "needed".

### Display (suggested)

- 1000 g and above shows as kg; 1000 ml and above shows as L.
- "Each" counts round up to a whole number when shown on the shopping list.
- Each list line shows its total and, on expand, the dishes it comes from.

### Building and rebuilding the list

- "Build shopping list" takes a week's plan entries (assigned and in the cart) and writes one `list_item_source` row per entry per ingredient, in base units.
- An item's total is the sum of its source rows. Derive it; do not store a second copy that can drift.
- Rebuilding is a diff, not a wipe. Sources for entries still in the plan are updated, sources for removed entries are deleted, and a recipe-derived item with no sources left is removed.
- Extras are never touched by a rebuild.
- An item's state (needed, bought, have_it) survives a rebuild.
- The list is a snapshot of recipes at build time. Editing a recipe afterwards does not change the list until the user rebuilds.

### Worked examples (use as test cases)

| Situation | Expected result |
| --- | --- |
| Dish A needs 1 brown onion, dish B needs 2 | One line: 3 brown onions, sources A and B |
| 2 tbsp soy sauce + 100 ml soy sauce | One line: 140 ml soy sauce |
| 200 g flour + 1 cup flour | Two lines: 200 g flour; 250 ml flour |
| Recipe for 2 servings, entry overridden to 4, needs 400 g chicken | 800 g chicken |
| Remove dish B from the plan and rebuild | Onions drop to 1; nothing else changes |
| "Milk" added as an extra, then rebuild | Milk is still there |
| Chicken ticked as bought, then rebuild with no plan change | Chicken is still ticked |
| Salt "to taste" in three dishes | One line in "Check you have", no quantity |

## 9. AI ingredient suggestions

### Behaviour

- Input: dish name, servings, optional notes ("no coriander", "vegetarian"), household dietary preferences.
- Output: a draft list shown in the recipe editor. Nothing is saved until the user saves the recipe.
- The model must return structured output that validates against a schema. Validate on the server; retry once on failure; then return a clear error the UI can show.

### Response shape (suggested)

```json
{
  "servings": 2,
  "ingredients": [
    {
      "name": "chicken thigh fillets",
      "quantity": 400,
      "unit": "g",
      "quantity_text": null,
      "category": "meat_seafood",
      "optional": false,
      "is_staple": false,
      "note": "or breast"
    }
  ]
}
```

`unit` is one of the units in section 8 or null. When the amount is vague, `quantity` and `unit` are null and `quantity_text` holds the phrase.

### Instructions to give the model

Metric units. Australian ingredient names. One entry per ingredient. Quantities sized for the requested servings. Mark everyday pantry basics (salt, pepper, cooking oil) as staples. Categories from the fixed aisle list only.

### Post-processing

1. Validate against the schema.
2. Match each name to the household's catalogue: exact match, then alias, then fuzzy match (PostgreSQL trigram similarity is a reasonable tool). Offer the match in the draft; create a new catalogue entry only when the recipe is saved and nothing matched.
3. Normalise units to the accepted set.

### Guardrails

- The API key lives only on the server.
- Rate-limit the endpoint per household and set a request timeout.
- Dish names and notes are user text: pass them as data, never as instructions.
- Put the provider behind an interface with a fake implementation, used in tests and in local development without a key.
- Log token usage per call so cost is visible.

## 10. Screens and UX

Mobile-first. The shopping list will be used one-handed in a supermarket, so favour large tap targets and instant feedback.

| Screen | Must do |
| --- | --- |
| Sign in / sign up | Email and password |
| Recipes | List with search, tag filter and favourites; create button |
| Recipe editor | Name, servings, tags, notes, optional method. Ingredient table with inline editing, add, delete, reorder, optional and staple toggles. "Suggest ingredients" button; AI drafts are visibly marked as unsaved |
| Planner | Week navigation. Meal cart. Grid of 7 days by breakfast, lunch, dinner. Add a dish to the cart or straight to a slot. Change servings per entry. "Build shopping list" |
| Shopping list | Grouped by aisle. Tick to mark bought. "Have it" to skip. "Check you have" group for staples. Expand a line to see its dishes. Add an extra. Clear bought items |
| Settings | Household name, timezone, hidden meal rows |

Notes:

- Assigning a dish to a slot must work by tapping. Drag and drop is a desktop enhancement, not a requirement.
- Ticking an item updates the screen immediately and syncs in the background.
- Suggested navigation on phones: a bottom bar with Plan, Recipes and List.

## 11. API surface (sketch)

A suggested shape, to be refined in the plan. All routes are under `/api`, return JSON, and use Problem Details for errors.

| Area | Endpoints |
| --- | --- |
| Auth | `POST /auth/register`, `POST /auth/login`, `POST /auth/logout`, `GET /me` |
| Ingredients | `GET /ingredients?search=`, `POST /ingredients`, `PATCH /ingredients/{id}` |
| Recipes | `GET /recipes?search=&tag=`, `POST /recipes`, `GET /recipes/{id}`, `PUT /recipes/{id}` (replaces the ingredient list as a whole), `DELETE /recipes/{id}`, `POST /recipes/{id}/duplicate` |
| AI | `POST /ai/ingredient-suggestions` |
| Meal plans | `GET /meal-plans/{weekStart}`, `POST /meal-plans/{weekStart}/entries`, `PATCH /meal-plan-entries/{id}`, `DELETE /meal-plan-entries/{id}` |
| Shopping list | `GET /shopping-list`, `POST /shopping-list/build` (body: weekStart), `POST /shopping-list/items` (extra), `PATCH /shopping-list/items/{id}`, `DELETE /shopping-list/items/{id}`, `POST /shopping-list/clear-bought` |

## 12. Security and multi-tenancy

- Every query for user data is scoped to the signed-in user's household. Enforce this in one place (for example an EF Core global query filter fed by a per-request household context), not ad hoc in each endpoint.
- Write integration tests that prove a user in household A cannot read or change household B's recipes, plans or list, including by guessing IDs.
- Role checks exist in code from the start, even though only the owner role is exercised in Stage 1.
- The app will be reachable from the internet. Restrict sign-up in Stage 1 (an allow-list of emails or an invite code from configuration) so strangers cannot create accounts and spend AI credit.
- Standard hygiene: hashed passwords via Identity, secure and same-site cookies, CSRF protection for cookie auth, input validation, rate limiting on auth and AI endpoints.

## 13. Testing expectations

- **Domain unit tests:** unit conversion, scaling, merging and the rebuild diff, including every worked example in section 8.
- **API integration tests:** auth, household isolation, recipe save with ingredients, plan edits, list build and rebuild, extras and state surviving a rebuild.
- **AI:** tests use the fake provider. One optional, manually run test may call the real provider.
- **Frontend:** type-check and lint in CI. A small end-to-end smoke test of the core loop is welcome but not required.
- CI runs build, tests and lint on every push.

## 14. Suggested milestones

1. **Scaffold.** Repo layout, local PostgreSQL, API with a health check, frontend shell with shadcn/ui, CI. OpenAPI-generated TypeScript client wired up.
2. **Units and merging library.** Pure domain code with full unit tests. No UI. Doing this early removes the main risk.
3. **Accounts and household.** Register, sign in, household context, query scoping, isolation tests.
4. **Catalogue and recipes.** Recipe CRUD with the editable ingredient table.
5. **AI suggestions.** Provider interface, fake and real implementations, draft flow in the editor.
6. **Meal planner.** Cart, week grid, servings override.
7. **Shopping list.** Build, rebuild, extras, states, grouping, sources.
8. **Deploy.** Production images, compose file, proxy integration with the existing app, backups and a tested restore.

## 15. Definition of done for Stage 1

The owner can, on a phone:

1. Sign up and sign in.
2. Create "Chicken katsu curry" for 2, tap "Suggest ingredients", edit two rows, delete one, add one, and save.
3. Reopen the recipe and see exactly the saved ingredients, with no AI call made.
4. Add five dishes to the cart and place them on breakfast, lunch and dinner slots across a week.
5. Build the shopping list and see merged, aisle-grouped items that match the worked examples in section 8.
6. Add "milk" and "dishwashing liquid" as extras.
7. Tick items off, remove a dish from the plan, rebuild, and see only that dish's share disappear while ticks and extras remain.
8. Do steps 4 and 5 for a new week in under 5 minutes.

And: the app runs on the VPS over HTTPS alongside the existing app, a nightly backup lands off the server, a restore has been tested, and all tests pass in CI.

## 16. Open questions to settle in planning

Raise these with the owner before finalising the plan. Each has a suggested answer.

1. **Catalogue scoping.** Is the ingredient catalogue per household, or a shared global list plus household additions? Suggested: per household, pre-filled from a small seed list on sign-up. Simplest for isolation, and it can be revisited before going public.
2. **Adding one dish straight to the list.** The product allows adding a single dish's ingredients without planning it. Suggested: treat it as an unscheduled entry in the current week's cart, so the list has one kind of source.
3. **A bought item whose quantity grows on rebuild.** Chicken is ticked, then another chicken dish is added. Suggested: return the item to "needed" and show that the amount changed.
4. **End of the week.** What happens to the list when a new week starts? Suggested: one ongoing list; the user clears bought items; building a new week adds to what remains.
5. **Optional ingredients.** Left off the list by default. Is a way to include them needed in Stage 1? Suggested: no.
6. **Frontend framework.** React with Vite is assumed from the choice of shadcn/ui. Confirm. - use nextjs.
7. **Sign-up restriction.** Allow-list or invite code? Suggested: invite code in configuration.
8. **Existing proxy on the VPS.** What serves the current app on ports 80 and 443? The answer decides how Pantrio is routed.
9. **Domain name.** Which domain or subdomain will Pantrio use?
10. **AI provider and model.** Confirm the provider and pick the model ID from current docs.

## 17. Later stages: do not build, do not block

| Stage | Contents | What Stage 1 must leave room for |
| --- | --- | --- |
| 2. Family and stock | Invites, roles in use, live shared plan and list, inventory with quantity or status tracking, list skips items at home, confirmed deduction after a meal's day passes | Household-scoped schema; `added_by` and `checked_by` fields; list totals derived from sources so an inventory subtraction step can be inserted; a server that can later push live updates (for example SignalR) |
| 3. Smarter picks | Recipe import from a web page, "decide for me", AI auto-plan, cooking log and ratings, AI-generated default cooking method | `method_source` on recipes; AI provider interface reusable for new prompts |
| 4. Video import | Recipes from RedNote, YouTube, TikTok and Instagram links via captions, subtitles, speech-to-text and on-screen text | The same ingredient schema as section 9; room for a background job queue |
| Later | Native mobile app, offline list, public sign-up | A clean JSON API with an OpenAPI contract; no server-rendered pages the app would depend on |
