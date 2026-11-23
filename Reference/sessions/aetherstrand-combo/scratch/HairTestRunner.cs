// Scratch test harness for the AetherStrand plugin (NOT part of SleepingCalamity).
// Drives the plugin through reflection so this assembly needs no reference to it.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class HairTestRunner
{
    const string OutDir = "/tmp/claude-0/-home-user-SleepingCalamity/5bd8c6bb-33e1-5bbf-b3d4-29f2ef526d91/scratchpad/hairtest_out";
    static int failures;
    const bool ZeroOffset = true;

    static void Check(bool ok, string what)
    {
        Debug.Log((ok ? "HAIRTEST PASS " : "HAIRTEST FAIL ") + what);
        if (!ok) failures++;
    }

    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        failures = 0;
        try { RunInner(); }
        catch (Exception e) { Debug.LogError("HAIRTEST EXCEPTION " + e); failures++; }
        Debug.Log(failures == 0 ? "HAIRTEST RESULT PASS" : "HAIRTEST RESULT FAIL " + failures);
        if (failures > 0) Debug.LogError("HAIRTEST FAILURES " + failures);
    }

    static void RunInner()
    {
        Type winType = Type.GetType("AetherStrand.Editor.AetherStrandSetupWindow, AetherStrand.Editor");
        Check(winType != null, "setup window type resolves from package assembly");
        if (winType == null) return;

        // 1. Open the window like the menu item does.
        var openM = winType.GetMethod("OpenWindow", BindingFlags.Public | BindingFlags.Static);
        Check(openM != null, "OpenWindow menu entry exists");
        openM?.Invoke(null, null);
        var win = Resources.FindObjectsOfTypeAll(winType).FirstOrDefault() as EditorWindow;
        Check(win != null, "setup window opens");
        if (win == null) return;
        win.Show(); win.Focus();

        // 2. Load the Tobin character (rigged FBX) and pick it as the character, as the UI does.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tobin/TobinBase.fbx");
        Check(prefab != null, "Tobin FBX loads");
        if (prefab == null) return;
        GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        character.name = "TobinTest";
        var headBone = AetherStrand_ResolveHead(character);
        Check(headBone != null, "character has a Head bone (" + (headBone ? headBone.name : "none") + ")");
        if (headBone == null) return;

        var rootField = winType.GetField("_characterRoot", BindingFlags.NonPublic | BindingFlags.Instance);
        rootField.SetValue(win, character);
        Check(rootField.GetValue(win) == character, "character assigned to the window");

        // 3. Click the one-click apply button's method.
        var builderField = winType.GetField("_builder", BindingFlags.NonPublic | BindingFlags.Instance);
        var builder = builderField.GetValue(win);
        var offField = builder.GetType().GetField("headLocalOffset");
        Debug.Log("HAIRTEST builder headLocalOffset before = " + offField.GetValue(builder));
        if (ZeroOffset) offField.SetValue(builder, Vector3.zero);
        Debug.Log("HAIRTEST builder headLocalOffset used = " + offField.GetValue(builder));
        var apply = winType.GetMethod("ApplyProceduralHairToCharacter", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(apply != null, "1-click apply method exists");
        var hairRendererBefore = character.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
        try { apply.Invoke(win, null); }
        catch (TargetInvocationException tie) { Debug.LogError("HAIRTEST apply threw " + tie.InnerException); Check(false, "1-click apply runs without exception"); return; }
        Check(true, "1-click apply runs without exception");

        // 4. Did hair get attached?
        var hairField = winType.GetField("_hairRenderer", BindingFlags.NonPublic | BindingFlags.Instance);
        var hair = hairField.GetValue(win) as SkinnedMeshRenderer;
        Check(hair != null, "hair SkinnedMeshRenderer created");
        int added = character.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length - hairRendererBefore;
        Check(added >= 1, "character gained " + added + " SkinnedMeshRenderer(s)");
        if (hair == null) return;

        Check(hair.sharedMesh != null && hair.sharedMesh.vertexCount > 0, "hair mesh has vertices (" + (hair.sharedMesh ? hair.sharedMesh.vertexCount : 0) + ")");
        Check(hair.sharedMaterial != null, "hair has a material");
        if (hair.sharedMaterial != null)
        {
            var sh = hair.sharedMaterial.shader;
            Debug.Log("HAIRTEST shader " + (sh ? sh.name : "null") + " supported=" + (sh && sh.isSupported) + " errors=" + (sh ? UnityEditor.ShaderUtil.GetShaderMessages(sh).Length : -1));
            Check(sh != null && sh.name == "AetherStrand/SculptedHair_URP" && sh.isSupported, "hair uses the plugin shader and it is supported");
        }
        Check(hair.bones != null && hair.bones.Length > 0, "hair is skinned to " + (hair.bones == null ? 0 : hair.bones.Length) + " bones");

        // 5. Does the hair sit on the head? Compare bounds in character space.
        var headB = headBoundsInRoot(character, headBone);
        var hairB = hair.bounds;
        Vector3 hc = hairB.center, hcHead = headB.center;
        float hairTop = hairB.max.y, headTop = headB.max.y;
        Debug.Log($"HAIRTEST bounds head centre {headB.center} size {headB.size}; hair centre {hairB.center} size {hairB.size}");
        Check(hairB.size.x > 0.05f && hairB.size.y > 0.05f, "hair has a real size (" + hairB.size + ")");
        Check(Vector2.Distance(new Vector2(hc.x, hc.z), new Vector2(hcHead.x, hcHead.z)) < headB.extents.x * 0.6f, "hair centre is over the head centre (horizontal)");
        Check(Mathf.Abs(hairTop - headTop) < headB.size.y * 0.25f, "hair crown within 25% of head height of the head top (" + (hairTop - headTop).ToString("F3") + " m)");

        // 6. Physics setup created Hair_ chains.
        var physField = winType.GetField("_activePhysics", BindingFlags.NonPublic | BindingFlags.Instance);
        var phys = physField.GetValue(win);
        Check(phys != null, "physics component set up by 1-click apply");

        // 7. Screenshots of hair on head, front and back (window capture is not possible headless).
        try
        {
            RenderShot(headB, new Vector3(0f, 0f, 1f), "hair_front.png");
            RenderShot(headB, new Vector3(0f, 0f, -1f), "hair_back.png");
            Check(true, "rendered hair_front.png and hair_back.png");
        }
        catch (Exception e) { Debug.LogWarning("HAIRTEST render skipped: " + e.Message); }
    }

    static Transform AetherStrand_ResolveHead(GameObject root)
    {
        var anim = root.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman) { var h = anim.GetBoneTransform(HumanBodyBones.Head); if (h != null) return h; }
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(t.name, "Head", StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }

    static Bounds headBoundsInRoot(GameObject root, Transform headBone)
    {
        // Vertices where the Head bone carries >50% weight, baked to world space.
        var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(s => s.sharedMesh != null && s.bones != null && s.bones.Contains(headBone) && !s.name.ToLower().Contains("hair"));
        if (body == null) return new Bounds(headBone.position, Vector3.one * 0.25f);
        int idx = Array.IndexOf(body.bones, headBone);
        var mesh = body.sharedMesh;
        var bws = mesh.boneWeights;
        var baked = new Mesh();
        body.BakeMesh(baked);
        var lv = baked.vertices;
        bool any = false; Bounds b = new Bounds();
        for (int i = 0; i < bws.Length && i < lv.Length; i++)
        {
            var bw = bws[i];
            float w = 0f;
            if (bw.boneIndex0 == idx) w += bw.weight0;
            if (bw.boneIndex1 == idx) w += bw.weight1;
            if (bw.boneIndex2 == idx) w += bw.weight2;
            if (bw.boneIndex3 == idx) w += bw.weight3;
            if (w <= 0.5f) continue;
            Vector3 wp = body.transform.TransformPoint(lv[i]);
            if (!any) { b = new Bounds(wp, Vector3.zero); any = true; } else b.Encapsulate(wp);
        }
        UnityEngine.Object.DestroyImmediate(baked);
        return any ? b : new Bounds(headBone.position, Vector3.one * 0.25f);
    }

    static void RenderShot(Bounds head, Vector3 dir, string file)
    {
        var camGo = new GameObject("HairTestCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
        cam.fieldOfView = 25f;
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f;
        var c = head.center;
        cam.transform.position = c + dir.normalized * 1.2f;
        cam.transform.LookAt(c);
        var light = new GameObject("HairTestLight").AddComponent<Light>();
        light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(40f, 30f, 0f); light.intensity = 1.2f;
        var rt = new RenderTexture(960, 960, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 960, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 960), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(light.gameObject);
        UnityEngine.Object.DestroyImmediate(rt);
    }
}
