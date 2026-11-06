// Presentation/Cellars/CellarObjectiveMarkers.cs: runtime greybox markers of the two floor objectives (plan 5 task 4): a valve wheel on a post, and the pump box with its ring. Authority: none (presentation).
// CellarContentsSync builds them on every peer for the built floor (no prefab, no NetworkObject, no scene YAML) and restyles them from the replicated ContentsState: a valve turns 90 degrees and goes
// green when its bit is set; the pump goes amber when started and green when done. The markers carry no colliders (a hero walks through the ring; the wheel is never a wall), and the wheel's
// working size is a 0.6 m disc (the plan's "cylinder 0.6 m wide on a post"). Colours are flat URP Lit fallbacks cached per colour.
using System.Collections.Generic;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public static class CellarObjectiveMarkers
    {
        public const string ValveName = "ObjectiveValve", PumpName = "ObjectivePump", WheelName = "Wheel", RingName = "PumpRing";
        public const float WheelDiameter = 0.6f;

        static readonly Color ValveShut = new Color(0.78f, 0.22f, 0.16f), ValveTurned = new Color(0.30f, 0.72f, 0.34f), Post = new Color(0.22f, 0.20f, 0.18f);
        static readonly Color PumpIdle = new Color(0.55f, 0.55f, 0.60f), PumpRunning = new Color(0.95f, 0.70f, 0.20f), PumpDone = new Color(0.30f, 0.72f, 0.34f);
        static readonly Dictionary<Color, Material> s_mats = new Dictionary<Color, Material>();

        static Material Mat(Color c)
        {
            if (s_mats.TryGetValue(c, out Material m) && m != null) return m;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh) { name = "CellarObjectiveFlat" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c; if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            s_mats[c] = m; return m;
        }

        static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color c)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = localPos; go.transform.localScale = localScale;
            Collider col = go.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
            return go;
        }

        // A valve at `worldPos` (the cell centre at floor height): a post and a wheel with a handle bar across it, under `parent`.
        public static GameObject BuildValve(Transform parent, Vector3 worldPos)
        {
            var root = new GameObject(ValveName); root.transform.SetParent(parent, false); root.transform.position = worldPos;
            Prim(root.transform, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 0.5f, 0.14f), Post);
            GameObject wheel = Prim(root.transform, WheelName, PrimitiveType.Cylinder, new Vector3(0f, 1.05f, 0f), new Vector3(WheelDiameter, 0.03f, WheelDiameter), ValveShut);
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // the disc stands upright, facing along the room's z axis
            Prim(wheel.transform, "Handle", PrimitiveType.Cube, Vector3.zero, new Vector3(1.0f, 3f, 0.1f), Post);   // a cross bar over the disc (the wheel's scale applies): it shows the turn
            SetValve(root, false);
            return root;
        }

        // Turned: the wheel is a quarter turn on and green.
        public static void SetValve(GameObject valve, bool turned)
        {
            if (valve == null) return;
            Transform wheel = valve.transform.Find(WheelName);
            if (wheel == null) return;
            wheel.localRotation = Quaternion.Euler(90f, turned ? 90f : 0f, 0f);
            wheel.GetComponent<MeshRenderer>().sharedMaterial = Mat(turned ? ValveTurned : ValveShut);
        }

        public static bool ValveShownTurned(GameObject valve)
        {
            if (valve == null) return false;
            Transform wheel = valve.transform.Find(WheelName);
            return wheel != null && Mathf.Abs(Mathf.DeltaAngle(wheel.localEulerAngles.y, 90f)) < 1f;
        }

        // The pump: a box with a stack, and a circle of `ringRadius` metres on the floor around it (a line loop).
        public static GameObject BuildPump(Transform parent, Vector3 worldPos, float ringRadius)
        {
            var root = new GameObject(PumpName); root.transform.SetParent(parent, false); root.transform.position = worldPos;
            Prim(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1.0f, 1.0f, 0.8f), PumpIdle);
            Prim(root.transform, "Stack", PrimitiveType.Cylinder, new Vector3(0.25f, 1.35f, 0f), new Vector3(0.22f, 0.35f, 0.22f), Post);
            var ring = new GameObject(RingName); ring.transform.SetParent(root.transform, false); ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            var line = ring.AddComponent<LineRenderer>();
            const int segments = 48;
            line.useWorldSpace = false; line.loop = true; line.positionCount = segments; line.widthMultiplier = 0.08f; line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * ringRadius, 0f, Mathf.Sin(a) * ringRadius));
            }
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            line.sharedMaterial = new Material(sh) { name = "CellarPumpRing" };
            SetPump(root, false, false);
            return root;
        }

        public static void SetPump(GameObject pump, bool started, bool done)
        {
            if (pump == null) return;
            Color c = done ? PumpDone : started ? PumpRunning : PumpIdle;
            Transform body = pump.transform.Find("Body");
            if (body != null) body.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
            Transform ring = pump.transform.Find(RingName);
            LineRenderer line = ring != null ? ring.GetComponent<LineRenderer>() : null;
            if (line != null)
            {
                line.startColor = line.endColor = c;
                Material m = line.sharedMaterial;
                if (m != null) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); m.color = c; }
            }
        }

        public static Color PumpColorOf(GameObject pump)
        {
            Transform body = pump != null ? pump.transform.Find("Body") : null;
            return body != null ? body.GetComponent<MeshRenderer>().sharedMaterial.color : Color.clear;
        }
    }
}
