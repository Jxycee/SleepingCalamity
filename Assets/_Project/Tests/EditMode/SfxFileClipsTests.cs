using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    // The recorded CC0 clips (Kenney, docs/audio/SOURCES.md) replace the generated combat and UI cues by file name = cue id; cues with no file stay generated.
    // Catches: a file whose name is not a cue id (silently unused), a loader that stops replacing, a replaced cue that loses its generated fallback list entry.
    public class SfxFileClipsTests
    {
        private static readonly string[] WithFile =
        {
            CombatSfx.Hit, CombatSfx.HitHeavy, CombatSfx.HitCrit, CombatSfx.Whoosh, CombatSfx.Block, CombatSfx.Parry, CombatSfx.Slam, CombatSfx.Hurt, CombatSfx.Pop,
            CombatSfx.Heal, CombatSfx.Spell, CombatSfx.Shot, CombatSfx.Dodge, CombatSfx.Telegraph, CombatSfx.Die, SfxIds.UiClick, SfxIds.UiConfirm, SfxIds.UiError,
        };
        private static readonly string[] StayGenerated = { SfxIds.WindAmbient, SfxIds.WaterLap, SfxIds.BellRing, CombatSfx.Whiff };

        [Test]
        public void ApplyFileClips_ReplacesEveryCueThatHasAFile_AndLeavesTheRestGenerated()
        {
            var gen = new SfxGenerator();
            gen.GenerateAll(1234UL);
            int replaced = gen.ApplyFileClips();
            Assert.That(replaced, Is.EqualTo(WithFile.Length), "every file in Resources/Sfx is named like a cue id, and every listed cue has its file");

            foreach (string id in WithFile)
            {
                Assert.That(gen.TryGetClip(id, out AudioClip clip), Is.True, id);
                Assert.That(clip, Is.SameAs(Resources.Load<AudioClip>(SfxGenerator.ClipFolder + "/" + id)), id + " plays the recorded clip");
                Assert.That(clip.length, Is.GreaterThan(0.03f), id + " has sound in it");
            }
            foreach (string id in StayGenerated)
            {
                Assert.That(gen.TryGetClip(id, out AudioClip clip), Is.True, id);
                Assert.That(Resources.Load<AudioClip>(SfxGenerator.ClipFolder + "/" + id), Is.Null, id + " has no file");
                Assert.That(clip.name, Is.EqualTo(id));
            }
        }
    }
}
