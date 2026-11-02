# Co-op bot, quad (4 players), 5 min, seed 12345

Game time 300 s, 4 players (host DiveBot + 3 FollowerBot).

## Headline

- Deepest floor reached: 3; floors left (host bot): 2; dives taken home: 0
- Dives failed: 0 (host bot: after low HP 0, near a floor/traversal fault 0)
- Hero downs: 0 (); out: 0; revives (downed back up): 0
- Time downed: 0 s total ()
- Hash samples: 29, agreed 29, mismatches 0
- Log: 0 errors/exceptions (0 distinct), 0 warnings (0 distinct) since the party was ready

## Host DiveBot

300 s, dive fails 0 (after low HP 0), floors cleared 2, chests 7, pieces 3, presses 11, heal trips 1 (reached 1), loaves eaten 0, fails near a floor fault 0, hits per floor F1 6 in 1 visits, F2 6 in 1 visits, F3 7 in 1 visits, stuck spells 0, downed False, heart banked False, went home False, findings 3

## Followers

- follower 1: 300 s, walked 1081 m, downs 0 (0 s down), revive holds 0.0 s, requests bank 1 / stair 0 / home 0, loaves 0, fight 93 s, stuck spells 0, rescues 0, findings 0 (swings 81, dodges 47; ArenaBot hit counts are inflated by the other peers' copies of each enemy, not reported)
- follower 2: 300 s, walked 1023 m, downs 0 (0 s down), revive holds 0.0 s, requests bank 1 / stair 0 / home 0, loaves 0, fight 91 s, stuck spells 0, rescues 0, findings 0 (swings 65, dodges 45; ArenaBot hit counts are inflated by the other peers' copies of each enemy, not reported)
- follower 3: 300 s, walked 1021 m, downs 0 (0 s down), revive holds 0.0 s, requests bank 1 / stair 0 / home 0, loaves 0, fight 83 s, stuck spells 0, rescues 0, findings 0 (swings 64, dodges 44; ArenaBot hit counts are inflated by the other peers' copies of each enemy, not reported)

## Hash mismatches (0)


## Errors (0 distinct)


## Warnings (0 distinct)


## Findings (3)

- host: **Door** F2 at (241.5, 0.0, -31.6): the door reads open (DoorOpen) but its gate object still blocks
- host: **Prompt** F2 at (229.6, 0.0, -39.0): standing 3.0 m from the checkpoint bank but the prompt is 'None' ('')
- host: **Prompt** F2 at (229.6, 0.0, -39.1): standing 2.9 m from the checkpoint bank but the prompt is 'None' ('')

## What these bots cannot do

- Followers do not path to goals of their own: they trail the host bot, so they only take pickups, chests and prompts the lead walks them into. They ask for bank / stair / go-home by RPC from their own director (no key press, so the prompt code itself is only exercised for the host hero).
- Every peer's director and chest code reads the one App input service (the host bot's), so a host interact press can also fire a nearby follower's own copy of the prompt; the host dedupes.
- One process: every peer builds its floor at the same place and holds a copy of every hero and enemy; same-hero copies are set to ignore each other, but a follower stranded on a copy of the floor shows as a stuck/rescue finding.
- Loopback transport with captureFramerate 60: no latency, loss or jitter (those are the QA walks under NetCondition). The hash covers replicated state, not transforms or enemies.

## Host bot log

        0.0s F0: goal: hub stair at (0.0, 0.0, 9.0)
        3.0s F1: floor 1 seed 12345 preset Hub rooms 4 chests 6 locked False
        3.5s F1: goal: chest 5 at (254.0, 0.4, -50.0)
        9.0s F1: goal: chest 0 at (218.0, 0.4, -54.0)
       24.3s F1: goal: gate-key carrier at (206.0, 0.0, -18.0)
       38.7s F1: hurt (42/100): heading for the healing point at (234.0, 0.0, -10.0)
       45.2s F1: healed to 100/100
       45.2s F1: goal: piece 1 at (218.0, 0.6, -42.0)
       57.3s F1: goal: piece 0 at (206.0, 0.6, -46.0)
       59.7s F1: goal: stair at (254.0, 0.0, -14.0)
       77.5s F1: left floor 1 after 75 s
       77.5s F2: floor 2 seed 12345 preset Ring rooms 6 chests 7 locked True
       78.0s F2: goal: chest 4 at (234.0, 0.4, -46.0)
       83.5s F2: goal: chest 0 at (218.0, 0.4, -34.0)
       92.4s F2: goal: key chest 3 at (218.0, 0.4, -6.0)
      168.4s F2: goal: locked door at (244.0, 1.4, -30.0)
      177.0s F2: locked door opened: NavMesh re-baked
      177.0s F2: goal: piece 0 at (210.0, 0.6, -14.0)
      189.1s F2: goal: checkpoint bank at (230.0, 0.0, -42.0)
      222.3s F2: goal: stair at (254.0, 0.0, -54.0)
      233.4s F2: left floor 2 after 156 s
      233.4s F3: floor 3 seed 12345 preset Spine rooms 5 chests 7 locked True
      233.9s F3: goal: chest 1 at (226.0, 0.4, -38.0)
      269.9s F3: goal: chest 0 at (214.0, 0.4, -42.0)
      274.9s F3: goal: key chest 2 at (246.0, 0.4, -30.0)

## Follower logs

### follower 1

       82.0s: asked to bank on floor 2

### follower 2

       82.6s: asked to bank on floor 2

### follower 3

       82.6s: asked to bank on floor 2

