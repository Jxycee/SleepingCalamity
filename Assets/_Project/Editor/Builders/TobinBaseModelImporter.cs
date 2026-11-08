// Import settings for the MetaPerson-based Tobin base model (TobinBase.fbx), applied by path so nobody edits the .meta by hand.
// Humanoid, avatar created from this model with Unity's automap (no manual bone fixes), blendshapes on.
// globalScale 1.0425 takes the 1.8225 m MetaPerson avatar to Tobin's 1.90 m (the FBX itself carries no scale).
using UnityEditor;

namespace SleepingCalamity.EditorTools
{
    public sealed class TobinBaseModelImporter : AssetPostprocessor
    {
        public const string ModelPath = "Assets/_Project/Content/Characters/TobinBase/TobinBase.fbx";
        public const float GlobalScale = 1.0425f;

        public override uint GetVersion() => 2;   // bump to reimport the model when these settings change

        void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = GlobalScale;
            mi.useFileScale = false;
            mi.importBlendShapes = true;
            mi.importBlendShapeNormals = ModelImporterNormals.Import;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.isReadable = true;   // BakeMesh and vertex checks in tests
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            FixHeadMapping(mi);
        }

        // Approved exception (user, 2026-11-08): automap puts the Head slot on Neck2 and leaves bone Head unmapped; Neck1 and Neck2 stay unmapped.
        static bool FixHeadMapping(ModelImporter mi)
        {
            var d = mi.humanDescription;
            if (d.human == null || d.human.Length == 0) return false;
            bool changed = false;
            for (int i = 0; i < d.human.Length; i++)
                if (d.human[i].humanName == "Head" && d.human[i].boneName != "Head") { d.human[i].boneName = "Head"; changed = true; }
            if (changed) mi.humanDescription = d;
            return changed;
        }

        // First import has no description to fix in OnPreprocessModel, so fix it once automap has produced one and reimport.
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (path != ModelPath) continue;
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                if (mi != null && FixHeadMapping(mi)) mi.SaveAndReimport();
            }
        }
    }
}
