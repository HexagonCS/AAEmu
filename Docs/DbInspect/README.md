# compact.sqlite3 Inspection

Generated: 2025-08-07 21:19:10Z

## Overview

- Tables: 677
- Edges (inferred): 15253
- DB Path: `/mnt/d/Repos/AAEmu/AAEmu.Game/Data/compact.sqlite3`

## Top Tables by Rows

- localized_texts: 263635
- quest_acts: 26886
- doodad_phase_funcs: 24465
- effects: 23697
- craft_materials: 23475
- items: 21482
- item_assets: 21276
- doodad_func_groups: 20077
- skill_effects: 19285
- item_armor_assets: 18003
- quest_components: 17851
- unit_modifiers: 16159
- npc_spawner_npcs: 15967
- npc_spawners: 15275
- skills: 15126
- loot_pack_dropping_npcs: 14977
- hair_colors: 14751
- doodad_funcs: 13974
- loots: 13823
- sound_pack_items: 13664

## Frequent Reference Columns

- item_id: appears in 90 tables
- npc_id: appears in 36 tables
- skill_id: appears in 35 tables
- buff_id: appears in 33 tables
- quest_act_obj_alias_id: appears in 29 tables
- doodad_id: appears in 25 tables
- kind_id: appears in 24 tables
- owner_id: appears in 24 tables
- category_id: appears in 21 tables
- model_id: appears in 20 tables
- sound_id: appears in 15 tables
- attach_point_id: appears in 12 tables
- grade_id: appears in 12 tables
- highlight_doodad_id: appears in 12 tables
- quest_id: appears in 11 tables
- faction_id: appears in 11 tables
- tag_id: appears in 10 tables
- zone_group_id: appears in 10 tables
- wi_id: appears in 10 tables
- zone_id: appears in 10 tables

## Sample Relationships

- accept_quest_effects -> doodad_func_quests via `quest_id`
- accept_quest_effects -> doodad_func_require_quests via `quest_id`
- accept_quest_effects -> game_schedule_quests via `quest_id`
- accept_quest_effects -> item_accept_quests via `quest_id`
- accept_quest_effects -> model_quest_cameras via `quest_id`
- accept_quest_effects -> quest_act_check_complete_components via `quest_id`
- accept_quest_effects -> quest_act_check_guards via `quest_id`
- accept_quest_effects -> quest_act_check_spheres via `quest_id`
- accept_quest_effects -> quest_act_con_accept_components via `quest_id`
- accept_quest_effects -> quest_act_con_accept_item_gains via `quest_id`
- accept_quest_effects -> quest_act_con_accept_items via `quest_id`
- accept_quest_effects -> quest_act_con_accept_npc_kills via `quest_id`
- accept_quest_effects -> quest_act_con_accept_npcs via `quest_id`
- accept_quest_effects -> quest_act_con_accept_spheres via `quest_id`
- accept_quest_effects -> quest_act_con_auto_completes via `quest_id`
- accept_quest_effects -> quest_act_con_fails via `quest_id`
- accept_quest_effects -> quest_act_con_report_doodads via `quest_id`
- accept_quest_effects -> quest_act_con_report_journals via `quest_id`
- accept_quest_effects -> quest_act_con_report_npcs via `quest_id`
- accept_quest_effects -> quest_act_obj_aggros via `quest_id`
- accept_quest_effects -> quest_act_obj_aliases via `quest_id`
- accept_quest_effects -> quest_act_obj_complete_quests via `quest_id`
- accept_quest_effects -> quest_act_obj_crafts via `quest_id`
- accept_quest_effects -> quest_act_obj_express_fires via `quest_id`
- accept_quest_effects -> quest_act_obj_interactions via `quest_id`
- accept_quest_effects -> quest_act_obj_item_gathers via `quest_id`
- accept_quest_effects -> quest_act_obj_item_uses via `quest_id`
- accept_quest_effects -> quest_act_obj_monster_group_hunts via `quest_id`
- accept_quest_effects -> quest_act_obj_monster_hunts via `quest_id`
- accept_quest_effects -> quest_act_obj_spheres via `quest_id`
- accept_quest_effects -> quest_act_obj_talks via `quest_id`
- accept_quest_effects -> quest_act_obj_zone_kills via `quest_id`
- accept_quest_effects -> quest_act_supply_appellations via `quest_id`
- accept_quest_effects -> quest_act_supply_coppers via `quest_id`
- accept_quest_effects -> quest_act_supply_exps via `quest_id`
- accept_quest_effects -> quest_act_supply_items via `quest_id`
- accept_quest_effects -> quest_act_supply_living_points via `quest_id`
- accept_quest_effects -> quest_act_supply_lps via `quest_id`
- accept_quest_effects -> quest_acts via `quest_id`
- accept_quest_effects -> quest_cameras via `quest_id`
- accept_quest_effects -> quest_chat_bubbles via `quest_id`
- accept_quest_effects -> quest_component_texts via `quest_id`
- accept_quest_effects -> quest_components via `quest_id`
- accept_quest_effects -> quest_contexts via `quest_id`
- accept_quest_effects -> quest_monster_groups via `quest_id`
- accept_quest_effects -> quest_monster_npcs via `quest_id`
- accept_quest_effects -> quest_supplies via `quest_id`
- accept_quest_effects -> sphere_quests via `quest_id`
- account_attribute_effects -> wearable_kinds via `kind_id`
- achievement_objectives -> achievements via `achievement_id`
