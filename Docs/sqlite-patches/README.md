# SQLite Patch Scripts

Create one `.sql` file per change to `compact.sqlite3`, and have a maintainer apply it manually. Do not modify the database from application code.

## Conventions
- Location: this folder (`Docs/sqlite-patches/`).
- Naming: `YYYYMMDD_short-description.sql` (e.g., `20250807_add-index-icons-name.sql`).
- One logical change per file; small, reviewable diffs.
- Idempotent and transactional; safe to re-run.
- Record in `schema_migrations` with a unique `version` value.

## Template
See `TEMPLATE_patch.sql` for a starting point. Replace placeholders with actual names.

## Applying a Patch (by maintainer)
1) Backup the DB file (e.g., copy `compact.sqlite3` to `compact.sqlite3.bak`).
2) Execute with SQLite CLI:
   - Windows: `sqlite3 D:\...\compact.sqlite3 ".read D:\...\patch.sql"`
   - Linux/macOS: `sqlite3 ./compact.sqlite3 < ./patch.sql`
3) Verify:
   - Schema: `SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;`
   - Indices: `PRAGMA index_list('your_table');`
   - Sample queries for expected rows/columns.

Rollback = restore your backup.

## Localization updates (required when text mentions changed values)
- Table: `localized_texts(tbl_name, tbl_column_name, idx, ko, en_us, ...)`.
- Always update both Korean (`ko`) and English (`en_us`) strings when your patch changes in-game numbers or wording.
- Find rows by entity and column, for example:
  - Item name: `SELECT id FROM localized_texts WHERE tbl_name='items' AND tbl_column_name='name' AND idx=<ITEM_ID>;`
  - Skill/buff description: `SELECT id FROM localized_texts WHERE tbl_name='skills' AND tbl_column_name='desc' AND idx=<SKILL_ID>;`
  - Buff description: `SELECT id FROM localized_texts WHERE tbl_name='buffs' AND tbl_column_name='desc' AND idx=<BUFF_ID>;`
- Prefer precise `WHERE id IN (...)` filters to avoid accidental replacements elsewhere.

Example snippet (replace IDs and text as needed):

BEGIN TRANSACTION;

-- Update English
UPDATE localized_texts
SET en_us = REPLACE(en_us, '10%', '80%')
WHERE id IN (<ID1>, <ID2>);

-- Update Korean
UPDATE localized_texts
SET ko = REPLACE(ko, '10%', '80%')
WHERE id IN (<ID1>, <ID2>);

COMMIT;

-- Verify
-- SELECT en_us, ko FROM localized_texts WHERE id IN (<ID1>, <ID2>);
