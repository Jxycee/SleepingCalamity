# Free humanoid animation sources for Sleeping Calamity (research, 2026-10-30)

Scope: free sources we could legally ship in a commercial Unity 6 URP game, beyond the already-planned Quaternius UAL1+UAL2 free tier and Mixamo. Research only; nothing downloaded or installed. Licence lines were checked on the source's own page where it could be reached; where it could not, the entry says so.

## Ranked table (best first)

| # | Source | Licence (as verified) | Commercial game? | Format / rig | Quality | Gaps it fills | Account / cost | URL |
|---|---|---|---|---|---|---|---|---|
| 1 | **Kevin Iglesias "FREE" packs** (Human Basic Motions, Archer, Spellcasting, Melee, Soldier, Crafting, Throwing, Dwarf) | Asset Store copies: Standard Unity Asset Store EULA (commercial use OK, no redistributing the raw files). The itch.io page says "royalty-free ... for commercial and personal projects", but the linked licence section on keviniglesias.com is **empty**, so take the Asset Store copy for the EULA | Yes | Unity package (Humanoid, FBX inside) + Blender sources; masc + fem versions; root motion + in-place | Hand-keyed, clean, game-ready, mildly stylised | Basic: walk/run 8-dir (incl. back/diagonals), **turn-in-place 90°/s L/R**, jump begin/land, fall loop. Archer: **bow shoot (front)**, sheathe/unsheathe, 6-dir **strafe run**. Spellcasting: enter/idle/exit cast + 5 spell attacks (**cast**). Melee: one **two-handed attack**, polearm attack (staff stand-in), strafe run 6-dir | Free (name your price); Unity ID for Asset Store | https://kevdev.itch.io (list), Asset Store publisher "Kevin Iglesias" |
| 2 | **100STYLE** (Ian Mason) | CC BY 4.0 (dataset page). Credit "The 100STYLE Dataset - Ian Mason" | Yes, with attribution | BVH, 60 fps, 28-bone skeleton; retargeted FBX/BVH on a skinned character by Daniel Holden (GitHub). Needs a Blender/MotionBuilder retarget to a Humanoid-friendly FBX, then imports cleanly | Xsens mocap, one actor, unedited; "Neutral" style plus 99 stylised gaits (Old, Proud, Angry...) | **Sidestep walk/run (L/R), backward walk/run**, forward walk/run, idle, transitions | None | https://www.ianxmason.com/100style/ |
| 3 | **CMU Graphics Lab mocap** (and RancidMilk's FBX conversion) | CMU terms (quoted on the RancidMilk page and the 4TU record): may be copied, modified, redistributed and included in commercially sold products; **may not resell the data directly, even converted**. Note the 4TU FBX record's citation metadata says CC BY-NC 4.0, which contradicts the text; use the original CMU / cgspeed BVH or RancidMilk instead. RancidMilk: "free to use/modify/redistribute so long as you don't sell the animations themselves" | Yes | BVH (cgspeed conversions); RancidMilk: 2,000+ FBX/glTF retargeted to a Quaternius CC0 rig, root motion, no fingers | Raw research mocap, realistic, some jitter; needs trimming, looping, foot cleanup | **Pick-up-from-ground**, turns, jumps, falls/lying down (raw material for knocked-down loop and get-up), misc locomotion | None (mocap.cs.cmu.edu cert errors in fetch) | http://mocap.cs.cmu.edu , https://rancidmilk.itch.io/free-character-animations |
| 4 | **Explosive free samples**: Warrior Pack Bundle 2 FREE (7 anims each for Knight, Mage, Archer, 2-Handed warrior) and RPG Character Mecanim Animation Pack FREE | Standard Unity Asset Store EULA (both listings) | Yes | Unity Mecanim, Humanoid | Hand-keyed, punchy, slightly stylised (fits our tone) | Small taste of **two-handed**, **mage (staff) cast**, **archer** sets; exact clip list not on the page | Free; Unity ID | https://assetstore.unity.com/packages/3d/animations/warrior-pack-bundle-2-free-42454 , https://assetstore.unity.com/packages/3d/animations/rpg-character-mecanim-animation-pack-free-65284 |
| 5 | **Rokoko 263 free mocap assets** | Rokoko page: use "in any animation, VFX, game ... including for commercial use". No separate licence text; redistribution not mentioned | Yes (per page) | FBX, 30 fps, exported on Mixamo, UE4/UE5 and HumanIK skeletons (Mixamo version retargets to Humanoid) | Mocap, realistic | Described as **magic-themed**: spell casts / area casts | Registration form (name, email, studio; marketing opt-in) | https://www.rokoko.com/resources/download-263-rokoko-motion-capture-assets |
| 6 | **Unity Starter Assets - Third Person** | Unity Companion License (use only with a Unity engine licence; fine for us). Listing also says "Non standard EULA"; read the bundled license.txt | Yes (Unity only) | Humanoid FBX | Hand-keyed, neutral | Only idle/walk/run/jump/land blend; nothing new over Quaternius | Free; Unity ID | Asset Store package 196526 |
| 7 | **Fab free listings under the Fab Standard License** | Epic docs: Standard License use "is not limited to Unreal Engine" (any engine). Epic's own UE-content (Game Animation Sample, Paragon) is **UE-only: unusable in Unity** | Per listing | Usually FBX on UE mannequin; retarget via Humanoid | Varies | Hunting ground for free combat packs (monthly free items); check each listing's licence tag | Epic account | https://www.fab.com |
| 8 | **Mocap Online free sampler** | Standard License: commercial use **capped at 1M end users / $1M revenue**; "AI Usage Permit" required for AI applications | Yes, under the cap | FBX, Unity, Unreal, Blender | Mocap | Idles, talking, basic movement only | Free; email delivery | https://mocaponline.com/products/free-mocap-animation-pack |
| 9 | **Truebones free mocap** | Royalty-free terms (2019): commercial incl. games; no redistributing the raw files; credit requested | Yes | FBX/BVH | Mocap, mostly creatures/animals | Little for humanoid gaps (useful for enemies) | Gumroad, free | https://truebones.gumroad.com |
| 10 | **Kenney / Max Parata / OpenGameArt CC0** | CC0 | Yes | GLB/FBX, tiny custom rigs (Kenney mini: 7 joints) | Simple hand-keyed | None of our gaps; rigs often fail Humanoid mapping | None | kenney.nl, https://maxparata.itch.io/cc0-animations |

## Unusable (licence)

- **Bandai Namco Research Motiondataset**: CC BY-NC 4.0 per the GitHub README (press reported BY-NC-ND). Non-commercial.
- **Ubisoft LaFAN1**: CC BY-NC-ND 4.0.
- **ActorCore free motions**: older terms required a separate mass-distribution licence for games; a July 2025 CG Channel update says Reallusion confirmed ActorCore motions can only be used inside AccuRig. Treat as unusable unless Reallusion confirms in writing.
- **Cascadeur Free plan**: non-commercial, exports .casc only (paid Indie tier needed).
- **DeepMotion free plan**: personal / non-commercial per third-party summaries; **Plask** free tier terms not confirmed (15 s/day). AI video-mocap, not libraries.
- **Unreal Game Animation Sample / Paragon / other Epic UE-only content**: Unreal Engine only.

## Gaps no free source fills well

- **Two-handed greatsword/axe combos and overhead finisher**: free sets give single 2H swings only (Kevin Melee, Explosive sample). Mixamo's Great Sword pack is the only free combo set; otherwise paid or hand-keyed.
- **Staff melee strikes**: only a polearm attack (Kevin) and Explosive's mage sample.
- **Longbow draw-hold aim and aim-walk**: Kevin Archer FREE has a front shot and strafe run, but no held-aim walk; would need an upper-body aim layer over strafe locomotion (Avatar Mask).
- **Launcher uppercut, jump/air attack**: nothing free beyond Mixamo singles; hand-key.
- **Side dodge / back step**: 100STYLE sidesteps are gaits, not dodges; Mixamo has dodges/rolls.
- **Knocked-down loop + get-up**: CMU raw falls need heavy cleanup; Mixamo is the practical free source.

## Practical notes

- For Kevin Iglesias, use the Asset Store copy: the itch FBX version caused a "Transform hierarchy does not match" Avatar error for a Unity user; the author points Unity users to the Asset Store package.
- Mocap sources (100STYLE, CMU, Rokoko) are realistic; our heroes are storybook-proportioned. Expect to push timing/poses (or blend with hand-keyed sets) to match the style.
- Keep attribution for 100STYLE (CC BY 4.0) and a courtesy credit for CMU, Kevin Iglesias and Truebones in the credits file.
