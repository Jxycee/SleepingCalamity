# Gold economy proposal (2026-11-06)

Status: proposal for the user. Docs only; nothing here is built. All numbers are **placeholders** for tuning. Original design: no outside names or text; genre notes are mechanics only.

## Decisions for the user

1. **What does gold buy in the first dungeon?** Recommended: two sinks only, the paid boss retry (exists) and a provisions counter at the baker (potions, bombs, bread top-up). See Q1.
2. **Does an enemy kill pay gold?** Recommended: no for the first dungeon. Chests plus room-clear payouts are the sources. See Q2.
3. **What happens to gold in town?** Recommended: nothing special. Gold is already a personal number that survives; there is no bank-desk conversion. See Q3.
4. **Failed dive:** keep the settled rule (the dive's gold is halved, held gold untouched, decision 7). Do not use the triage note's "keep half of that player's gold". See Q4.
5. **Is gold ever shared or traded between players?** Recommended: never. See Q5.

## What exists today (code, 2026-11-06)

- Per-player gold lives in the bag: `PlayerBag.Gold` (held) and `DiveGold` (earned this dive), saved in `BagsSave`, synced owner-only in `PlayerBagSync` (`Gold`, `DiveGold`). The HUD already shows it (`StirMeterHud.Bag`).
- Sources, all host-granted through `DiveHaulSync.ServerAddGold`: a chest roll that comes up gold (`CellarLoot.GoldPurse = 25`, one roll in `GoldOneIn = 4`; `CellarContentsSync`), and room-clear payouts for Crowded and Ambush rooms (`CellarContentsSync` ~1047; Hurried floors pay `LeaveGold`).
- Sink: only the boss retry. `DiveMomentum.PriceFor` takes the idol first, else `GoldPrice = 500`.
- Not built: enemy-kill gold; any shop or town spend; any screen that shows or empties the satchel. `BagView.StashTotal` counts **items** in the satchel/stash, not gold, so a "stash view" is an item view and gold needs nothing extra.
- Settled by the user: failed dive halves the dive's gold (dungeon spec decision 7); gold is not bankable and uses no bank slot (decision 4); paid boss retry once per dive, idol first and gold as fallback (decisions 25, 32; section 11 item 3); gold is per player (decision 39). No weapon wear in Phase 1 (PHASE1 7), so there is no repair sink. Town rebuilding is paid in recovered **pieces and people**, not gold.

## Q1. What gold buys

Gold with no sink is a number going up. The first dungeon should stay simple, so the aim is the smallest set that makes gold feel like a choice.

| Option | What it is | Cost to build | Fit |
|---|---|---|---|
| A. Retry only | The 500 fallback is the only sink | none | Gold would be nearly meaningless unless the party misses the idol |
| **B. Retry + provisions counter (recommended)** | The baker (service opens when his wishes are met) sells a short fixed list before a dive: bread top-up to the stack of 10, a basic potion, a bomb. Bought items go in the bag and count toward bank slots only when bankable, like any find | One small town UI list; prices in data | Gives each dive a "spend what I earned" beat with no new systems; bread stays never-best (potions are about equal to the best bread) |
| C. B + weapon and gear shop | Buy boots, a starter weapon or a build-up material | New shop stock, drop-table overlap | Competes with finding gear in chests (decision 39 need-weighted rolls); defer |
| D. B + gold-priced town upgrades | Pay gold to upgrade buildings or helpers | Needs building upgrade design | Town growth is already paid by pieces and people; defer |

Placeholder prices (option B): bread top-up 5 per loaf, basic potion 40, bomb 25, retry 500 (unchanged). A clean 7-floor solo dive earns about 300-400 (triage C1), so a full restock is cheap and a gold retry costs about 1.5 dives of income: noticeable, not a wall. Gold stays the fallback because the idol is the primary price.

Open numbers for the user: only that the retry stays at 500 or moves; tuning waits for the M7 walk.

## Q2. Enemy-kill gold

- Option 1 (recommended): no per-kill gold in the first dungeon. Chest purses and room-clear payouts already pay; fewer grants means no new host call in the combat path and nothing to farm by re-pulling.
- Option 2: killer's bag only, by tier (Slime 2, Broom 3, RatKing 6), as drafted in triage C1. Matches per-player loot, rewards the one who fought, but a support player earns less and the enemy-kill hook needs a kill-credit path in `CombatHost`.
- Option 3: a per-room bonus split equally (what room-clear payouts already do): no change needed.

If the walk shows a dive earns under about 250 gold, add Option 2; do not decide before the walk.

## Q3. Town handling and the satchel view

- Gold needs no emptying step: it is already a held total. Skip the bank-desk conversion from triage C1.
- The satchel is an **item** matter (decision 35: emptied in town into town storage or the heroes' home). That is the real open piece; keep it as its own item with the bank room UI, not part of the economy.
- Show held gold on the town HUD as it is in the dive HUD; show the baker's price list only inside his service.

## Q4. Failed-dive gold

Keep decision 7 exactly: `DiveGold` is halved (round down), `Gold` held is untouched, and the lost satchel holds items only. The triage note's "keep half of that player's gold" line would also tax held gold and contradicts decision 7; recommend dropping it.

## Q5. Sharing and co-op

- Gold stays per player: no pooling, no trades, no sharing of a bought retry (the retry price is paid once from the party member who triggers it with enough gold; if none has 500, the dive fails as normal). Simple alternative: the party pays 500 split equally rounded up, each from their own gold; recommend this only if solo-vs-co-op feels unfair in the walk.
- No co-op gold multiplier: each player's chest rolls already scale with the party (decision 39).
- A rejoining or late player brings only their own saved gold.

## Build size if approved (Q1 B, Q2 option 1)

Small: price list in data, one baker-service screen, buy RPC (host validates gold and bag room; owner-only view; no new sync type), save already covers `Gold`. Needs a `risk-reviewer` pass because it touches bags and the host grant path.
