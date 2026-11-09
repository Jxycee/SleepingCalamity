# STATUS: face-template-spike (branch claude/face-template-spike, from claude/pensive-clarke-e25tku)
Updated 2026-11-08. Not merged; CURRENT.md untouched.

State: DONE (spike). Sheet C:/AI/test_out/heroes/face_spike/out/face2_proof_sheet.png; doc section 8 written. Verdict: route sound, not yet tool-worthy (target quality, wrap weighting, texture). Unity gate not run.
Data (outside repo): C:/AI/test_out/heroes/face_spike/{template,target,fit,bake,out}
Target: SDXL (Gemini not signed in) bald dark-skinned adult male, front only, Hunyuan 40k + TRELLIS tex (blotchy colours, no eye cavities).
Fit (face-skin RMS/P95 mm): basis 2.05/4.47, sliders 1.90/4.18, wrap r3 0.91/1.60. Sliders add little; ICP+landmarks did most.
Hard stop: lips pass; eyeBlinkLeft mean lid gap 1.205 mm vs limit 1.197 (template itself ~2 mm max, literal 0.5 mm gate unmeetable). Wrap copies head/jaw shape, not brow/nose/lip identity.
Next: read agent report, view sheet, write doc section in docs/research/2026-11-08-character-pipeline.md (what worked/broke/tool verdict), MPFB2 note, Unity gate only if sheet good.
MPFB2: docs say core assets (base mesh, targets, skins) CC0, add-on code GPLv3 (https://static.makehumancommunity.org/mpfb/faq/use_in_closed_source.html, https://extensions.blender.org/add-ons/mpfb/). faceunits/ARKit-like set in repo NOT verified.
Archive: ready
