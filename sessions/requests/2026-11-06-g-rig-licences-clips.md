Title: Rigged assets: licence checks + townsfolk clip map
Target: cloud
Status: pending
Model: sonnet
From: coordinator session 10 (QUEUE.md "Ready: rigged characters and animals", two S items, no Unity)
Status file: Reference/sessions/rig-licences-clips/STATUS.md

Brief:
Read first: docs/research/2026-11-06-open-rigged-assets.md (esp. "Still to verify" and "Restricted licences").
Licence bar (user, 2026-11-06): personal use. Non-commercial / territory / MAU-limited assets and models are acceptable but must be recorded with `licence: personal-use` in the asset source record so they can be swapped before any sale. Dark Cloud IP rule unchanged (no names, characters, text, music, assets).
1. Verify licences from the primary sources (web): Quaternius QAL (Bestiary kit), UAL2 wording, Ultimate Animated Animals roster, MPFB2 rig options, Mixamo FAQ, UniRig skinning release. For each: licence name, link, what it allows (personal / commercial / redistribution in a game build), attribution needed, and our tag (cc0 / cc-by / personal-use). Update the research note's "Still to verify" section in place.
2. Townsfolk idle-work loops: map UAL2 farming/fishing/carry clips (and other free packs from the note) to baker, smith, innkeeper and the Saltwake fisherman; list the gaps to generate later with HY-Motion (Tencent HF Space) or MoMask locally. Write docs/research/2026-11-06-townsfolk-clip-map.md.
Docs only. One Sonnet reviewer pass, then merge into fresh origin/main (merge commit, no PR, never force-push; no Unity check for docs). Never edit CURRENT.md or tick QUEUE.md. Report via STATUS.md.
Done when: merged, STATUS DONE with merge commit, Archive: ready.
