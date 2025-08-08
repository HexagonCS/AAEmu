aaemu_game MySQL Schema Guide (Game Server)

Purpose

- Scope: Persistent, player/world state for the game server. This schema stores mutable runtime data (characters, inventories, mail, guilds, housing, auction house, etc.). Static game design data lives in compact.sqlite3.
- Ownership: Mutated only by the game server during normal operation. Manual edits risk cache/state divergence; prefer server/admin tools or scheduled maintenance windows.

Connection & Location

- Config: `AAEmu.Game/ExampleConfig.json` → `Connections:MySQLProvider` (Host, Port, User, Password, Database).
- Code: Connection built in `AAEmu.Commons/Utils/DB/MySQL.cs` using `MySql.Data` with pooling enabled, UTF-8, and SSL mode preferred.
- Startup updates: On game start, `GameService` calls `MySqlDatabaseUpdater.Run(connection, "aaemu_game", <dbName>)`. It applies scripts from `SQL/updates` whose filename contains `aaemu_game` and records them in the `updates` table.
- Docker: `docker-compose.yaml` mounts `SQL/aaemu_game.sql` for initial bootstrap; readiness checks use `mysql aaemu_game -h db -u root -p$DB_PASSWORD`.

Domain Overview (key tables)

- Characters
  - `characters`: Core character record (identity, position, stats, currencies, timestamps); `account_id` links to `accounts`.
  - `abilities`, `actabilities`: Skillset and vocation progression per character.
  - `skills`: Learned skills per character.
  - `appellations`: Earned titles per character.
  - `completed_quests`, `quests`: Quest progress and state blobs per character.
  - Social: `friends`, `blocked` track relationships by owner/ids.

- Accounts & Economy
  - `accounts`: Account-scoped progression and currencies (labor, credits, loyalty), with update trigger on write.
  - `auction_house`: Live auction listings with seller/bidder references and timestamps.
  - ICS (cash shop): `ics_menu`, `ics_shop_items`, `ics_skus`; audit via `audit_ics_sales`.

- Inventory & Items
  - `items`: All item instances (type, template_id, owner, slots, grades, lifespans, charges, timestamps).
  - `item_containers`: Logical containers (bags/banks/coffers) referenced by `items.container_id`.
  - `uccs`: User‑created content (e.g., custom images) metadata.

- Companions & Music
  - `mates`: Mounts/pets bound to characters.
  - `music`: User music storage.

- Housing & World Objects
  - `housings`: Player buildings with placement, ownership, permissions, pricing, and protection windows.
  - `doodads`: Persistent world objects (e.g., tradepacks, furniture, plants) with transform and ownership fields.
  - Portal book: `portal_book_coords`, `portal_visited_district` store teleport coordinates and visited subzones.

- Guilds & Families
  - `expeditions` (guilds), `expedition_members`, `expedition_role_policies` (role permissions).
  - `family_members`: Family membership with roles/titles.

- Auditing & Logs
  - `audit_char_sus`: Suspicious activity log with spatiotemporal context and categorization.
  - `audit_ics_sales`: Cash shop sales audit trail.

How To Inspect the Schema

- Basic inventory
  - List tables: `SHOW TABLES FROM aaemu_game;`
  - Table DDL: `SHOW CREATE TABLE aaemu_game.items\G`
  - Columns: `DESCRIBE aaemu_game.characters;`

- Indices & estimates
  - Keys: `SHOW INDEX FROM aaemu_game.items;`
  - Table sizes: `SELECT table_name, table_rows, data_length/1024/1024 AS mb FROM information_schema.tables WHERE table_schema='aaemu_game' ORDER BY table_rows DESC;`

- Cross‑table discovery (common conventions)
  - Character ownership: columns named `owner`, `owner_id`, `character_id` typically reference `characters.id`.
  - Account linkage: `account_id` references `accounts.account_id`.
  - Containers: `items.container_id` → `item_containers.id`.
  - Guilds: `expedition_members.expedition_id` → `expeditions.id`.

Common Debug Queries

- Find a character and their account
  - `SELECT c.*, a.* FROM aaemu_game.characters c JOIN aaemu_game.accounts a ON a.account_id = c.account_id WHERE c.name = 'SomeName';`

- List inventory for a character
  - `SELECT i.* FROM aaemu_game.items i WHERE i.owner = 12345 ORDER BY i.container_id, i.slot;`

- Mailbox snapshot
  - `SELECT id, type, status, sender_name, receiver_name, send_date FROM aaemu_game.mails WHERE receiver_id = 12345 ORDER BY send_date DESC;`

- Auctions by seller
  - `SELECT * FROM aaemu_game.auction_house WHERE client_id = 12345 ORDER BY post_date DESC;`

- Doodads placed by a character
  - `SELECT * FROM aaemu_game.doodads WHERE owner_id = 12345 ORDER BY plant_time DESC;`

Operational Guidance

- Read‑only first: Prefer SELECTs for investigation; take backups before manual modifications.
- Avoid live edits: The server maintains in‑memory state. Manual UPDATE/DELETE while running can desync. If changes are required, stop the game server or use admin tools.
- Transactions: Group related changes in a transaction with consistent timestamps to keep invariants intact.
- Backups: `mysqldump aaemu_game --single-transaction --routines --triggers > backup.sql` (run with appropriate credentials/host).
- Analyze/Explain: Use `EXPLAIN` on slow queries; add supporting indexes via migrations (see below).

Migrations & Updates

- Source of truth: Initial DDL in `SQL/aaemu_game.sql`. Incremental changes in `SQL/updates/*aaemu_game*.sql`.
- Auto‑installer: On startup, the server ensures an `updates` bookkeeping table exists and executes pending scripts ordered by filename. Results are tracked so each file runs once.
- Script naming: Use a sortable prefix and module tag, e.g., `YYYY-MM-DD_aaemu_game_<topic>.sql`.
- Safety practices
  - Prefer `CREATE TABLE IF NOT EXISTS` and `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` (MySQL 8).
  - For indexes: on MySQL 8.0+, `CREATE INDEX IF NOT EXISTS idx_name ON table(col);` (verify server version) or pre‑check with `information_schema.statistics` inside a stored block.
  - Be explicit with collations/charsets (`utf8mb4_general_ci`) when creating new text columns or tables.
  - Keep changes idempotent where feasible; the update runner prevents re‑execution, but idempotency eases manual installs.

Example: safe index (pattern)

```
-- If on MySQL 8.0+ with IF NOT EXISTS support
CREATE INDEX IF NOT EXISTS idx_items_owner_slot ON aaemu_game.items (owner, container_id, slot);
```

Example: add column with default

```
ALTER TABLE aaemu_game.characters
  ADD COLUMN IF NOT EXISTS last_login_ip VARBINARY(16) NULL AFTER last_login;
```

Grepping & Tracing Changes

- Schema history: Review `SQL/updates/*aaemu_game*.sql` for evolution of features (e.g., auction house, items, mails, housing, ICS, audits).
- Code touchpoints: Search for table names and column names in `AAEmu.Game` to find read/write paths and invariants enforced at the application layer.
- Static vs. dynamic: When investigating a gameplay issue, correlate MySQL instance data (e.g., `items.template_id`) with static design from `compact.sqlite3` to understand intent.

Conventions & Invariants (observed)

- Primary keys: Many tables use composite keys involving owner and id (e.g., `abilities(id, owner)`, `mates(id, item_id, owner)`). Preserve these patterns when extending.
- Ownership fields: `owner` typically means `characters.id`; `account_id` is account scope; document any deviations in migrations.
- Timestamps: Several tables default to `CURRENT_TIMESTAMP` or `0001-01-01`. Be consistent when adding new audit columns.

Performance Tips

- Hot paths: Expect frequent access on `items(owner, container_id, slot)`, `characters(name)`, `mails(receiver_id, send_date)`, `auction_house(client_id, post_date)`. Support with appropriate composite indexes.
- Maintenance: For large, fragmented tables after heavy churn, consider `ANALYZE TABLE`/`OPTIMIZE TABLE` during maintenance windows.

Admin Playbook (common tasks)

- Grant temporary currency to an account
  - Prefer in‑game/admin API. If offline maintenance: `UPDATE aaemu_game.accounts SET credits = credits + 100 WHERE account_id = 42;`

- Move a stuck character (offline)
  - `UPDATE aaemu_game.characters SET zone_id = <safe_zone>, x = ..., y = ..., z = ... WHERE id = 12345;`

- Find and remove orphaned items (diagnostics)
  - `SELECT i.id FROM aaemu_game.items i LEFT JOIN aaemu_game.characters c ON c.id = i.owner WHERE c.id IS NULL LIMIT 50;`

Versioning & Environment

- MySQL server: Target MySQL 8.x. Some DDL uses 8.x conveniences (`IF NOT EXISTS`). Verify server version before relying on them.
- Client library: `MySql.Data` 9.x used by the server. Connection pooling and timeouts are already configured in code.

Appendix: Table Catalog

- Characters: `characters`, `abilities`, `actabilities`, `skills`, `appellations`, `completed_quests`, `quests`, `options`.
- Inventory: `items`, `item_containers`, `uccs`.
- Social: `friends`, `blocked`, `family_members`.
- Guilds: `expeditions`, `expedition_members`, `expedition_role_policies`.
- Housing & World: `housings`, `doodads`, `portal_book_coords`, `portal_visited_district`.
- Commerce: `auction_house`, ICS tables `ics_menu`, `ics_shop_items`, `ics_skus`.
- Messaging: `mails`.
- Companions & Music: `mates`, `music`.
- Auditing: `audit_ics_sales`, `audit_char_sus`.

If you add new tables, keep them grouped by domain and include clear comments in DDL mirroring the style in `SQL/aaemu_game.sql`.

