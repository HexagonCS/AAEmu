-- Patch: Vocation Expertise Tonic (item_id=8000020)
-- Goal: Change both effects from 10% to 80%
-- - Decrease production time by 80% (was -10)
-- - Increase vocation gained by 80% (was +10)
-- Chain: items(8000020) -> use_skill_id(8000013) -> skill_effects(effect_id=8000032)
--        effects(8000032: BuffEffect->8000022) -> buff_effects(8000022 -> buff_id=8000010)
--        modifiers live on:
--          unit_modifiers(owner_type='Buff', owner_id=8000010, unit_attribute_id=137, value=10)
--          skill_modifiers(owner_type='Buff', owner_id=8000010, skill_attribute_id=4, unit_modifier_type_id=1, value=-10)

BEGIN TRANSACTION;

-- Safety checks (no-op if rows don’t exist)
-- Verify the item and skill linkage
-- SELECT id, name, use_skill_id FROM items WHERE id=8000020;
-- SELECT id, name FROM skills WHERE id=8000013;
-- SELECT id, effect_id FROM skill_effects WHERE skill_id=8000013;
-- SELECT id, actual_type, actual_id FROM effects WHERE id=8000032;
-- SELECT id, buff_id FROM buff_effects WHERE id=8000022;

-- 1) Increase vocation gained to +80%
-- unit_modifiers entry currently at +10 → set to +80
UPDATE unit_modifiers
SET value = 80
WHERE owner_type = 'Buff'
  AND owner_id = 8000010
  AND unit_attribute_id = 137
  AND value <> 80;

-- 2) Decrease production time by 80%
-- skill_modifiers entry currently at -10 → set to -80
UPDATE skill_modifiers
SET value = -80
WHERE owner_type = 'Buff'
  AND owner_id = 8000010
  AND skill_attribute_id = 4
  AND unit_modifier_type_id = 1
  AND value <> -80;

-- 3) Localization: update text from 10% -> 80%
-- Affects: skills.desc (id=315863), skills.web_desc (id=315864), buffs.desc (id=315866)
-- Update English
UPDATE localized_texts
SET en_us = REPLACE(en_us, '10%', '80%')
WHERE id IN (315863, 315864, 315866);

-- Update Korean
UPDATE localized_texts
SET ko = REPLACE(ko, '10%', '80%')
WHERE id IN (315863, 315864, 315866);

COMMIT;

-- Optional verification (run manually):
-- SELECT value FROM unit_modifiers WHERE owner_type='Buff' AND owner_id=8000010 AND unit_attribute_id=137;
-- SELECT value FROM skill_modifiers WHERE owner_type='Buff' AND owner_id=8000010 AND skill_attribute_id=4 AND unit_modifier_type_id=1;
