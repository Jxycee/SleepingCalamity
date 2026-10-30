// Import settings for every animation FBX under Content/Animation, applied from code (never by hand in the inspector) and idempotent: an importer is only
// re-imported when a setting actually differs, and the resulting .meta files are committed.
//   Quaternius UAL1 + UAL2 (CC0)   Humanoid, avatar from the file's own mannequin, Bake Axis Conversion on, every clip named *_Loop loops. The non-RM files:
//                                   root motion is already off in them, and the hero is driven by its CharacterController anyway.
//   Mixamo                          YBot.fbx (with skin) is the hero's mannequin and avatar source. The without-skin clips copy its avatar.
//   Kevin Iglesias                  the package's own .meta files came across (each clip file is a Humanoid with its own avatar and the author's loop
//                                   flags); this only checks they are still Humanoid.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class AnimationImportBuilder
    {
        public const string Root = "Assets/_Project/Content/Animation";
        public const string QuaterniusFolder = Root + "/Quaternius", MixamoFolder = Root + "/Mixamo", KevinFolder = Root + "/KevinIglesias";
        public const string Ual1 = QuaterniusFolder + "/UAL1_Standard.fbx", Ual2 = QuaterniusFolder + "/UAL2_Standard.fbx";
        public const string YBot = MixamoFolder + "/YBot.fbx";

        public static void Build()
        {
            BuilderUtil.EnsureFolder(Root);
            bool changed = false;
            changed |= ConfigureQuaternius(Ual1);
            changed |= ConfigureQuaternius(Ual2);
            changed |= ConfigureMixamo();
            changed |= VerifyKevin();
            if (changed) AssetDatabase.Refresh();
        }

        static ModelImporter Importer(string path)
        {
            if (!File.Exists(BuilderUtil.Abs(path))) return null;
            return AssetImporter.GetAtPath(path) as ModelImporter;
        }

        static bool ConfigureQuaternius(string path)
        {
            ModelImporter mi = Importer(path);
            if (mi == null) { Debug.LogWarning("SC ANIM missing " + path); return false; }
            bool dirty = false;
            if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; dirty = true; }
            if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
            if (!mi.bakeAxisConversion) { mi.bakeAxisConversion = true; dirty = true; }
            if (!mi.importAnimation) { mi.importAnimation = true; dirty = true; }
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
            dirty |= ApplyClips(mi, name => name.EndsWith("_Loop"));
            return Commit(mi, path, dirty);
        }

        static bool ConfigureMixamo()
        {
            bool changed = false;
            Avatar source = null;
            ModelImporter ybot = Importer(YBot);
            if (ybot != null)
            {
                bool dirty = false;
                if (ybot.animationType != ModelImporterAnimationType.Human) { ybot.animationType = ModelImporterAnimationType.Human; dirty = true; }
                if (ybot.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { ybot.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
                if (ybot.materialImportMode != ModelImporterMaterialImportMode.None) { ybot.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
                changed |= Commit(ybot, YBot, dirty);
                source = AssetDatabase.LoadAllAssetsAtPath(YBot).OfType<Avatar>().FirstOrDefault();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MixamoFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == YBot) continue;
                ModelImporter mi = Importer(path);
                if (mi == null) continue;
                bool dirty = false;
                if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; dirty = true; }
                if (source != null && source.isValid && source.isHuman)
                {
                    if (mi.avatarSetup != ModelImporterAvatarSetup.CopyFromOther) { mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; dirty = true; }
                    if (mi.sourceAvatar != source) { mi.sourceAvatar = source; dirty = true; }
                }
                else if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
                if (!mi.importAnimation) { mi.importAnimation = true; dirty = true; }
                if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
                dirty |= ApplyClips(mi, LoopsByName);
                changed |= Commit(mi, path, dirty);
            }
            return changed;
        }

        // "Warrior Idle" loops; "Standing Idle To Action Idle" (a transition) does not.
        static bool LoopsByName(string clipName)
        {
            string n = clipName.ToLowerInvariant();
            if (n.Contains(" to ") || n.Contains("_to_") || n.Contains("transition")) return false;
            return n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("jog") || n.Contains("strafe") || n.Contains("loop");
        }

        static bool VerifyKevin()
        {
            bool changed = false;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { KevinFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter mi = Importer(path);
                if (mi == null || mi.animationType == ModelImporterAnimationType.Human) continue;
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.SaveAndReimport();
                changed = true;
            }
            return changed;
        }

        // Sets loopTime / loopPose per clip, and bakes the root rotation and height into the pose so a clip never drifts the visual off its parent.
        // Returns true when the importer's clip list changed.
        static bool ApplyClips(ModelImporter mi, System.Func<string, bool> loops)
        {
            ModelImporterClipAnimation[] defaults = mi.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0) return false;   // not imported yet: the next Build picks it up
            ModelImporterClipAnimation[] current = mi.clipAnimations;
            var next = new ModelImporterClipAnimation[defaults.Length];
            bool dirty = current == null || current.Length != defaults.Length;
            for (int i = 0; i < defaults.Length; i++)
            {
                ModelImporterClipAnimation clip = defaults[i];
                ModelImporterClipAnimation old = null;
                if (current != null) foreach (ModelImporterClipAnimation c in current) if (c.takeName == clip.takeName) { old = c; break; }
                clip.loopTime = loops(clip.name);
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                if (old == null || old.loopTime != clip.loopTime || old.lockRootRotation != clip.lockRootRotation || old.lockRootHeightY != clip.lockRootHeightY
                    || old.lockRootPositionXZ != clip.lockRootPositionXZ || old.name != clip.name) dirty = true;
                next[i] = clip;
            }
            if (dirty) mi.clipAnimations = next;
            return dirty;
        }

        static bool Commit(ModelImporter mi, string path, bool dirty)
        {
            if (!dirty) return false;
            mi.SaveAndReimport();
            Debug.Log("SC ANIM imported " + path);
            return true;
        }
    }
}
