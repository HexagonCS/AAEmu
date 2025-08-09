# Agent Guide

This file orients any AI/dev agent to the repo in seconds. It links all supporting docs and shows exactly where to start for common tasks.

## Docs‑First Rule
- Always open the relevant docs before acting. Start with this file’s Start Here and Quick Decision Map, then jump to the specific guide (e.g., `Docs/compact-sqlite.md` for SQLite questions, service READMEs for config, `Docs/mysql-aaemu_game.md` for MySQL).
- Verify paths, environment, and caveats from docs first (e.g., note Korean localization, Docker hostnames, secrets policy) before running commands or changing code.
- Reference the doc you used in your task notes/PR description. If something is missing or unclear, ask, then update the doc.

## Sandbox & Approvals
- Do not assume tools are unavailable. If a command (e.g., `sqlite3`) fails due to sandboxing, re-run it with elevated permissions and a 1‑sentence justification.
- Prefer read‑only flags when possible (e.g., `sqlite3 -readonly` for `compact.sqlite3`).
- Ask for elevation before any potentially destructive action (deletes, resets, DB writes) unless the task explicitly requests it.
- For SQLite work, prefer direct `sqlite3` queries over grepping the binary; escalate if needed rather than skipping the query.

## Start Here
- Overview: `README.md` (project, links to Wiki/Discord).
- Quick setup: `Docs/getstarted.md` (manual install) and `Docs/Docker-Installation-Guide.md` (Docker flow).
- Component map: `Docs/components.md` (how services and DBs interact).
- Service configs: `AAEmu.Login/README.md`, `AAEmu.Game/README.md` (config keys, secrets usage).
- Schemas & seeds: `SQL/` (`aaemu_login.sql`, `aaemu_game.sql`).
- Scripts: `Scripts/` (local run helpers, docker install/update, start scripts).
- Code sharing: `AAEmu.Commons/` (models, networking, utils).
- Tests: `AAEmu.UnitTests/`, `AAEmu.IntegrationTests/`.

## Quick Decision Map (When You Get a Task)
- Feature/bug in gameplay, items, skills, NPCs: check `AAEmu.Game/Services`, `AAEmu.Game/GameData`, `AAEmu.Game/Models` and references in `AAEmu.Commons/Models`.
- Login/auth/session issue: check `AAEmu.Login/Core`, `AAEmu.Login/Program.cs`, and shared `AAEmu.Commons/Network`.
- Packet/protocol change: search in `AAEmu.Commons/Network` and respective service handlers in `AAEmu.Game` or `AAEmu.Login`.
- Config change: copy `ExampleConfig.json` → `Config.json` in target service or prefer `dotnet user-secrets` per the service README.
- Data-driven behavior (IDs, loot, skills): verify `compact.sqlite3`. See compact-sqlite.md for more info. (many human-readable fields (names, descriptions, UI strings) are in Korean. Searching by English text may not yield results; prefer IDs, category keys, or explicit queries that target known columns. Localization exists within the compact.sqlite3's 'localized_texts' table. It is recommended to first identify the Korean equivalent to any English string to lookup. Korean string is in the 'ko' column, and English string is in the 'en_us' column.)
  - Loot amount multiplier: server supports `UnitAttribute.LootItemCountMul` (187, VALUE-type percent) on buffs to scale non-coin item counts; see `Docs/compact-sqlite.md` for the insert pattern and an example with Lucky Quicksilver Tonic. Also see world config `World.LootItemCountRate` for a global multiplier.
- DB schema/state issues: use `SQL/` for MySQL base schemas; remember two DBs: `aaemu_login`, `aaemu_game`.

## Commands Cheat Sheet
- Build: `dotnet restore && dotnet build -c Release`.
- Unit tests (no integration): `dotnet test --filter FullyQualifiedName!~AAEmu.IntegrationTests /p:CollectCoverage=true`.
- One test: `dotnet test --filter "FullyQualifiedName~Namespace.ClassName.Method"`.
- Integration tests only: `dotnet test AAEmu.IntegrationTests`.
- Run services:
  - Login: `dotnet run --project AAEmu.Login/AAEmu.Login.csproj`
  - Game: `dotnet run --project AAEmu.Game/AAEmu.Game.csproj`
  - Helpers: see `Scripts/` (`start_login.*`, `start_game.*`).

## Data & Config Essentials
- Game data (SQLite): place `compact.sqlite3` at `AAEmu.Game/Data/` (copied to `bin/<Config>/net9.0/Data/` on build). Docker: `.server_files/AAEmu.Game/Data/compact.sqlite3` (mounted to `/app/Data`).
- World config: keys in `Configurations/World.json` bind to `WorldConfig` (merged in memory). Added `LootItemCountRate` to scale non-coin loot item counts globally (default 1.0). Existing bin configs use PreserveNewest copy semantics; update the project file or delete bin copies if you need to pick up source changes.
- MySQL 8: two schemas `aaemu_login` and `aaemu_game`. In Docker Compose, hostnames are `db` (MySQL) and `login` (login service).
- Secrets: never commit creds. Prefer `dotnet user-secrets` as outlined in `AAEmu.Login/README.md` and `AAEmu.Game/README.md`. For Docker, edit `.env` (e.g., `DB_PASSWORD`).
- Text locale note: many `compact.sqlite3` text fields (item/skill/event names and descriptions) are in Korean. English keyword greps may not match expected rows; prefer ID-based searches or query the DB directly.
- Grep hygiene: exclude SQLite files in repo-wide searches to avoid noisy binary matches. Examples: `rg -S <term> -g '!**/*.sqlite3'` or `grep -R --binary-files=without-match --exclude='*.sqlite3' <term> .`. If you intend to search inside the DB, use `sqlite3` queries instead (see `Docs/compact-sqlite.md`).

## Reference DBs At A Glance
- `compact.sqlite3` (read-only): Static design/reference data extracted from client assets; used by `AAEmu.Game` to populate world definitions at startup. Typical tables include `items`, `skills`, `effects`, many `doodad_*`, `quest_*`, crafting (`craft_*`), FX (`fx_*`), icons, categories. Location: `AAEmu.Game/Data/compact.sqlite3`. Details: `Docs/compact-sqlite.md`.
- `aaemu_game` (MySQL, mutable): Persistent runtime state for characters, inventories, mails, housing, doodads, auctions, guilds, companions, etc. Owned by the game server; do not live‑edit while running. Initial DDL: `SQL/aaemu_game.sql`; incremental updates in `SQL/updates/*aaemu_game*.sql`. Details: `Docs/mysql-aaemu_game.md`.

## Supporting Docs Index
- `README.md`: entry point, community links.
- `Docs/getstarted.md`: manual setup guide for new environments.
- `Docs/components.md`: architecture of servers, DBs, and client.
- `Docs/Docker-Installation-Guide.md`: compose scripts, watch, common pitfalls.
- `Docs/compact-sqlite.md`: compact.sqlite3 purpose, domains, inspection helpers.
- `Docs/mysql-aaemu_game.md`: aaemu_game schema overview, operations, and migrations.
- `Docs/skills-plots.md`: skills execution model (effect vs plot), controllers, projectile timing, CC flags, and verification SQL.
- `AAEmu.Login/README.md`: login config structure and secrets commands.
- `AAEmu.Game/README.md`: game config structure and secrets commands.
- `SQL/`: bootstrap schemas and SQL snippets used by guides.
- `Scripts/`: docker install/update and start scripts for each service.

---

# Repository Guidelines

## Project Structure & Modules
- `AAEmu.Game`: Game server (configs, services, data, scripts).
- `AAEmu.Login`: Login server (internal/external networks, auth).
- `AAEmu.Commons`: Shared models, networking, utilities.
- `AAEmu.UnitTests` / `AAEmu.IntegrationTests`: xUnit test projects.
- `SQL`: MySQL schemas and seed data; consumed by Docker.
- `Scripts`: Local helpers (start, watch, docker install/update).
- `Docs`, `Tools`: Documentation and conversion utilities.

## Architecture Overview
- Two services: `Login` (ports 1234 internal, 1237 external) and `Game` (ports 1239 game, 1250 stream, optional 1280 Web API).
- MySQL 8 stores `aaemu_login` and `aaemu_game`. Shared code resides in `AAEmu.Commons`.
- Docker Compose wires `db -> login -> game`; in Compose, use hostnames `db` and `login` in configs.

## Game Data (SQLite)
- File: `compact.sqlite3` (read-only) contains Events, Buffs, Items, Skills, NPCs.
- Location (runtime): `./Data/compact.sqlite3` relative to the executable (`FileManager.AppPath`).
- Easiest setup: place the file in `AAEmu.Game/Data/`; it is copied to `bin/<Debug|Release>/net9.0/Data/` on build.
- Alternative: drop it directly into `AAEmu.Game/bin/<Config>/net9.0/Data/`.
- Docker: put it at `.server_files/AAEmu.Game/Data/compact.sqlite3` (mounted to `/app/Data`).
- Verify: on server start, no "Server database does not exist" fatal in logs; game data loads successfully.
- Language caveat: names/descriptions are often stored in Korean in this DB. Plan searches accordingly and prefer `sqlite3` over grepping the binary file.

## Local Dev Workflow
1) Prereqs: .NET SDK 9 (`global.json`), Docker (optional), MySQL 8.
2) Configure: copy `ExampleConfig.json` to `Config.json` in `AAEmu.Login` and `AAEmu.Game`, or prefer secrets:
   - `dotnet user-secrets init`
   - `dotnet user-secrets set "Connections:MySQLProvider:Host" "localhost"` (and other keys shown in project READMEs).
3) Database via Docker: `cp .env.example .env && docker compose up -d` (edit `DB_PASSWORD`).
4) Run services:
   - Login: `dotnet run --project AAEmu.Login/AAEmu.Login.csproj`
   - Game: `dotnet run --project AAEmu.Game/AAEmu.Game.csproj`
   - Helpers: `Scripts/start_login.sh`, `Scripts/start_game.sh` (Linux) or corresponding `.bat`/`.ps1` on Windows.

## Build, Test, and Coverage
- Build: `dotnet restore && dotnet build -c Release`.
- Unit tests (exclude Integration):
  - `dotnet test --filter FullyQualifiedName!~AAEmu.IntegrationTests /p:CollectCoverage=true`
- Run a single test: `dotnet test --filter "FullyQualifiedName~Namespace.ClassName.Method"`.
- Integration tests only: `dotnet test AAEmu.IntegrationTests` (keep DB/config isolated).
- CI mirrors these steps and uploads Coveralls from `AAEmu.UnitTests/TestResults/coverage.info`.

## Coding Style & Naming
- Indentation: spaces (C# 4, JSON 4); line endings: CRLF.
- `using System.*` first, prefer `var`, require braces, keep analyzers warning-free.
- Naming (see `.editorconfig`):
  - Types/members/constants: PascalCase.
  - Instance fields: `_camelCase` prefix `_`.
  - Static fields: `s_camelCase` prefix `s_`.
  - Locals/parameters: camelCase.
- Prettier exists for non-C# assets (`.prettierrc`).

## Commit, Branching & PRs
- Branch from `develop`; name `feature/...`, `fix/...`, `chore/...`.
- Commits: present tense and scoped (e.g., "Add NPC spawn validation").
- PRs: clear description, rationale, tests, linked issues (e.g., `Fixes #123`), and relevant logs/screens.
- Ensure GitHub Actions “Build & Unit Test” is green before requesting review.

## Security & Configuration Tips
- Never commit secrets. Use `dotnet user-secrets` or environment variables; for Docker, edit `.env` (e.g., `DB_PASSWORD`).
- In Compose, config hosts: `db` for MySQL, `login` for the login service.

## Troubleshooting
- DB connection errors: confirm `docker compose ps` shows `db` healthy; verify `MYSQL_ROOT_PASSWORD` matches `.env`.
- Port conflicts: free 1234/1237/1239/1250 or update configs.
- Game cannot reach login: in Docker, set `LoginNetwork:Host` to `login`; locally, use the actual IP.
