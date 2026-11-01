State: WORKING
Session: Local next: M5 follow-ups (healing, stir effects hook, stir HUD, flaky parry test)
Progress: 50% (healing merged 88086f9, parry fix merged 78b4326; stir HUD + stir effects in progress)
Status: items 1 + 4 merged; 2 + 3 in progress
Archive: not ready
Branch: claude/m5-next-639689 (each item on its own subagent branch, merged to main on its own green check)
Last check: PASS ca545f1 (healing: edit 798/798, play 37 pass 0 fail 8 skip); PASS be2f382 (parry: 786/786, 36/0/8)
Next step: review + check + merge stir HUD (claude/m5-stir-hud) and stir effects (claude/m5-stir-effects)
Sheet: none
Needs: none
Blocker: none

## Plan (each item merges on its own green check)
1. P47 healing: Core picks healing points for about half of floors 1-3, deterministic per (seed, floor) on its own Rng.Derive stream (floor goldens untouched); 30 s recharge placeholder in one tuning spot; full heal at the floor-2 checkpoint and floor-4 bank point on a successful bank. Host-authoritative via HeroVitals.ServerHeal. EditMode + PlayMode. risk-reviewer.
2. Stir effects hook: no cellar enemy spawner exists yet (only the arena's), so: a Core encounter plan per (layout, stir) giving enemies per room = spawn points + ExtraEnemiesPerRoom and a rat-king placeholder cell once the hunter latches; host exposes it; greybox marker for the rat-king. risk-reviewer if networked.
3. Minimal stir HUD meter in the UI assembly reading DiveHaulSync's summary (value bar + latch flags + deep-closes seconds). reviewer.
4. Flaky CombatArenaTests.Solo_DaggerGuard_Reduces_AndAParryStopsTheHit: root cause (real-time parry window), deterministic fix, no retry hacks. reviewer.

## Results
- Item 1 P47 healing: SC RESULT check PASS ca545f1 (edit 798/798, play 37 pass 0 fail 8 skip); MERGED main 88086f9. New Core CellarHealing + HealTuning (51%, 30 s, 1.6 m; own "cellars.healing" stream; 12345 goldens unchanged; floor 4 none, floor 2 may have one); host walk-on heal via HeroVitals.ServerHeal + broadcast; NetHealSecondsLeft NetworkVariable drives the HealingOrb on every peer; ServerBank heals all living heroes + Banked event. risk-reviewer approve; fixed: client test pins its own floor sync, floor-sync lookup throttled to 1/s, heal tick before orb. Notes: downed heroes are not healed or revived by a bank (as HealingDrop); repeated checkpoint banks give repeated full heals (spec); M6 flow must call both ServerSetFloors.
- Item 4 parry flake: root cause test-only (shield raised after a real-time 0.1 s wait, about one frame of margin before TestNova's hitbox). Fix: fixed 1/60 s step for that section, shield keyed to the attack frame. 3 filtered runs 12/12; SC RESULT check PASS be2f382; MERGED main 78b4326.
- Procgen redirect (coordinator) then reversed by the user: one procgen subagent started and stopped, nothing merged; Q12 owns procgen.

## Usage waste
- One procgen subagent run thrown away after the coordinator reversed the redirect (stopped early).
