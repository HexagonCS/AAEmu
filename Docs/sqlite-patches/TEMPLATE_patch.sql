-- Patch: <YYYYMMDD_short-description>
-- Purpose: <what/why>
-- Notes: idempotent and transactional; safe to re-run

BEGIN TRANSACTION;

-- 1) Ensure migrations table exists for tracking
CREATE TABLE IF NOT EXISTS schema_migrations(version TEXT);

-- 2) Example: add an index if missing
-- Replace <table> and <column>
CREATE INDEX IF NOT EXISTS idx_<table>_<column>
  ON "<table>"("<column>");

-- 3) Example: additive schema change (avoid destructive DDL)
-- ALTER TABLE "<table>" ADD COLUMN "<new_column>" TEXT DEFAULT NULL;

-- 4) Example: data patch using INSERT OR REPLACE
-- INSERT OR REPLACE INTO "<table>"(id, name) VALUES (123, 'example');

-- 5) Record application
INSERT INTO schema_migrations(version) VALUES ('<YYYYMMDD_codex_short>');

COMMIT;

-- Verification checks (run manually after applying):
-- PRAGMA index_list('<table>');
-- SELECT COUNT(1) FROM "<table>";
