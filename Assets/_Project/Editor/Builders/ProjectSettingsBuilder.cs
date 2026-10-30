// Project-wide settings the foundation needs. Idempotent. It deliberately leaves the render pipeline and the Enter Play Mode options alone
// (art settings belong to the style bible work; play-mode options are the user's editor preference).
using System.Reflection;
using SleepingCalamity.Core;
using Unity.Netcode.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class ProjectSettingsBuilder
    {
        public static void Apply()
        {
            // Netcode first. Every later asset import or delete runs NGO's NetworkPrefabProcessor, which recreates
            // Assets/DefaultNetworkPrefabs.asset while GenerateDefaultNetworkPrefabs is still on.
            ApplyNetcode();
            ApplyLayers();
            PlayerSettings.runInBackground = true;   // MPPM clones and alt-tabbed hosts must keep ticking

            // The project owns its input asset: clear the project-wide actions entry, then delete the template asset.
            UnityEngine.InputSystem.InputSystem.actions = null;
            EditorBuildSettings.RemoveConfigObject(BuilderUtil.ProjectWideActionsConfigKey); // the setter skips this when already null
            DeleteIfPresent(AssetPaths.TemplateInputActions);
            DeleteIfPresent(AssetPaths.DefaultNetworkPrefabs);

            // API setters do not always mark these settings objects dirty; make sure SaveAssets writes them.
            MarkDirty(AssetPaths.TagManagerFile);
            MarkDirty(AssetPaths.ProjectSettingsFile);
            AssetDatabase.SaveAssets();
        }

        static void ApplyNetcode()
        {
            NetcodeForGameObjectsProjectSettings settings = NetcodeForGameObjectsProjectSettings.instance;
            settings.GenerateDefaultNetworkPrefabs = false;
            MethodInfo save = typeof(NetcodeForGameObjectsProjectSettings)
                .GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            if (save == null) throw new System.MissingMethodException("NetcodeForGameObjectsProjectSettings.SaveSettings not found (internal in NGO 2.13.3)");
            save.Invoke(settings, null); // writes ProjectSettings/NetcodeForGameObjects.asset
        }

        static void ApplyLayers()
        {
            Object[] objects = AssetDatabase.LoadAllAssetsAtPath(AssetPaths.TagManagerFile);
            if (objects.Length == 0) throw new System.InvalidOperationException("Cannot load " + AssetPaths.TagManagerFile);
            var tagManager = new SerializedObject(objects[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 0; i < Layers.UserLayerIndices.Length; i++)
                layers.GetArrayElementAtIndex(Layers.UserLayerIndices[i]).stringValue = Layers.UserLayerNames[i];
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void DeleteIfPresent(string assetPath)
        {
            bool known = AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
            if (!known && !System.IO.File.Exists(BuilderUtil.Abs(assetPath))) return;
            if (!AssetDatabase.DeleteAsset(assetPath)) throw new System.InvalidOperationException("Could not delete " + assetPath);
        }

        static void MarkDirty(string settingsPath)
        {
            Object[] objects = AssetDatabase.LoadAllAssetsAtPath(settingsPath);
            if (objects.Length == 0) throw new System.InvalidOperationException("Cannot load " + settingsPath);
            EditorUtility.SetDirty(objects[0]);
        }
    }
}
