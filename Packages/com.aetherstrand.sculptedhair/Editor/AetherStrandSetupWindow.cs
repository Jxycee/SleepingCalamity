#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AetherStrand.Editor
{
    /// <summary>
    /// AetherStrand Hair Studio v3.0.1 (Unity 6 / URP 17.6):
    /// 1:1 Dark-Studio UI converging with the AetherStrand web studio:
    /// - Top brand header bar (Viewport | Character | Maps | AI Suite + amber Apply CTA)
    /// - Full-bleed interactive 3D Viewport (PreviewRenderUtility) with HUD stats overlay & Hide Controls toggle
    /// - Floating dark control dock: 2-column Preset Cards with color swatches, Hair Builder, Shader, Physics,
    ///   Scene/Camera, and full AI Eval & Auto-Hillclimb suite
    /// - Persistent Character Motion bar (Idle | Walk | Head Turn | Wind Surge)
    /// - 100% backward-compatible headless batchmode static API for automated CI/builder scripts
    /// </summary>
    public class AetherStrandSetupWindow : EditorWindow
    {
        [Serializable] private class RecipeRoot { public string schemaVersion, presetId; public RecipeShader shader; public RecipePhysics physics; }
        [Serializable] private class RecipeShader
        {
            public string rootColor, baseColor, tipColor, primarySpecColor, secondarySpecColor;
            public float primaryShift, primaryPower, primaryIntensity;
            public float secondaryShift, secondaryPower, secondaryIntensity;
            public float rimIntensity, rimPower, rootAO, grooveDepth, strandVariation;
        }
        [Serializable] private class RecipePhysics
        {
            public float stiffness, gravity, windStrength, windTurbulence, windAzimuth;
            public float damping, inertia, bendingSmoothness, collisionMarginMm, rootAnchor;
            public int solverSubsteps;
        }

        private enum TopView { Viewport, Character, Maps, AiSuite }
        private enum DockTab { Presets, Builder, Shader, Physics, Scene, Inspect }
        private enum MotionPreviewMode { Idle, Walk, HeadTurn, WindSurge }

        // Palette matching the AetherStrand web studio dark theme
        private static readonly Color ColBgTopBar   = new Color(0.055f, 0.067f, 0.094f, 1f);
        private static readonly Color ColBgViewport = new Color(0.215f, 0.224f, 0.247f, 1f);
        private static readonly Color ColBgDock     = new Color(0.071f, 0.086f, 0.122f, 1f);
        private static readonly Color ColBgCard     = new Color(0.094f, 0.114f, 0.157f, 1f);
        private static readonly Color ColBgCardSel  = new Color(0.125f, 0.145f, 0.192f, 1f);
        private static readonly Color ColBgPill     = new Color(0.130f, 0.153f, 0.204f, 1f);
        private static readonly Color ColBorder     = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color ColAmber      = new Color(0.918f, 0.702f, 0.204f, 1f);
        private static readonly Color ColAmberDark  = new Color(0.12f, 0.09f, 0.02f, 1f);
        private static readonly Color ColTextMuted  = new Color(0.62f, 0.66f, 0.74f, 1f);

        // Embedded 3D Preview
        private PreviewRenderUtility _preview;
        private Mesh _previewHairMesh;
        private Mesh _previewBustMesh;
        private Material _previewBustMat;
        private Material _previewHairMat;
        private Vector2 _orbit = new Vector2(18f, 8f);
        private float _zoom = 0.46f;
        private bool _previewDirty = true;
        private int _previewLockCount = 376;

        // UI State
        private TopView _topView = TopView.Viewport;
        private DockTab _dockTab = DockTab.Presets;
        private MotionPreviewMode _motionMode = MotionPreviewMode.Idle;
        private bool _controlsCollapsed;
        private int _activePresetIndex;
        private bool _useExactHeroPreset = true;
        private Vector2 _scroll;
        private HairBuilderState _builder = new HairBuilderState();
        private bool _sceneFitHandleActive;
        private double _lastTickTime;
        private float _simClock;

        // Character & Batch State
        private GameObject _characterRoot;
        private Renderer _hairRenderer;
        private Material _activeMaterial;
        private AetherStrandPhysics _activePhysics;
        private string _bonePrefix = "Hair_";
        private string _materialOutputPath = "Assets/AetherStrand_Hair_Mat.mat";
        private string _meshOutputPath = "Assets/AetherStrand_Hair_Mesh.asset";
        private string _presetJsonPath = "";
        private string _diagOutputFolder = "Assets/AetherStrand_AI_Diag";
        private float _headHeightOverride = 0f;
        private PhysicsAuditResult? _lastAudit;
        private string _lastDiagMarkdown = "";

        // Cached GUIStyles
        private GUIStyle _stBrand, _stHudTitle, _stHudSub, _stCardTitle, _stCardSub, _stMuted, _stBtnDark, _stBtnAmber;
        private bool _stylesReady;

        [MenuItem("Tools/AetherStrand/Hair Studio Setup")]
        public static void OpenWindow()
        {
            var win = GetWindow<AetherStrandSetupWindow>("AetherStrand");
            win.minSize = new Vector2(540, 720);
        }

        private void OnEnable()
        {
            InitPreview();
            ApplyHeroPreset(0, regenerateCharacter: false);
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorTick;
            _lastTickTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorTick;
            SceneView.duringSceneGui -= OnSceneGUI;
            DisposePreview();
        }

        private void OnEditorTick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - _lastTickTime), 0f, 0.05f);
            _lastTickTime = now;
            if (dt <= 0.0001f) return;

            if (_motionMode != MotionPreviewMode.Idle)
            {
                _simClock += dt;
                if (_activePhysics != null && _activePhysics.headTransform != null && !EditorApplication.isPlaying)
                {
                    if (_motionMode == MotionPreviewMode.WindSurge)
                        _activePhysics.fallbackWindStrength = 1.6f;
                    else
                        _activePhysics.fallbackWindStrength = 0f;
                    _activePhysics.Simulate(dt, _simClock);
                }
                Repaint();
            }
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stBrand = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, normal = { textColor = Color.white } };
            _stHudTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = Color.white } };
            _stHudSub = new GUIStyle(EditorStyles.label) { fontSize = 11, normal = { textColor = ColTextMuted } };
            _stCardTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, normal = { textColor = Color.white } };
            _stCardSub = new GUIStyle(EditorStyles.label) { fontSize = 10, normal = { textColor = ColTextMuted }, wordWrap = false, clipping = TextClipping.Clip };
            _stMuted = new GUIStyle(EditorStyles.label) { fontSize = 11, normal = { textColor = ColTextMuted } };

            _stBtnDark = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.88f, 0.90f, 0.94f) }
            };
            _stBtnAmber = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColAmberDark }
            };
            _stylesReady = true;
        }

        private void InitPreview()
        {
            if (_preview != null) return;
            _preview = new PreviewRenderUtility();
            _preview.cameraFieldOfView = 30f;
            _preview.camera.nearClipPlane = 0.02f;
            _preview.camera.farClipPlane = 10f;
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = ColBgViewport;

            _preview.lights[0].intensity = 1.35f;
            _preview.lights[0].color = new Color(1.0f, 0.96f, 0.92f);
            _preview.lights[0].transform.rotation = Quaternion.Euler(32f, -35f, 0f);
            _preview.lights[1].intensity = 0.65f;
            _preview.lights[1].color = new Color(0.75f, 0.85f, 1.0f);
            _preview.lights[1].transform.rotation = Quaternion.Euler(-15f, 145f, 0f);

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
            _previewHairMat.SetFloat("_AlphaClip", 0.22f);
            _previewHairMat.SetFloat("_HeadCenterBlend", 0.45f);
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

        private string GetActiveHeroPresetJsonPath()
        {
            string[] names = { "PartedShag", "SpikyCrown", "LongCascade", "SmokySpiky", "WolfCut" };
            string nm = names[Mathf.Clamp(_activePresetIndex, 0, names.Length - 1)];
            string p = ResolvePackageRoot() + "/Presets/" + nm + ".json";
            return File.Exists(p) ? p : null;
        }

        private void RebuildPreviewMesh()
        {
            if (_previewHairMesh != null) DestroyImmediate(_previewHairMesh);
            string exactJson = _useExactHeroPreset ? GetActiveHeroPresetJsonPath() : null;
            if (!string.IsNullOrEmpty(exactJson))
            {
                _previewHairMesh = AetherStrandMeshGenerator.BuildStaticPreviewMeshFromJson(exactJson, _builder.overallScale, _builder.headLocalOffset);
                int[] counts = { 376, 243, 263, 430, 230 };
                _previewLockCount = counts[Mathf.Clamp(_activePresetIndex, 0, counts.Length - 1)];
            }
            if (_previewHairMesh == null)
            {
                var locks = AetherStrandProceduralBuilder.BuildLocks(_builder);
                _previewLockCount = locks.Count;
                _previewHairMesh = AetherStrandProceduralBuilder.BuildPreviewMesh(locks);
            }
            _previewDirty = false;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_preview == null) InitPreview();
            if (_previewDirty) RebuildPreviewMesh();

            // Full dark studio backdrop
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), ColBgViewport);

            DrawTopHeaderBar();

            if (_topView == TopView.Viewport)
            {
                DrawStudioViewportLayout();
            }
            else if (_topView == TopView.Character)
            {
                DrawCharacterBindingView();
            }
            else if (_topView == TopView.Maps)
            {
                DrawMapsGalleryView();
            }
            else
            {
                DrawFullAiSuiteView();
            }
        }

        private void DrawTopHeaderBar()
        {
            Rect bar = new Rect(0, 0, position.width, 44);
            EditorGUI.DrawRect(bar, ColBgTopBar);
            EditorGUI.DrawRect(new Rect(0, 43, position.width, 1), ColBorder);

            GUI.Label(new Rect(14, 12, 130, 22), "AetherStrand", _stBrand);

            // Center Nav Tabs with amber active underline
            string[] labels = { "View", "Character", "Maps", "AI Suite" };
            float tabX = 148;
            for (int i = 0; i < labels.Length; i++)
            {
                bool active = (int)_topView == i;
                Rect tr = new Rect(tabX, 8, 72, 28);
                var st = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                    normal = { textColor = active ? Color.white : ColTextMuted }
                };
                if (GUI.Button(tr, labels[i], st)) { _topView = (TopView)i; Repaint(); }
                if (active) EditorGUI.DrawRect(new Rect(tr.x + 12, 41, tr.width - 24, 2.5f), ColAmber);
                tabX += 76;
            }

            // Right Amber Primary CTA Button
            Rect cta = new Rect(position.width - 134, 8, 122, 28);
            bool hasChar = _characterRoot != null;
            EditorGUI.DrawRect(cta, hasChar ? ColAmber : ColBgPill);
            var ctaStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = hasChar ? ColAmberDark : ColTextMuted }
            };
            if (GUI.Button(cta, hasChar ? "Apply to Character" : "Pick Character", ctaStyle))
            {
                if (hasChar) ApplyProceduralHairToCharacter();
                else _topView = TopView.Character;
            }
        }

        private void DrawStudioViewportLayout()
        {
            float topY = 44f;
            float dockH = _controlsCollapsed ? 0f : Mathf.Clamp(position.height * 0.48f, 290f, 390f);
            float viewH = Mathf.Max(180f, position.height - topY - dockH);

            Rect viewRect = new Rect(0, topY, position.width, viewH);
            DrawInteractive3DViewport(viewRect);

            // Top-left HUD overlay (Preset Name + lock/tri stats, matching web studio 1:1)
            var activePreset = AetherStrandProceduralBuilder.HeroPresets[Mathf.Clamp(_activePresetIndex, 0, 4)];
            string title = _useExactHeroPreset ? activePreset.Name : $"Custom {_builder.archetype}";
            int triCount = _previewHairMesh != null ? _previewHairMesh.triangles.Length / 3 : 0;
            GUI.Label(new Rect(16, topY + 12, 320, 22), title, _stHudTitle);
            GUI.Label(new Rect(16, topY + 34, 360, 18), $"{_previewLockCount} locks  ·  {triCount:N0} tris  ·  Unity URP 17", _stHudSub);

            // Bottom-center "Hide Controls / Show Controls" floating pill
            Rect togglePill = new Rect((position.width - 126f) * 0.5f, topY + viewH - 34f, 126f, 24f);
            EditorGUI.DrawRect(togglePill, new Color(0.08f, 0.10f, 0.14f, 0.90f));
            DrawOutline(togglePill, ColBorder);
            if (GUI.Button(togglePill, _controlsCollapsed ? "^ Show Controls" : "v Hide Controls",
                new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = Color.white } }))
            {
                _controlsCollapsed = !_controlsCollapsed;
                Repaint();
            }

            if (!_controlsCollapsed)
            {
                Rect dockRect = new Rect(10, topY + viewH + 4, position.width - 20, dockH - 12);
                DrawFloatingControlDock(dockRect);
            }
        }

        private void DrawFloatingControlDock(Rect dock)
        {
            EditorGUI.DrawRect(dock, ColBgDock);
            DrawOutline(dock, ColBorder);

            GUILayout.BeginArea(new Rect(dock.x + 10, dock.y + 8, dock.width - 20, dock.height - 14));

            // Row 1: Dock Tabs + Quick Action Buttons (Randomize | 4-Up | Gust | Reset)
            EditorGUILayout.BeginHorizontal();
            string[] tabNames = { "Presets", "Builder", "Shader", "Physics", "Scene", "Inspect" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                bool active = (int)_dockTab == i;
                GUI.backgroundColor = active ? ColAmber : ColBgPill;
                if (GUILayout.Button(tabNames[i], active ? _stBtnAmber : _stBtnDark, GUILayout.Height(26), GUILayout.MinWidth(54)))
                {
                    _dockTab = (DockTab)i;
                    Repaint();
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.24f, 0.20f, 0.10f);
            if (GUILayout.Button("Randomize", _stBtnDark, GUILayout.Height(26), GUILayout.Width(76)))
            {
                RandomizeShape();
            }
            GUI.backgroundColor = ColBgPill;
            if (GUILayout.Button("4-Up", _stBtnDark, GUILayout.Height(26), GUILayout.Width(44)))
            {
                string outPng = "Assets/AetherStrand_Turnaround_4Up.png";
                Capture4UpTurnaroundPng(outPng);
            }
            if (GUILayout.Button("Gust", _stBtnDark, GUILayout.Height(26), GUILayout.Width(42)))
            {
                _motionMode = MotionPreviewMode.WindSurge;
                if (_activePhysics != null) _activePhysics.AddImpulse(new Vector3(1.6f, 0.1f, 0.6f));
            }
            if (GUILayout.Button("Reset", _stBtnDark, GUILayout.Height(26), GUILayout.Width(46)))
            {
                _orbit = new Vector2(18f, 8f);
                _zoom = 0.46f;
                _motionMode = MotionPreviewMode.Idle;
                if (_activePhysics != null) _activePhysics.ResetToRestPose();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Middle Scrollable Dock Body
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            switch (_dockTab)
            {
                case DockTab.Presets: DrawPresetCardsGrid(); break;
                case DockTab.Builder: DrawBuilderSliders(); break;
                case DockTab.Shader:  DrawShaderSliders(); break;
                case DockTab.Physics: DrawPhysicsSliders(); break;
                case DockTab.Scene:   DrawSceneCameraControls(); break;
                case DockTab.Inspect: DrawInlineAiInspect(); break;
            }
            EditorGUILayout.EndScrollView();

            // Persistent Bottom Character Motion Bar (matching web studio 1:1!)
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Character Motion", _stMuted, GUILayout.Width(108));
            string[] mLabels = { "Idle", "Walk", "Head Turn", "Wind Surge" };
            for (int i = 0; i < mLabels.Length; i++)
            {
                bool active = (int)_motionMode == i;
                GUI.backgroundColor = active ? new Color(0.24f, 0.28f, 0.38f) : ColBgCard;
                if (GUILayout.Button(mLabels[i], _stBtnDark, GUILayout.Height(24)))
                {
                    _motionMode = (MotionPreviewMode)i;
                    Repaint();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawPresetCardsGrid()
        {
            var presets = AetherStrandProceduralBuilder.HeroPresets;
            for (int row = 0; row < presets.Length; row += 2)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < 2; col++)
                {
                    int idx = row + col;
                    if (idx < presets.Length) DrawSinglePresetCard(idx, presets[idx]);
                    else GUILayout.FlexibleSpace();
                    if (col == 0) GUILayout.Space(6);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(6);
            }
        }

        private void DrawSinglePresetCard(int idx, HeroPresetDef p)
        {
            bool selected = _useExactHeroPreset && idx == _activePresetIndex;
            Rect r = GUILayoutUtility.GetRect(180, 54, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, selected ? ColBgCardSel : ColBgCard);
            DrawOutline(r, selected ? ColAmber : ColBorder, selected ? 1.5f : 1f);

            // Swatch circle/chip on top-right
            if (ColorUtility.TryParseHtmlString(p.BaseHex, out Color sw))
            {
                Rect dot = new Rect(r.xMax - 22, r.y + 10, 12, 12);
                EditorGUI.DrawRect(dot, sw);
                DrawOutline(dot, new Color(1, 1, 1, 0.35f));
            }

            GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 42, 18), p.Name, _stCardTitle);
            GUI.Label(new Rect(r.x + 12, r.y + 28, r.width - 24, 16), p.Subtitle, _stCardSub);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                ApplyHeroPreset(idx, regenerateCharacter: false);
            }
        }

        private void DrawBuilderSliders()
        {
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
            if (EditorGUI.EndChangeCheck())
            {
                _useExactHeroPreset = false;
                _previewDirty = true;
                Repaint();
            }
        }

        private void DrawShaderSliders()
        {
            Material m = _activeMaterial != null ? _activeMaterial : _previewHairMat;
            if (m == null) return;
            EditorGUI.BeginChangeCheck();
            ColorField(m, "_RootColor", "Root Color");
            ColorField(m, "_BaseColor", "Mid / Base Color");
            ColorField(m, "_TipColor", "Tip Color");
            FloatSlider(m, "_HeadCenterBlend", "Radial Normal Smooth", 0f, 0.7f);
            FloatSlider(m, "_AlphaClip", "Tip Feather Cutoff", 0.05f, 0.85f);
            FloatSlider(m, "_GrooveDepth", "Carved Groove Depth", 0.1f, 1f);
            FloatSlider(m, "_RootAO", "Root AO", 0f, 1f);
            ColorField(m, "_PrimarySpecColor", "Primary Specular");
            FloatSlider(m, "_PrimaryIntensity", "Primary Intensity", 0f, 2f);
            ColorField(m, "_SecondarySpecColor", "Secondary Specular");
            FloatSlider(m, "_SecondaryIntensity", "Secondary Intensity", 0f, 2f);
            FloatSlider(m, "_RimIntensity", "Rim Backlight", 0f, 2f);
            if (EditorGUI.EndChangeCheck())
            {
                if (_activeMaterial != null && _previewHairMat != null) _previewHairMat.CopyPropertiesFromMaterial(_activeMaterial);
                EditorUtility.SetDirty(m);
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void DrawPhysicsSliders()
        {
            if (_activePhysics == null)
            {
                EditorGUILayout.HelpBox("Assign your Character in the 'Character' top tab and click 'Apply to Character' to tune live physics.", MessageType.Info);
                return;
            }
            EditorGUI.BeginChangeCheck();
            _activePhysics.stiffness = EditorGUILayout.Slider("Stiffness", _activePhysics.stiffness, 0.05f, 0.95f);
            _activePhysics.gravity = EditorGUILayout.Slider("Gravity", _activePhysics.gravity, 0f, 12f);
            _activePhysics.damping = EditorGUILayout.Slider("Damping", _activePhysics.damping, 0.01f, 0.35f);
            _activePhysics.headInertia = EditorGUILayout.Slider("Head Inertia", _activePhysics.headInertia, 0f, 1.5f);
            _activePhysics.bendingSmoothness = EditorGUILayout.Slider("Anti-Kink Smoothness", _activePhysics.bendingSmoothness, 0f, 0.95f);
            _activePhysics.enableInterLockPush = EditorGUILayout.Toggle("Inter-Lock Push (Rest-Aware)", _activePhysics.enableInterLockPush);
            _activePhysics.interLockRadius = EditorGUILayout.Slider("Inter-Lock Radius (m)", _activePhysics.interLockRadius, 0.005f, 0.045f);
            _activePhysics.interLockStrength = EditorGUILayout.Slider("Inter-Lock Strength", _activePhysics.interLockStrength, 0f, 1f);
            _activePhysics.fallbackWindStrength = EditorGUILayout.Slider("Indoor Fallback Wind", _activePhysics.fallbackWindStrength, 0f, 3f);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_activePhysics);
        }

        private void DrawSceneCameraControls()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Hero"))  { _orbit = new Vector2(20f, 8f);  _zoom = 0.46f; Repaint(); }
            if (GUILayout.Button("Front")) { _orbit = new Vector2(0f, 5f);   _zoom = 0.44f; Repaint(); }
            if (GUILayout.Button("Left"))  { _orbit = new Vector2(-90f, 5f); _zoom = 0.46f; Repaint(); }
            if (GUILayout.Button("Right")) { _orbit = new Vector2(90f, 5f);  _zoom = 0.46f; Repaint(); }
            if (GUILayout.Button("Back"))  { _orbit = new Vector2(180f, 7f); _zoom = 0.46f; Repaint(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUI.BeginChangeCheck();
            _builder.overallScale = EditorGUILayout.Slider("Character Head Scale", _builder.overallScale, 0.70f, 1.40f);
            _builder.headLocalOffset = EditorGUILayout.Vector3Field("Character Head Offset", _builder.headLocalOffset);
            if (EditorGUI.EndChangeCheck()) { _previewDirty = true; Repaint(); }
            _sceneFitHandleActive = EditorGUILayout.Toggle("Show 3D Fit Gizmo in Scene View", _sceneFitHandleActive);
        }

        private void DrawInlineAiInspect()
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_characterRoot == null))
            {
                if (GUILayout.Button("Run 3-Scenario Audit", GUILayout.Height(26)))
                {
                    var rep = AetherStrandAiDiagnostics.CollectReport(_characterRoot, _hairRenderer);
                    _lastDiagMarkdown = AetherStrandAiDiagnostics.FormatMarkdownSummary(rep);
                    if (_activePhysics != null) _lastAudit = _activePhysics.ComputeAudit();
                }
                if (GUILayout.Button("Export AI Bundle", GUILayout.Height(26)))
                {
                    var rep = AetherStrandAiDiagnostics.ExportBundle(_characterRoot, _diagOutputFolder, _hairRenderer);
                    _lastDiagMarkdown = AetherStrandAiDiagnostics.FormatMarkdownSummary(rep);
                    Capture4UpTurnaroundPng(Path.Combine(_diagOutputFolder, "aetherstrand_turnaround_4up.png").Replace('\\', '/'));
                }
                if (GUILayout.Button("Copy MD", GUILayout.Height(26), GUILayout.Width(72)))
                {
                    var rep = AetherStrandAiDiagnostics.CollectReport(_characterRoot, _hairRenderer);
                    _lastDiagMarkdown = AetherStrandAiDiagnostics.FormatMarkdownSummary(rep);
                    EditorGUIUtility.systemCopyBuffer = _lastDiagMarkdown;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastDiagMarkdown))
                EditorGUILayout.TextArea(_lastDiagMarkdown, GUILayout.MinHeight(110));
            else
                EditorGUILayout.HelpBox("Assign a Character and click 'Run 3-Scenario Audit' or 'Export AI Bundle' to generate millimeter alignment deltas, 3-scenario physics audits, and a 4-Up PNG.", MessageType.Info);
        }

        private void DrawCharacterBindingView()
        {
            GUILayout.BeginArea(new Rect(14, 56, position.width - 28, position.height - 68));
            GUILayout.Label("Character Binding & Batch Paths", _stHudTitle);
            EditorGUILayout.Space(8);
            EditorGUI.BeginChangeCheck();
            _characterRoot = (GameObject)EditorGUILayout.ObjectField("Character Root", _characterRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && _characterRoot != null) SyncReferencesFromCharacter();

            _hairRenderer = (Renderer)EditorGUILayout.ObjectField("Hair Renderer (Optional)", _hairRenderer, typeof(Renderer), true);
            _bonePrefix = EditorGUILayout.TextField("Hair Bone Prefix", _bonePrefix);
            _materialOutputPath = EditorGUILayout.TextField("Material Output Path", _materialOutputPath);
            _meshOutputPath = EditorGUILayout.TextField("Mesh Output Path", _meshOutputPath);
            _presetJsonPath = EditorGUILayout.TextField("Look Preset JSON (Opt)", _presetJsonPath);
            _headHeightOverride = EditorGUILayout.FloatField("Head Height Override (0=Auto)", _headHeightOverride);
            _builder.targetChainCount = EditorGUILayout.IntSlider("Physics Bone Chains", _builder.targetChainCount, 16, 64);

            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(_characterRoot == null))
            {
                if (GUILayout.Button("Apply Active Hair Preset to Character (1-Click)", GUILayout.Height(34)))
                    ApplyProceduralHairToCharacter();
            }
            GUILayout.EndArea();
        }

        private void DrawMapsGalleryView()
        {
            GUILayout.BeginArea(new Rect(14, 56, position.width - 28, position.height - 68));
            GUILayout.Label("Bundled Seamless Strand Maps", _stHudTitle);
            EditorGUILayout.Space(8);
            string[] tokens = { "AetherStrand_GrooveAO", "AetherStrand_FlowIDShift", "AetherStrand_Normal", "AetherStrand_AlphaCard" };
            EditorGUILayout.BeginHorizontal();
            foreach (string tk in tokens)
            {
                Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(ResolvePackageRoot() + "/Textures/" + tk + ".png");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(tk.Replace("AetherStrand_", ""), _stCardTitle);
                Rect r = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true));
                if (t != null) GUI.DrawTexture(r, t, ScaleMode.ScaleToFit);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawFullAiSuiteView()
        {
            GUILayout.BeginArea(new Rect(14, 56, position.width - 28, position.height - 68));
            GUILayout.Label("AI Visual Eval & Diagnostic Suite", _stHudTitle);
            EditorGUILayout.Space(6);
            _diagOutputFolder = EditorGUILayout.TextField("Export Folder", _diagOutputFolder);
            DrawInlineAiInspect();
            GUILayout.EndArea();
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
                    _zoom = Mathf.Clamp(_zoom + e.delta.y * 0.025f, 0.26f, 1.05f);
                    e.Use();
                    Repaint();
                }
            }

            if (e.type == EventType.Repaint && rect.width > 16 && rect.height > 16)
            {
                float motionYaw = _motionMode == MotionPreviewMode.HeadTurn ? Mathf.Sin(_simClock * 3.0f) * 32f
                                : _motionMode == MotionPreviewMode.Walk ? Mathf.Sin(_simClock * 4.5f) * 5f : 0f;
                float motionBob = _motionMode == MotionPreviewMode.Walk ? Mathf.Abs(Mathf.Sin(_simClock * 4.5f)) * 0.008f : 0f;

                _preview.BeginPreview(rect, GUIStyle.none);
                Vector3 target = new Vector3(0f, 0.075f + motionBob, 0.030f);
                Quaternion q = Quaternion.Euler(_orbit.y, _orbit.x + 180f, 0f);
                _preview.camera.transform.position = target + q * (Vector3.back * _zoom);
                _preview.camera.transform.LookAt(target, Vector3.up);

                Quaternion headAnim = Quaternion.Euler(0f, motionYaw, 0f);
                Matrix4x4 hairM = Matrix4x4.TRS(new Vector3(0f, motionBob, 0f), headAnim, Vector3.one);
                Matrix4x4 skullM = hairM * Matrix4x4.TRS(new Vector3(0f, 0.088f, 0.042f), Quaternion.identity, new Vector3(0.180f, 0.212f, 0.200f));
                Matrix4x4 neckM  = hairM * Matrix4x4.TRS(new Vector3(0f, -0.042f, 0.022f), Quaternion.identity, new Vector3(0.085f, 0.120f, 0.088f));

                _preview.DrawMesh(_previewBustMesh, skullM, _previewBustMat, 0);
                _preview.DrawMesh(_previewBustMesh, neckM, _previewBustMat, 0);
                if (_previewHairMesh != null && _previewHairMat != null)
                    _preview.DrawMesh(_previewHairMesh, hairM, _previewHairMat, 0);

                _preview.camera.Render();
                Texture tex = _preview.EndPreview();
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
            }
        }

        private void RandomizeShape()
        {
            _builder.seed = (_builder.seed * 1103515245 + 12345) & 0x7FFFFFF;
            _builder.partPosition = Mathf.Lerp(-0.7f, 0.7f, ((_builder.seed >> 4) % 100) / 100f);
            _builder.bangsLength = Mathf.Lerp(0.48f, 0.78f, ((_builder.seed >> 8) % 100) / 100f);
            _builder.crownVolume = Mathf.Lerp(0.30f, 0.80f, ((_builder.seed >> 12) % 100) / 100f);
            _builder.spikeLift = Mathf.Lerp(0.08f, 0.85f, ((_builder.seed >> 16) % 100) / 100f);
            _useExactHeroPreset = false;
            _previewDirty = true;
            Repaint();
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (!_sceneFitHandleActive || _characterRoot == null) return;
            Transform head = AetherStrandMeshGenerator.ResolveHeadBone(_characterRoot);
            if (head == null) return;

            Vector3 worldPos = head.TransformPoint(_builder.headLocalOffset + new Vector3(0f, 0.088f, 0.042f));
            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.PositionHandle(worldPos, head.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                _builder.headLocalOffset = head.InverseTransformPoint(newWorldPos) - new Vector3(0f, 0.088f, 0.042f);
                _previewDirty = true;
                Repaint();
            }
            Handles.Label(worldPos + Vector3.up * 0.14f, "AetherStrand Hair Fit");
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
            _builder.overallScale = 1.0f;
            _builder.headLocalOffset = Vector3.zero;
            _useExactHeroPreset = true;

            ApplyPaletteToMat(_previewHairMat, p);
            if (_activeMaterial != null) ApplyPaletteToMat(_activeMaterial, p);
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

            string sourceJson = _useExactHeroPreset ? GetActiveHeroPresetJsonPath() : null;
            if (string.IsNullOrEmpty(sourceJson))
            {
                var locks = AetherStrandProceduralBuilder.BuildLocks(_builder);
                sourceJson = Path.Combine(Application.temporaryCachePath, "AetherStrand_LiveStudio.json");
                WriteLocksAsPresetJson(locks, sourceJson);
            }

            var sculpt = new HairSculptParams
            {
                overallScale = _useExactHeroPreset ? _builder.overallScale : 1.0f,
                headLocalOffset = _useExactHeroPreset ? _builder.headLocalOffset : Vector3.zero,
                bangsLength = 1.0f,
                layerLength = 1.0f,
                widthScale = 1.0f,
                thicknessScale = 1.0f,
                targetChainCount = _builder.targetChainCount,
                bonesPerChain = _builder.bonesPerChain
            };

            SkinnedMeshRenderer smr = AetherStrandMeshGenerator.GenerateAndAttach(
                _characterRoot, sculpt, sourceJson, _meshOutputPath, _activeMaterial);
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
            using (var sw = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
            {
                sw.WriteLine("{\"schemaVersion\":\"3.0\",\"locks\":[");
                for (int i = 0; i < locks.Count; i++)
                {
                    var lk = locks[i];
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

        public void Capture4UpTurnaroundPng(string outputAssetPath)
        {
            if (_preview == null) InitPreview();
            if (_previewDirty) RebuildPreviewMesh();
            const int cell = 512;
            var finalTex = new Texture2D(cell * 2, cell * 2, TextureFormat.RGBA32, false);
            Vector2[] views = { new Vector2(0f, 5f), new Vector2(180f, 7f), new Vector2(-90f, 5f), new Vector2(90f, 5f) };
            int[] xs = { 0, cell, 0, cell };
            int[] ys = { cell, cell, 0, 0 };

            RenderTexture prevRT = RenderTexture.active;
            for (int v = 0; v < 4; v++)
            {
                _preview.BeginStaticPreview(new Rect(0, 0, cell, cell));
                Vector3 target = new Vector3(0f, 0.075f, 0.030f);
                Quaternion q = Quaternion.Euler(views[v].y, views[v].x + 180f, 0f);
                _preview.camera.transform.position = target + q * (Vector3.back * 0.46f);
                _preview.camera.transform.LookAt(target, Vector3.up);

                Matrix4x4 skullM = Matrix4x4.TRS(new Vector3(0f, 0.088f, 0.042f), Quaternion.identity, new Vector3(0.180f, 0.212f, 0.200f));
                Matrix4x4 neckM  = Matrix4x4.TRS(new Vector3(0f, -0.042f, 0.022f), Quaternion.identity, new Vector3(0.085f, 0.120f, 0.088f));
                _preview.DrawMesh(_previewBustMesh, skullM, _previewBustMat, 0);
                _preview.DrawMesh(_previewBustMesh, neckM, _previewBustMat, 0);
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
        }

        private void SyncReferencesFromCharacter()
        {
            if (_characterRoot == null) return;
            _activePhysics = _characterRoot.GetComponent<AetherStrandPhysics>();
            Transform hairT = _characterRoot.transform.Find("AetherStrand_HairMesh");
            if (hairT != null)
            {
                _hairRenderer = hairT.GetComponent<Renderer>();
                if (_hairRenderer != null && _hairRenderer.sharedMaterial != null) _activeMaterial = _hairRenderer.sharedMaterial;
            }
        }

        private static Bounds? TryComputeHeadBounds(GameObject root)
        {
            if (root == null) return null;
            Transform head = AetherStrandMeshGenerator.ResolveHeadBone(root);
            if (head == null) return null;
            return new Bounds(head.position + head.up * 0.088f + head.forward * 0.042f, new Vector3(0.184f, 0.22f, 0.22f));
        }

        private static void DrawOutline(Rect r, Color c, float t = 1f)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
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
        // Headless Batchmode Static API (100% compatible with Wren's builders)
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
            mat.SetFloat("_HeadCenterBlend", 0.45f);
            mat.SetFloat("_UseAlphaClip", 1f);
            mat.SetFloat("_AlphaClip", 0.36f);
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
            Vector3 skullCenterW = physics.headTransform.position + physics.headTransform.up * 0.088f + physics.headTransform.forward * 0.042f;
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
