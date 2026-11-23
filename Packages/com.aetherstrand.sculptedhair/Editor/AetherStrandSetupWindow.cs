#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AetherStrand.Editor
{
    /// <summary>
    /// AetherStrand Hair Studio v2.5 (Unity 6 / URP 17.6):
    /// - Embedded interactive 3D Viewport (PreviewRenderUtility): drag to orbit, scroll to zoom,
    ///   camera presets (Hero/Front/Left/Right/Back), and 4-Up Turnaround PNG capture for AI visual eval.
    /// - All 5 Hero Presets + 14 Procedural Archetypes (including Coiled Afro & Taper Fade) with live sliders.
    /// - Interactive 3D Scene-View Fit Handle on the character's head (no typing offset numbers).
    /// - Full headless batchmode static API preserved 100% compatible for automated builders.
    /// </summary>
    public class AetherStrandSetupWindow : EditorWindow
    {
        [Serializable]
        private class RecipeRoot
        {
            public string schemaVersion;
            public string presetId;
            public RecipeShader shader;
            public RecipePhysics physics;
        }

        [Serializable]
        private class RecipeShader
        {
            public string rootColor, baseColor, tipColor, primarySpecColor, secondarySpecColor;
            public float primaryShift, primaryPower, primaryIntensity;
            public float secondaryShift, secondaryPower, secondaryIntensity;
            public float rimIntensity, rimPower, rootAO, grooveDepth, strandVariation;
        }

        [Serializable]
        private class RecipePhysics
        {
            public float stiffness, gravity, windStrength, windTurbulence, windAzimuth;
            public float damping, inertia, bendingSmoothness, collisionMarginMm, rootAnchor;
            public int solverSubsteps;
        }

        // Embedded 3D Preview
        private PreviewRenderUtility _preview;
        private Mesh _previewHairMesh;
        private Mesh _previewBustMesh;
        private Material _previewBustMat;
        private Material _previewHairMat;
        private Vector2 _orbit = new Vector2(18f, 10f);
        private float _zoom = 0.52f;
        private bool _previewDirty = true;

        // Studio State
        private int _tabIndex;
        private int _activePresetIndex;
        private Vector2 _scroll;
        private HairBuilderState _builder = new HairBuilderState();
        private bool _sceneFitHandleActive;

        // Character Binding
        private GameObject _characterRoot;
        private Renderer _hairRenderer;
        private Material _activeMaterial;
        private AetherStrandPhysics _activePhysics;
        private string _bonePrefix = "Hair_";
        private string _materialOutputPath = "Assets/AetherStrand_Hair_Mat.mat";
        private string _meshOutputPath = "Assets/AetherStrand_Hair_Mesh.asset";
        private string _presetJsonPath = "";
        private float _headHeightOverride = 0f;
        private PhysicsAuditResult? _lastAudit;

        [MenuItem("Tools/AetherStrand/Hair Studio Setup")]
        public static void OpenWindow()
        {
            var win = GetWindow<AetherStrandSetupWindow>("AetherStrand Hair Studio");
            win.minSize = new Vector2(520, 680);
        }

        private void OnEnable()
        {
            InitPreview();
            ApplyHeroPreset(0, regenerateCharacter: false);
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DisposePreview();
        }

        private void InitPreview()
        {
            if (_preview != null) return;
            _preview = new PreviewRenderUtility();
            _preview.cameraFieldOfView = 32f;
            _preview.camera.nearClipPlane = 0.02f;
            _preview.camera.farClipPlane = 10f;
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.14f, 0.15f, 0.18f, 1f);

            _preview.lights[0].intensity = 1.35f;
            _preview.lights[0].color = new Color(1.0f, 0.96f, 0.92f);
            _preview.lights[0].transform.rotation = Quaternion.Euler(32f, -35f, 0f);
            _preview.lights[1].intensity = 0.65f;
            _preview.lights[1].color = new Color(0.75f, 0.85f, 1.0f);
            _preview.lights[1].transform.rotation = Quaternion.Euler(-15f, 145f, 0f);

            // Stylized dark-umber mannequin bust sphere for clear contrast behind any hair color
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _previewBustMesh = Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            DestroyImmediate(tmp);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _previewBustMat = new Material(lit);
            if (_previewBustMat.HasProperty("_BaseColor")) _previewBustMat.SetColor("_BaseColor", new Color(0.24f, 0.18f, 0.15f, 1f));
            if (_previewBustMat.HasProperty("_Smoothness")) _previewBustMat.SetFloat("_Smoothness", 0.18f);

            Shader hairSh = Shader.Find("AetherStrand/SculptedHair_URP") ?? lit;
            _previewHairMat = new Material(hairSh);
            TryBindTexture(_previewHairMat, "_GrooveAoMap", "AetherStrand_GrooveAO");
            TryBindTexture(_previewHairMat, "_FlowIdMap", "AetherStrand_FlowIDShift");
            TryBindTexture(_previewHairMat, "_NormalMap", "AetherStrand_Normal");
            TryBindTexture(_previewHairMat, "_AlphaMap", "AetherStrand_AlphaCard");
            _previewHairMat.EnableKeyword("_USE_ALPHA_CLIP");
            _previewHairMat.SetFloat("_UseAlphaClip", 1f);
            _previewHairMat.SetFloat("_AlphaClip", 0.34f);
            _previewDirty = true;
        }

        private void DisposePreview()
        {
            if (_previewHairMesh != null) { DestroyImmediate(_previewHairMesh); _previewHairMesh = null; }
            if (_previewBustMesh != null) { DestroyImmediate(_previewBustMesh); _previewBustMesh = null; }
            if (_previewBustMat != null) { DestroyImmediate(_previewBustMat); _previewBustMat = null; }
            if (_previewHairMat != null) { DestroyImmediate(_previewHairMat); _previewHairMat = null; }
            if (_preview != null) { _preview.Cleanup(); _preview = null; }
        }

        private void RebuildPreviewMesh()
        {
            if (_previewHairMesh != null) DestroyImmediate(_previewHairMesh);
            var locks = AetherStrandProceduralBuilder.BuildLocks(_builder);
            _previewHairMesh = AetherStrandProceduralBuilder.BuildPreviewMesh(locks);
            _previewDirty = false;
        }

        private void OnGUI()
        {
            if (_preview == null) InitPreview();
            if (_previewDirty) RebuildPreviewMesh();

            DrawViewportHeader();
            DrawInteractive3DViewport(new Rect(8, 30, position.width - 16, 260));
            GUILayout.Space(268);

            // Character Bar + 1-Click Apply
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            _characterRoot = (GameObject)EditorGUILayout.ObjectField("Character", _characterRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && _characterRoot != null) SyncReferencesFromCharacter();

            using (new EditorGUI.DisabledScope(_characterRoot == null))
            {
                _sceneFitHandleActive = GUILayout.Toggle(_sceneFitHandleActive, "Fit Gizmo", "Button", GUILayout.Width(72));
                GUI.backgroundColor = new Color(0.95f, 0.76f, 0.28f);
                if (GUILayout.Button("Apply to Character", GUILayout.Width(135)))
                {
                    ApplyProceduralHairToCharacter();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            _tabIndex = GUILayout.Toolbar(_tabIndex, new[] { "Presets", "Hair Builder", "Color & Shader", "Physics & Audit", "AI Debug & Export" });
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tabIndex)
            {
                case 0: DrawPresetsTab(); break;
                case 1: DrawBuilderTab(); break;
                case 2: DrawShaderTab(); break;
                case 3: DrawPhysicsTab(); break;
                case 4: DrawBatchEvalTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawViewportHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("AetherStrand Hair Studio", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Hero", EditorStyles.toolbarButton, GUILayout.Width(42)))  { _orbit = new Vector2(22f, 10f); _zoom = 0.52f; Repaint(); }
            if (GUILayout.Button("Front", EditorStyles.toolbarButton, GUILayout.Width(44))) { _orbit = new Vector2(0f, 6f);   _zoom = 0.50f; Repaint(); }
            if (GUILayout.Button("Left", EditorStyles.toolbarButton, GUILayout.Width(38)))  { _orbit = new Vector2(-90f, 6f); _zoom = 0.52f; Repaint(); }
            if (GUILayout.Button("Right", EditorStyles.toolbarButton, GUILayout.Width(42))) { _orbit = new Vector2(90f, 6f);  _zoom = 0.52f; Repaint(); }
            if (GUILayout.Button("Back", EditorStyles.toolbarButton, GUILayout.Width(42)))  { _orbit = new Vector2(180f, 8f); _zoom = 0.52f; Repaint(); }
            if (GUILayout.Button("4-Up PNG", EditorStyles.toolbarButton, GUILayout.Width(66)))
            {
                string outPng = "Assets/AetherStrand_Turnaround_4Up.png";
                Capture4UpTurnaroundPng(outPng);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(outPng));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInteractive3DViewport(Rect rect)
        {
            Event e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    _orbit.x += e.delta.x * 0.7f;
                    _orbit.y = Mathf.Clamp(_orbit.y + e.delta.y * 0.5f, -45f, 65f);
                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    _zoom = Mathf.Clamp(_zoom + e.delta.y * 0.025f, 0.28f, 1.10f);
                    e.Use();
                    Repaint();
                }
            }

            if (e.type == EventType.Repaint && rect.width > 16 && rect.height > 16)
            {
                RenderPreviewToRect(rect, _orbit, _zoom);
                GUI.Label(new Rect(rect.x + 8, rect.y + rect.height - 22, 340, 20),
                    "Drag to orbit  |  Scroll to zoom  |  " + (_previewHairMesh != null ? $"{_previewHairMesh.triangles.Length / 3:N0} tris" : ""),
                    EditorStyles.miniLabel);
            }
        }

        private void RenderPreviewToRect(Rect rect, Vector2 orbitDeg, float dist)
        {
            _preview.BeginPreview(rect, GUIStyle.none);
            Vector3 target = new Vector3(0f, 0.105f, 0.0f);
            Quaternion q = Quaternion.Euler(orbitDeg.y, orbitDeg.x + 180f, 0f);
            _preview.camera.transform.position = target + q * (Vector3.back * dist);
            _preview.camera.transform.LookAt(target, Vector3.up);

            // Stylized head + neck mannequin bust
            Matrix4x4 skullMat = Matrix4x4.TRS(new Vector3(0f, 0.112f, 0.004f), Quaternion.identity, new Vector3(0.180f, 0.212f, 0.200f));
            Matrix4x4 neckMat  = Matrix4x4.TRS(new Vector3(0f, -0.025f, -0.012f), Quaternion.identity, new Vector3(0.085f, 0.120f, 0.088f));
            _preview.DrawMesh(_previewBustMesh, skullMat, _previewBustMat, 0);
            _preview.DrawMesh(_previewBustMesh, neckMat, _previewBustMat, 0);

            if (_previewHairMesh != null && _previewHairMat != null)
            {
                _preview.DrawMesh(_previewHairMesh, Matrix4x4.identity, _previewHairMat, 0);
            }

            _preview.camera.Render();
            Texture tex = _preview.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        private void DrawPresetsTab()
        {
            GUILayout.Label("Hero Presets (1-Click Style + Color + Physics)", EditorStyles.boldLabel);
            var presets = AetherStrandProceduralBuilder.HeroPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                var p = presets[i];
                bool selected = i == _activePresetIndex;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Color swatch chip
                if (ColorUtility.TryParseHtmlString(p.BaseHex, out Color sw))
                {
                    Rect r = GUILayoutUtility.GetRect(24, 28, GUILayout.Width(24));
                    EditorGUI.DrawRect(r, sw);
                }

                EditorGUILayout.BeginVertical();
                GUILayout.Label(p.Name, EditorStyles.boldLabel);
                GUILayout.Label(p.Subtitle, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                GUI.backgroundColor = selected ? new Color(0.95f, 0.76f, 0.28f) : Color.white;
                if (GUILayout.Button(selected ? "Active" : "Load", GUILayout.Width(68), GUILayout.Height(28)))
                {
                    ApplyHeroPreset(i, regenerateCharacter: false);
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawBuilderTab()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Randomize Shape"))
            {
                _builder.seed = (_builder.seed * 1103515245 + 12345) & 0x7FFFFFF;
                _builder.partPosition = Mathf.Lerp(-0.7f, 0.7f, ((_builder.seed >> 4) % 100) / 100f);
                _builder.bangsLength = Mathf.Lerp(0.45f, 0.80f, ((_builder.seed >> 8) % 100) / 100f);
                _builder.crownVolume = Mathf.Lerp(0.30f, 0.80f, ((_builder.seed >> 12) % 100) / 100f);
                _builder.spikeLift = Mathf.Lerp(0.08f, 0.85f, ((_builder.seed >> 16) % 100) / 100f);
                _previewDirty = true;
                Repaint();
            }
            if (GUILayout.Button("Apply to Character") && _characterRoot != null)
            {
                ApplyProceduralHairToCharacter();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            _builder.archetype = (HairArchetype)EditorGUILayout.EnumPopup("Archetype", _builder.archetype);
            _builder.partPosition = EditorGUILayout.Slider("Part Position", _builder.partPosition, -1f, 1f);
            _builder.bangsLength = EditorGUILayout.Slider("Bangs Length", _builder.bangsLength, 0.25f, 1.0f);
            _builder.bangsSweep = EditorGUILayout.Slider("Bangs Sweep", _builder.bangsSweep, -1f, 1f);
            _builder.crownVolume = EditorGUILayout.Slider("Crown Volume", _builder.crownVolume, 0f, 1f);
            _builder.spikeLift = EditorGUILayout.Slider("Spike Lift", _builder.spikeLift, 0f, 1f);
            _builder.sideLength = EditorGUILayout.Slider("Side Length", _builder.sideLength, 0.20f, 1.0f);
            _builder.backLength = EditorGUILayout.Slider("Back Length", _builder.backLength, 0.25f, 1.0f);
            _builder.layerShag = EditorGUILayout.Slider("Layer Shag", _builder.layerShag, 0f, 1f);
            _builder.curlAmplitude = EditorGUILayout.Slider("Curl / Coil / Afro", _builder.curlAmplitude, 0f, 1f);
            _builder.density = EditorGUILayout.Slider("Density", _builder.density, 0.60f, 1.35f);
            _builder.strandWidth = EditorGUILayout.Slider("Strand Width", _builder.strandWidth, 0.70f, 1.35f);
            _builder.seed = EditorGUILayout.IntField("Seed", _builder.seed);

            EditorGUILayout.Space(4);
            GUILayout.Label("Character Head Fit", EditorStyles.boldLabel);
            _builder.overallScale = EditorGUILayout.Slider("Head Scale", _builder.overallScale, 0.70f, 1.40f);
            _builder.headLocalOffset = EditorGUILayout.Vector3Field("Head Offset (or use Fit Gizmo)", _builder.headLocalOffset);
            _builder.targetChainCount = EditorGUILayout.IntSlider("Physics Bone Chains", _builder.targetChainCount, 16, 64);

            if (EditorGUI.EndChangeCheck())
            {
                _previewDirty = true;
                Repaint();
            }
        }

        private void DrawShaderTab()
        {
            Material targetMat = _activeMaterial != null ? _activeMaterial : _previewHairMat;
            if (targetMat == null) return;

            EditorGUI.BeginChangeCheck();
            ColorField(targetMat, "_RootColor", "Root Color");
            ColorField(targetMat, "_BaseColor", "Mid / Base Color");
            ColorField(targetMat, "_TipColor", "Tip Color");
            FloatSlider(targetMat, "_StrandVariation", "Strand Variation", 0f, 0.4f);
            FloatSlider(targetMat, "_RootAO", "Root AO", 0f, 1f);
            FloatSlider(targetMat, "_GrooveDepth", "Carved Groove Depth", 0.1f, 1f);
            FloatSlider(targetMat, "_HeadCenterBlend", "Radial Head Normal Blend", 0f, 0.7f);

            EditorGUILayout.Space(4);
            ColorField(targetMat, "_PrimarySpecColor", "Primary Spec Color");
            FloatSlider(targetMat, "_PrimaryShift", "Primary Shift", -0.5f, 0.5f);
            FloatSlider(targetMat, "_PrimaryPower", "Primary Power", 16f, 200f);
            FloatSlider(targetMat, "_PrimaryIntensity", "Primary Intensity", 0f, 2f);
            ColorField(targetMat, "_SecondarySpecColor", "Secondary Spec Color");
            FloatSlider(targetMat, "_SecondaryShift", "Secondary Shift", -0.5f, 0.5f);
            FloatSlider(targetMat, "_SecondaryPower", "Secondary Power", 8f, 120f);
            FloatSlider(targetMat, "_SecondaryIntensity", "Secondary Intensity", 0f, 2f);
            FloatSlider(targetMat, "_RimIntensity", "Rim Backlight", 0f, 2f);
            FloatSlider(targetMat, "_AlphaClip", "Tip Alpha Cutoff", 0.05f, 0.85f);

            if (EditorGUI.EndChangeCheck())
            {
                if (_activeMaterial != null && _previewHairMat != null)
                    _previewHairMat.CopyPropertiesFromMaterial(_activeMaterial);
                EditorUtility.SetDirty(targetMat);
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void DrawPhysicsTab()
        {
            _activePhysics = (AetherStrandPhysics)EditorGUILayout.ObjectField("AetherStrandPhysics", _activePhysics, typeof(AetherStrandPhysics), true);
            if (_activePhysics == null)
            {
                EditorGUILayout.HelpBox("Click 'Apply to Character' first to attach physics, colliders, and inter-lock separation.", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            _activePhysics.stiffness = EditorGUILayout.Slider("Stiffness", _activePhysics.stiffness, 0.05f, 0.95f);
            _activePhysics.gravity = EditorGUILayout.Slider("Gravity (m/s^2)", _activePhysics.gravity, 0f, 12f);
            _activePhysics.damping = EditorGUILayout.Slider("Damping", _activePhysics.damping, 0.01f, 0.35f);
            _activePhysics.headInertia = EditorGUILayout.Slider("Head Inertia", _activePhysics.headInertia, 0f, 1.5f);
            _activePhysics.bendingSmoothness = EditorGUILayout.Slider("Anti-Kink Smoothness", _activePhysics.bendingSmoothness, 0f, 0.95f);
            _activePhysics.enableInterLockPush = EditorGUILayout.Toggle("Inter-Lock Push", _activePhysics.enableInterLockPush);
            _activePhysics.interLockRadius = EditorGUILayout.Slider("Inter-Lock Radius (m)", _activePhysics.interLockRadius, 0.005f, 0.045f);
            _activePhysics.interLockStrength = EditorGUILayout.Slider("Inter-Lock Strength", _activePhysics.interLockStrength, 0f, 1f);
            _activePhysics.fallbackWindStrength = EditorGUILayout.Slider("Indoor Fallback Wind", _activePhysics.fallbackWindStrength, 0f, 3f);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_activePhysics);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Rest")) { _activePhysics.RebuildChains(); _activePhysics.ResetToRestPose(); SceneView.RepaintAll(); }
            if (GUILayout.Button("Test Wind Gust"))
            {
                _activePhysics.AddImpulse(new Vector3(1.5f, 0.1f, 0.6f));
                for (int i = 0; i < 15; i++) _activePhysics.Simulate(1f / 60f, i / 60f);
                _lastAudit = _activePhysics.ComputeAudit();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Run Audit"))
            {
                for (int i = 0; i < 30; i++) _activePhysics.Simulate(1f / 60f, i / 60f);
                _lastAudit = _activePhysics.ComputeAudit();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            if (_lastAudit.HasValue)
            {
                var a = _lastAudit.Value;
                EditorGUILayout.HelpBox(
                    $"Verdict: {a.verdict} (Score {a.score}/100)\n" +
                    $"Chains: {a.totalChains} | Colliders: {a.colliderCount} | Penetrating: {a.penetratingNodes} | Inter-Lock Clips: {a.interLockClipCount}\n" +
                    $"Max Kink: {a.maxKinkAngleDeg:F1} deg | Max Stretch: {a.maxStretchErrorPct:F2}% | Tip Sway: {a.meanTipSwayCm:F2} cm",
                    a.verdict == "PASS" ? MessageType.Info : MessageType.Warning);
            }
        }

        private string _diagOutputFolder = "Assets/AetherStrand_AI_Diag";
        private string _lastDiagMarkdown = "";

        private void DrawBatchEvalTab()
        {
            GUILayout.Label("AI Debug & Diagnostic Export (for Wren / Neon)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Exports millimeter Head-vs-Hair alignment deltas, skull bounds, collider config, 3-scenario deterministic motion audits (Rest, 60-deg Turn, 5 m/s Sprint), shader state, and a 4-Up Turnaround PNG.",
                MessageType.Info);

            _diagOutputFolder = EditorGUILayout.TextField("Diag Export Folder", _diagOutputFolder);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_characterRoot == null))
            {
                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.98f);
                if (GUILayout.Button("Export AI Diagnostic Bundle", GUILayout.Height(30)))
                {
                    var rep = AetherStrandAiDiagnostics.ExportBundle(_characterRoot, _diagOutputFolder, _hairRenderer);
                    _lastDiagMarkdown = AetherStrandAiDiagnostics.FormatMarkdownSummary(rep);
                    Capture4UpTurnaroundPng(Path.Combine(_diagOutputFolder, "aetherstrand_turnaround_4up.png").Replace('\\', '/'));
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Copy MD Report", GUILayout.Height(30), GUILayout.Width(120)))
                {
                    var rep = AetherStrandAiDiagnostics.CollectReport(_characterRoot, _hairRenderer);
                    _lastDiagMarkdown = AetherStrandAiDiagnostics.FormatMarkdownSummary(rep);
                    EditorGUIUtility.systemCopyBuffer = _lastDiagMarkdown;
                    Debug.Log("[AetherStrand] Copied AI Diagnostic Markdown report to clipboard.");
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastDiagMarkdown))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.TextArea(_lastDiagMarkdown, GUILayout.MinHeight(140));
            }

            EditorGUILayout.Space(8);
            GUILayout.Label("Batch Paths & Manual Overrides", EditorStyles.boldLabel);
            _hairRenderer = (Renderer)EditorGUILayout.ObjectField("Hair Renderer", _hairRenderer, typeof(Renderer), true);
            _bonePrefix = EditorGUILayout.TextField("Hair Bone Prefix", _bonePrefix);
            _materialOutputPath = EditorGUILayout.TextField("Material Output Path", _materialOutputPath);
            _meshOutputPath = EditorGUILayout.TextField("Mesh Output Path", _meshOutputPath);
            _presetJsonPath = EditorGUILayout.TextField("Preset JSON (Opt)", _presetJsonPath);
            _headHeightOverride = EditorGUILayout.FloatField("Head Height Override", _headHeightOverride);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Export 4-Up Turnaround PNG Only", GUILayout.Height(24)))
                Capture4UpTurnaroundPng("Assets/AetherStrand_Turnaround_4Up.png");
            if (GUILayout.Button("Create / Update Material Asset Only", GUILayout.Height(24)))
                _activeMaterial = CreateMaterialAsset(_materialOutputPath, string.IsNullOrWhiteSpace(_presetJsonPath) ? null : _presetJsonPath);
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (!_sceneFitHandleActive || _characterRoot == null) return;
            Transform head = AetherStrandMeshGenerator.ResolveHeadBone(_characterRoot);
            if (head == null) return;

            Vector3 worldPos = head.TransformPoint(_builder.headLocalOffset + new Vector3(0f, 0.112f, 0.004f));
            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.PositionHandle(worldPos, head.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                _builder.headLocalOffset = head.InverseTransformPoint(newWorldPos) - new Vector3(0f, 0.112f, 0.004f);
                _previewDirty = true;
                Repaint();
            }
            Handles.Label(worldPos + Vector3.up * 0.14f, "AetherStrand Hair Fit (click Apply to Character to rebuild)");
        }

        private void ApplyHeroPreset(int idx, bool regenerateCharacter)
        {
            var presets = AetherStrandProceduralBuilder.HeroPresets;
            _activePresetIndex = Mathf.Clamp(idx, 0, presets.Length - 1);
            var p = presets[_activePresetIndex];

            _builder.archetype = p.Builder.archetype;
            _builder.partPosition = p.Builder.partPosition;
            _builder.bangsLength = p.Builder.bangsLength;
            _builder.bangsSweep = p.Builder.bangsSweep;
            _builder.crownVolume = p.Builder.crownVolume;
            _builder.spikeLift = p.Builder.spikeLift;
            _builder.sideLength = p.Builder.sideLength;
            _builder.backLength = p.Builder.backLength;
            _builder.layerShag = p.Builder.layerShag;
            _builder.curlAmplitude = p.Builder.curlAmplitude;
            _builder.density = p.Builder.density;
            _builder.strandWidth = p.Builder.strandWidth;
            _builder.seed = p.Builder.seed;

            ApplyPaletteToMat(_previewHairMat, p);
            if (_activeMaterial != null) ApplyPaletteToMat(_activeMaterial, p);
            if (_activePhysics != null)
            {
                _activePhysics.stiffness = p.Stiffness;
                _activePhysics.gravity = p.Gravity;
                _activePhysics.damping = p.Damping;
                _activePhysics.headInertia = p.Inertia;
                _activePhysics.bendingSmoothness = p.Bending;
                _activePhysics.rootAnchor = p.RootAnchor;
                EditorUtility.SetDirty(_activePhysics);
            }

            _previewDirty = true;
            if (regenerateCharacter && _characterRoot != null) ApplyProceduralHairToCharacter();
            Repaint();
        }

        private static void ApplyPaletteToMat(Material mat, HeroPresetDef p)
        {
            if (mat == null) return;
            if (ColorUtility.TryParseHtmlString(p.RootHex, out Color c0)) mat.SetColor("_RootColor", c0);
            if (ColorUtility.TryParseHtmlString(p.BaseHex, out Color c1)) mat.SetColor("_BaseColor", c1);
            if (ColorUtility.TryParseHtmlString(p.TipHex, out Color c2)) mat.SetColor("_TipColor", c2);
            if (ColorUtility.TryParseHtmlString(p.Spec1Hex, out Color s1)) mat.SetColor("_PrimarySpecColor", s1);
            if (ColorUtility.TryParseHtmlString(p.Spec2Hex, out Color s2)) mat.SetColor("_SecondarySpecColor", s2);
            EditorUtility.SetDirty(mat);
        }

        private void ApplyProceduralHairToCharacter()
        {
            if (_characterRoot == null) return;
            _activeMaterial = CreateMaterialAsset(_materialOutputPath, string.IsNullOrWhiteSpace(_presetJsonPath) ? null : _presetJsonPath);
            if (_activeMaterial != null && _previewHairMat != null)
                _activeMaterial.CopyPropertiesFromMaterial(_previewHairMat);

            // Convert procedural locks into a temporary PresetGeometry-compatible run via AetherStrandMeshGenerator
            var locks = AetherStrandProceduralBuilder.BuildLocks(_builder);
            string tmpJson = Path.Combine(Application.temporaryCachePath, "AetherStrand_LiveStudio.json");
            WriteLocksAsPresetJson(locks, tmpJson);

            var sculpt = new HairSculptParams
            {
                overallScale = 1.0f, // already scaled & aligned by AetherStrandProceduralBuilder
                headLocalOffset = Vector3.zero,
                bangsLength = 1.0f,
                layerLength = 1.0f,
                widthScale = 1.0f,
                thicknessScale = 1.0f,
                targetChainCount = _builder.targetChainCount,
                bonesPerChain = _builder.bonesPerChain
            };

            SkinnedMeshRenderer smr = AetherStrandMeshGenerator.GenerateAndAttach(
                _characterRoot, sculpt, tmpJson, _meshOutputPath, _activeMaterial);

            if (smr == null) return;
            _hairRenderer = smr;

            _activePhysics = AutoSetupPhysics(
                _characterRoot, _bonePrefix, null, _headHeightOverride, _hairRenderer, TryComputeHeadBounds(_characterRoot));
            if (_activePhysics != null)
            {
                var p = AetherStrandProceduralBuilder.HeroPresets[_activePresetIndex];
                _activePhysics.stiffness = p.Stiffness;
                _activePhysics.gravity = p.Gravity;
                _activePhysics.damping = p.Damping;
                _activePhysics.headInertia = p.Inertia;
                _activePhysics.bendingSmoothness = p.Bending;
                _activePhysics.rootAnchor = p.RootAnchor;
                _activePhysics.ResetToRestPose();
                _lastAudit = _activePhysics.ComputeAudit();
                EditorUtility.SetDirty(_activePhysics);
            }
            SceneView.RepaintAll();
        }

        private static void WriteLocksAsPresetJson(List<AetherStrandProceduralBuilder.ProceduralLock> locks, string path)
        {
            using (var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8))
            {
                sw.WriteLine("{\"schemaVersion\":\"2.5\",\"locks\":[");
                for (int i = 0; i < locks.Count; i++)
                {
                    var lk = locks[i];
                    // Write back in Blender Z-up (-Y front) so AetherStrandMeshGenerator's Unity conversion is exact identity
                    sw.Write($"{{\"zone\":\"{lk.zone}\",\"width\":{lk.width.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)},\"thickness\":{lk.thickness.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)},\"seed\":{lk.seed.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},\"layerAO\":{lk.layerAO.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)},\"isBaseShell\":{(lk.isBaseShell ? "true" : "false")},\"points\":[");
                    for (int k = 0; k < lk.points.Length; k++)
                    {
                        Vector3 p = lk.points[k];
                        sw.Write($"[{p.x.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)},{(-p.z).ToString("F5", System.Globalization.CultureInfo.InvariantCulture)},{p.y.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}]");
                        if (k < lk.points.Length - 1) sw.Write(",");
                    }
                    sw.Write("]}");
                    if (i < locks.Count - 1) sw.WriteLine(",");
                }
                sw.WriteLine("]}");
            }
        }

        /// <summary>
        /// Renders a 1024x1024 4-view turnaround (Front, Back, Left, Right) PNG for AI visual eval / hillclimbing.
        /// </summary>
        public void Capture4UpTurnaroundPng(string outputAssetPath)
        {
            if (_preview == null) InitPreview();
            if (_previewDirty) RebuildPreviewMesh();

            const int cell = 512;
            var finalTex = new Texture2D(cell * 2, cell * 2, TextureFormat.RGBA32, false);
            Vector2[] views = { new Vector2(0f, 6f), new Vector2(180f, 8f), new Vector2(-90f, 6f), new Vector2(90f, 6f) };
            int[] xs = { 0, cell, 0, cell };
            int[] ys = { cell, cell, 0, 0 };

            RenderTexture prevRT = RenderTexture.active;
            for (int v = 0; v < 4; v++)
            {
                _preview.BeginStaticPreview(new Rect(0, 0, cell, cell));
                Vector3 target = new Vector3(0f, 0.105f, 0f);
                Quaternion q = Quaternion.Euler(views[v].y, views[v].x + 180f, 0f);
                _preview.camera.transform.position = target + q * (Vector3.back * 0.52f);
                _preview.camera.transform.LookAt(target, Vector3.up);

                Matrix4x4 skullMat = Matrix4x4.TRS(new Vector3(0f, 0.112f, 0.004f), Quaternion.identity, new Vector3(0.180f, 0.212f, 0.200f));
                Matrix4x4 neckMat  = Matrix4x4.TRS(new Vector3(0f, -0.025f, -0.012f), Quaternion.identity, new Vector3(0.085f, 0.120f, 0.088f));
                _preview.DrawMesh(_previewBustMesh, skullMat, _previewBustMat, 0);
                _preview.DrawMesh(_previewBustMesh, neckMat, _previewBustMat, 0);
                if (_previewHairMesh != null && _previewHairMat != null)
                    _preview.DrawMesh(_previewHairMesh, Matrix4x4.identity, _previewHairMat, 0);

                _preview.camera.Render();
                Texture2D shot = _preview.EndStaticPreview();
                finalTex.SetPixels(xs[v], ys[v], cell, cell, shot.GetPixels());
                DestroyImmediate(shot);
            }
            RenderTexture.active = prevRT;
            finalTex.Apply();

            string dir = Path.GetDirectoryName(outputAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(outputAssetPath, finalTex.EncodeToPNG());
            DestroyImmediate(finalTex);
            AssetDatabase.ImportAsset(outputAssetPath);
            Debug.Log($"[AetherStrand] Exported 4-Up Turnaround PNG to '{outputAssetPath}'.");
        }

        private void SyncReferencesFromCharacter()
        {
            if (_characterRoot == null) return;
            _activePhysics = _characterRoot.GetComponent<AetherStrandPhysics>();
            Transform hairT = _characterRoot.transform.Find("AetherStrand_HairMesh");
            if (hairT != null)
            {
                _hairRenderer = hairT.GetComponent<Renderer>();
                if (_hairRenderer != null && _hairRenderer.sharedMaterial != null)
                    _activeMaterial = _hairRenderer.sharedMaterial;
            }
        }

        private static Bounds? TryComputeHeadBounds(GameObject root)
        {
            if (root == null) return null;
            Transform head = AetherStrandMeshGenerator.ResolveHeadBone(root);
            if (head == null) return null;
            return new Bounds(head.position + head.up * 0.112f, new Vector3(0.165f, 0.22f, 0.195f));
        }

        private static void ColorField(Material m, string prop, string label)
        {
            if (m.HasProperty(prop)) m.SetColor(prop, EditorGUILayout.ColorField(label, m.GetColor(prop)));
        }

        private static void FloatSlider(Material m, string prop, string label, float min, float max)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, EditorGUILayout.Slider(label, m.GetFloat(prop), min, max));
        }

        // =========================================================================
        // Headless Batchmode Static API (100% compatible with automated builders)
        // =========================================================================

        public static Material CreateMaterialAsset(
            string outputPath = "Assets/AetherStrand_Hair_Mat.mat",
            string presetJsonPath = null)
        {
            Shader s = Shader.Find("AetherStrand/SculptedHair_URP");
            if (s == null) { Debug.LogError("[AetherStrand] Shader 'AetherStrand/SculptedHair_URP' not found."); return null; }

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            bool isNew = mat == null;
            if (isNew) mat = new Material(s);
            else mat.shader = s;

            mat.SetColor("_RootColor", new Color(0.118f, 0.094f, 0.082f, 1f));
            mat.SetColor("_BaseColor", new Color(0.227f, 0.184f, 0.157f, 1f));
            mat.SetColor("_TipColor", new Color(0.329f, 0.275f, 0.235f, 1f));
            mat.SetColor("_PrimarySpecColor", new Color(0.847f, 0.812f, 0.784f, 1f));
            mat.SetColor("_SecondarySpecColor", new Color(0.549f, 0.463f, 0.400f, 1f));
            mat.SetFloat("_PrimaryShift", 0.08f);
            mat.SetFloat("_PrimaryPower", 84f);
            mat.SetFloat("_PrimaryIntensity", 0.34f);
            mat.SetFloat("_SecondaryShift", -0.15f);
            mat.SetFloat("_SecondaryPower", 32f);
            mat.SetFloat("_SecondaryIntensity", 0.20f);
            mat.SetFloat("_RimIntensity", 0.28f);
            mat.SetFloat("_RimPower", 3.4f);
            mat.SetFloat("_RootAO", 0.46f);
            mat.SetFloat("_GrooveDepth", 0.68f);
            mat.SetFloat("_StrandVariation", 0.22f);
            mat.SetFloat("_HeadCenterBlend", 0.30f);
            mat.SetFloat("_UseAlphaClip", 1f);
            mat.SetFloat("_AlphaClip", 0.34f);
            mat.EnableKeyword("_USE_ALPHA_CLIP");

            RecipeRoot recipe = LoadRecipeJson(presetJsonPath);
            if (recipe != null && recipe.shader != null)
            {
                if (TryParseHex(recipe.shader.rootColor, out Color c0)) mat.SetColor("_RootColor", c0);
                if (TryParseHex(recipe.shader.baseColor, out Color c1)) mat.SetColor("_BaseColor", c1);
                if (TryParseHex(recipe.shader.tipColor, out Color c2)) mat.SetColor("_TipColor", c2);
                if (TryParseHex(recipe.shader.primarySpecColor, out Color s1)) mat.SetColor("_PrimarySpecColor", s1);
                if (TryParseHex(recipe.shader.secondarySpecColor, out Color s2)) mat.SetColor("_SecondarySpecColor", s2);
                if (recipe.shader.primaryPower > 0f) mat.SetFloat("_PrimaryPower", recipe.shader.primaryPower);
                if (recipe.shader.secondaryPower > 0f) mat.SetFloat("_SecondaryPower", recipe.shader.secondaryPower);
                mat.SetFloat("_PrimaryShift", recipe.shader.primaryShift);
                mat.SetFloat("_PrimaryIntensity", recipe.shader.primaryIntensity);
                mat.SetFloat("_SecondaryShift", recipe.shader.secondaryShift);
                mat.SetFloat("_SecondaryIntensity", recipe.shader.secondaryIntensity);
                mat.SetFloat("_RimIntensity", recipe.shader.rimIntensity);
                if (recipe.shader.rimPower > 0f) mat.SetFloat("_RimPower", recipe.shader.rimPower);
                mat.SetFloat("_RootAO", recipe.shader.rootAO);
                if (recipe.shader.grooveDepth > 0f) mat.SetFloat("_GrooveDepth", recipe.shader.grooveDepth);
                mat.SetFloat("_StrandVariation", recipe.shader.strandVariation);
            }

            TryBindTexture(mat, "_GrooveAoMap", "AetherStrand_GrooveAO");
            TryBindTexture(mat, "_FlowIdMap", "AetherStrand_FlowIDShift");
            TryBindTexture(mat, "_NormalMap", "AetherStrand_Normal");
            TryBindTexture(mat, "_AlphaMap", "AetherStrand_AlphaCard");

            if (isNew) AssetDatabase.CreateAsset(mat, outputPath);
            else EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static AetherStrandPhysics AutoSetupPhysics(
            GameObject characterRoot,
            string bonePrefix = "Hair_",
            string presetJsonPath = null,
            float headHeightOverride = 0f,
            Renderer hairRenderer = null,
            Bounds? headMeshWorldBounds = null)
        {
            if (characterRoot == null) return null;

            AetherStrandPhysics physics = characterRoot.GetComponent<AetherStrandPhysics>();
            if (physics == null) physics = characterRoot.AddComponent<AetherStrandPhysics>();
            if (hairRenderer != null) physics.hairRenderer = hairRenderer;

            Animator animator = characterRoot.GetComponentInChildren<Animator>();
            Transform headBone = null, neckBone = null, chestBone = null, leftArm = null, rightArm = null;
            if (animator != null && animator.isHuman)
            {
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
                chestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                leftArm = animator.GetBoneTransform(HumanBodyBones.LeftShoulder) ?? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightArm = animator.GetBoneTransform(HumanBodyBones.RightShoulder) ?? animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
            if (headBone == null) headBone = AetherStrandMeshGenerator.ResolveHeadBone(characterRoot);

            physics.headTransform = headBone != null ? headBone : characterRoot.transform;
            physics.colliders.Clear();

            float H = headHeightOverride > 0.05f ? headHeightOverride : 0.22f;
            Vector3 skullCenterW = physics.headTransform.position + Vector3.up * (0.50f * H);
            float craniumR = 0.38f * H;

            if (headMeshWorldBounds.HasValue)
            {
                Bounds hb = headMeshWorldBounds.Value;
                H = Mathf.Clamp(hb.size.y, 0.16f, 0.28f);
                skullCenterW = hb.center + Vector3.up * (0.04f * H);
                craniumR = Mathf.Min(hb.extents.x, hb.extents.z) * 0.84f;
            }

            Vector3 fwdW = characterRoot.transform.forward;
            Vector3 rightW = characterRoot.transform.right;
            Vector3 upW = Vector3.up;

            AddWorldCollider(physics.headTransform, "AS_Col_Cranium", HairColliderShape.Sphere, skullCenterW - fwdW * (0.04f * H), craniumR, 0f, upW, physics);
            AddWorldCollider(physics.headTransform, "AS_Col_FaceJaw", HairColliderShape.Capsule, skullCenterW - upW * (0.20f * H) + fwdW * (0.14f * H), craniumR * 0.72f, 0.34f * H, (upW - fwdW * 0.15f).normalized, physics);
            if (neckBone != null)
                AddWorldCollider(neckBone, "AS_Col_Neck", HairColliderShape.Capsule, neckBone.position + upW * (0.18f * H), 0.22f * H, 0.36f * H, upW, physics);
            if (chestBone != null)
                AddWorldCollider(chestBone, "AS_Col_UpperChest", HairColliderShape.Capsule, chestBone.position + upW * (0.12f * H) - fwdW * (0.04f * H), 0.38f * H, 0.85f * H, rightW, physics);
            if (leftArm != null)
                AddWorldCollider(leftArm, "AS_Col_ShoulderL", HairColliderShape.Capsule, leftArm.position, 0.24f * H, 0.48f * H, upW, physics);
            if (rightArm != null)
                AddWorldCollider(rightArm, "AS_Col_ShoulderR", HairColliderShape.Capsule, rightArm.position, 0.24f * H, 0.48f * H, upW, physics);

            string prefix = string.IsNullOrEmpty(bonePrefix) ? "Hair_" : bonePrefix;
            var roots = new List<Transform>();
            foreach (Transform t in characterRoot.GetComponentsInChildren<Transform>(true))
            {
                bool matches = t.name.StartsWith(prefix, StringComparison.Ordinal);
                bool parentMatches = t.parent != null && t.parent.name.StartsWith(prefix, StringComparison.Ordinal);
                if (matches && !parentMatches) roots.Add(t);
            }
            roots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            physics.hairRootBones = roots;

            RecipeRoot recipe = LoadRecipeJson(presetJsonPath);
            if (recipe != null && recipe.physics != null)
            {
                if (recipe.physics.stiffness > 0f) physics.stiffness = recipe.physics.stiffness;
                if (recipe.physics.gravity > 0f) physics.gravity = recipe.physics.gravity;
                if (recipe.physics.damping > 0f) physics.damping = recipe.physics.damping;
                if (recipe.physics.inertia > 0f) physics.headInertia = recipe.physics.inertia;
                physics.bendingSmoothness = recipe.physics.bendingSmoothness;
                if (recipe.physics.collisionMarginMm > 0f) physics.collisionMargin = recipe.physics.collisionMarginMm * 0.001f;
                if (recipe.physics.rootAnchor > 0f) physics.rootAnchor = recipe.physics.rootAnchor;
                if (recipe.physics.solverSubsteps > 0) physics.solverSubsteps = Mathf.Clamp(recipe.physics.solverSubsteps, 1, 6);
                physics.fallbackWindStrength = recipe.physics.windStrength;
            }

            physics.RebuildChains();
            EditorUtility.SetDirty(physics);
            return physics;
        }

        private static void AddWorldCollider(
            Transform parentBone, string childName, HairColliderShape shape,
            Vector3 worldCenter, float worldRadius, float worldHeight, Vector3 worldAxis, AetherStrandPhysics physics)
        {
            if (parentBone == null) return;
            Transform existing = parentBone.Find(childName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null)
            {
                go.transform.SetParent(parentBone, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            AetherStrandCollider col = go.GetComponent<AetherStrandCollider>();
            if (col == null) col = go.AddComponent<AetherStrandCollider>();

            Vector3 s = go.transform.lossyScale;
            float invScale = 1f / Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
            col.shape = shape;
            col.centerOffset = go.transform.InverseTransformPoint(worldCenter);
            col.radius = worldRadius * invScale;
            col.capsuleHeight = worldHeight * invScale;
            col.capsuleAxis = go.transform.InverseTransformDirection(worldAxis.normalized).normalized;
            physics.colliders.Add(col);
        }

        private static string ResolvePackageRoot()
        {
            string[] guids = AssetDatabase.FindAssets("AetherStrandSetupWindow t:MonoScript");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                string editorDir = Path.GetDirectoryName(scriptPath);
                string pkgRoot = string.IsNullOrEmpty(editorDir) ? null : Path.GetDirectoryName(editorDir);
                if (!string.IsNullOrEmpty(pkgRoot)) return pkgRoot.Replace('\\', '/');
            }
            return "Packages/com.aetherstrand.sculptedhair";
        }

        private static void TryBindTexture(Material mat, string prop, string fileToken)
        {
            string pkgTex = ResolvePackageRoot() + "/Textures/" + fileToken + ".png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pkgTex);
            if (tex == null)
            {
                string[] guids = AssetDatabase.FindAssets(fileToken + " t:Texture2D");
                if (guids.Length > 0)
                {
                    Array.Sort(guids, StringComparer.Ordinal);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
            if (tex != null) mat.SetTexture(prop, tex);
        }

        private static RecipeRoot LoadRecipeJson(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try { return JsonUtility.FromJson<RecipeRoot>(File.ReadAllText(path)); }
            catch { return null; }
        }

        private static bool TryParseHex(string hex, out Color c)
        {
            c = Color.white;
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c);
        }
    }
}
#endif
