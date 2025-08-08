-- Patch: 20250808_increase-inventory-cap
-- Purpose: Allow inventory expansions beyond 130 slots by adding new bag_expands steps
-- Notes: Idempotent and transactional; safe to re-run. Default uses Expansion Scroll (item_id 8000025).

BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS schema_migrations(version TEXT);

-- Add new inventory expansion steps (is_bank = 0) for steps 8..10 => 140/150/160 slots
-- Pricing/requirements: set to consume 1x Expansion Scroll (8000025); adjust as desired.

INSERT INTO bag_expands(is_bank, step, price, item_id, item_count, currency_id)
SELECT 0, 8, 0, 8000025, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM bag_expands WHERE is_bank=0 AND step=8);

INSERT INTO bag_expands(is_bank, step, price, item_id, item_count, currency_id)
SELECT 0, 9, 0, 8000025, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM bag_expands WHERE is_bank=0 AND step=9);

INSERT INTO bag_expands(is_bank, step, price, item_id, item_count, currency_id)
SELECT 0, 10, 0, 8000025, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM bag_expands WHERE is_bank=0 AND step=10);

-- Optionally extend bank expansions too (uncomment if needed)
-- INSERT INTO bag_expands(is_bank, step, price, item_id, item_count, currency_id)
-- SELECT 1, 8, 0, 8000025, 1, 0
-- WHERE NOT EXISTS (SELECT 1 FROM bag_expands WHERE is_bank=1 AND step=8);
-- INSERT INTO bag_expands(is_bank, step, price, item_id, item_count, currency_id)
-- SELECT 1, 9, 0, 8000025, 1, 0
-- WHERE NOT EXISTS (SELECT 1 FROM bag_expands WHERE is_bank=1 AND step=9);
-- INSERT INTO bag_expands(is_bank, step, price, item_id, item_count, currency_id)
-- SELECT 1, 10, 0, 8000025, 1, 0
-- WHERE NOT EXISTS (SELECT 1 FROM bag_expands WHERE is_bank=1 AND step=10);

-- Record application
INSERT INTO schema_migrations(version) VALUES ('20250808_increase-inventory-cap');

COMMIT;

-- After applying, restart AAEmu.Game. New expansions will push bag size to 140/150/160.
-- Verify in logs that CharacterManager loaded bag_expands and expand steps exist.

