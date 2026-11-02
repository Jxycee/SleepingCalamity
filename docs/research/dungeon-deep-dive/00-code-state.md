# What main already implements (checked 2026-11-02, f05a7fd), so synthesis doesn't re-recommend it
Core/Cellars: LayoutPreset (Spine/Ring/Hub/Wing, never same twice in a row); FloorRoute (spine, arcs, branch; cycle role KeyArc/ShortcutBack(label only)/Plain);
SpecialRoom (one capped special per floor, once per dive per kind; kinds greybox, effects later); DoorHints (reward flags on doors: chests, key, special, reward vignette, exit, checkpoint, locked);
LootMix (Hades-style feedback on loot category); Vignettes (Cogmind 4 categories, falling 1-in-N, Moai must-see, max per floor, guarantee floor, state-conditioned odds);
Chunks (1x1/2x1/3x1 regions, 5-6 variants: rubble, cask, arch, clutter); Atmosphere (lamp/fog/dust roll, dark floor ~1/12 never floor 1 never twice, odd-light room);
FloorRoster (roster subset floors 2-3; twist None/Ambush/Thin/Crowded floors 2-3, each at most once per dive); Ambush (ring spawn); Healing (51% floors 1-3);
KeyDrop (key always reachable); EncounterPlan (extras at stir 40, hunter at 70 at farthest room); RoomClear (host); DiveHaul (shared, host); StirMeter (linear player factor 0.25, latches; 90 s countdown).
Presentation: downed state with timer + hold-E revive; wipe = all heroes down -> dive fails; downed hero can't take stair; host disconnect = NGO shutdown, no migration (unbanked haul gone).
NOT present: floor map/minimap, arena door locks, secrets, cross-room dependencies (levers), objective types beyond key carrier, band mechanics, elite affixes, events with choices (vignettes are props+category), mutators with rewards, retreat ping, priced escape, solo self-revive, music plan.
Pool: 12 combat rooms (4 per sub-theme), entry, exit, checkpoint, 6 corridor pieces.
