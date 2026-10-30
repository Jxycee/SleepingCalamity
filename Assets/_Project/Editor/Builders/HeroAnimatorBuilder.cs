// Builds the hero's AnimatorController from HeroClipTable (never edited by hand). Parameters and ids come from Core's HeroAnimParam / HeroAnimMap, the same
// ones AnimatorHeroDriver writes.
//   Layer 0 Base    Free (1D by Speed: idle, walk, jog, run) | Strafe (2D by MoveX/MoveY, the locked-on set) | JumpStart > JumpAir > JumpLand |
//                   Dodge (2D by the dodge direction, played by DodgeTime) | Guard | Aim (2D aim walk) | Downed > GetUp
//   Layer 1 Attack  Empty, and one state per (heirloom, attack slot), entered on Attack == id and played by AttackTime (the combat clock), so the clip's
//                   length never has to match the attack's frame data
//   Layer 2 Hurt    Empty, HurtLight, HurtHeavy on triggers
// The controller keeps its GUID across rebuilds: it is emptied and refilled in place.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HeroAnimatorBuilder
    {
        public const string ControllerPath = AnimationImportBuilder.Root + "/HeroAnimator.controller";

        static readonly string[] DirNames = { "Forward", "ForwardRight", "Right", "BackwardRight", "Backward", "BackwardLeft", "Left", "ForwardLeft" };
        static readonly Vector2[] DirVecs =
        {
            new Vector2(0f, 1f), new Vector2(0.7071f, 0.7071f), new Vector2(1f, 0f), new Vector2(0.7071f, -0.7071f),
            new Vector2(0f, -1f), new Vector2(-0.7071f, -0.7071f), new Vector2(-1f, 0f), new Vector2(-0.7071f, 0.7071f),
        };

        // What a clean build must contain; the EditMode test checks a built controller against these.
        public static readonly string[] BaseStates = { "Free", "Strafe", "JumpStart", "JumpAir", "JumpLand", "Dodge", "Guard", "Aim", "Downed", "GetUp" };
        public static readonly string[] FloatParams = { HeroAnimParam.Speed, HeroAnimParam.MoveX, HeroAnimParam.MoveY, HeroAnimParam.VertSpeed, HeroAnimParam.AttackTime, HeroAnimParam.DodgeTime };
        public static readonly string[] BoolParams = { HeroAnimParam.Grounded, HeroAnimParam.Locked, HeroAnimParam.Dodging, HeroAnimParam.Guarding, HeroAnimParam.Aiming, HeroAnimParam.Downed };
        public static readonly string[] IntParams = { HeroAnimParam.Attack, HeroAnimParam.Weapon };
        public static readonly string[] TriggerParams = { HeroAnimParam.Hurt, HeroAnimParam.HurtHeavy };

        public static AnimatorController Build() => Build(ControllerPath, null);

        public static AnimatorController Build(string path, List<string> placeholders)
        {
            var clips = new ClipResolver();
            var slots = HeroClipTable.Slots();
            var byId = new Dictionary<string, AnimationClip>();
            var missing = new List<string>();
            foreach (ClipSlot slot in slots)
            {
                AnimationClip clip = clips.Resolve(slot, out string used);
                if (clip == null) { missing.Add(slot.Id); continue; }
                byId[slot.Id] = clip;
                if (slot.OwedFromMixamo && used != slot.Sources[0])
                {
                    string line = "SC ANIM placeholder " + slot.Id + " <- " + used + " (owed Mixamo: " + slot.Sources[0].Substring(3) + ")";
                    placeholders?.Add(line);
                    Debug.Log(line);
                }
            }
            if (missing.Count > 0) throw new InvalidOperationException("Hero animator: no clip found for " + string.Join(", ", missing) + " (are the FBX imported?)");

            BuilderUtil.EnsureFolder(BuilderUtil.Folder(path));
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            // Unity gives every state and blend tree a new random fileID on each rebuild, so rebuilding in place would rewrite the whole file. The controller is
            // therefore a pure function of its inputs (the slot table, the clips it resolved to and those FBX files' import hashes, and this builder's Version):
            // that signature is kept in the controller's .meta (userData), and a matching one means the file on disk is already right and is left alone.
            string signature = Signature(slots, byId, clips);
            if (ctrl != null && AssetImporter.GetAtPath(path) is AssetImporter existing && existing.userData == signature) return ctrl;

            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);   // comes with a default "Base Layer"
            Clear(ctrl, path);

            foreach (string n in FloatParams) ctrl.AddParameter(n, AnimatorControllerParameterType.Float);
            foreach (string n in BoolParams)
                ctrl.AddParameter(new AnimatorControllerParameter { name = n, type = AnimatorControllerParameterType.Bool, defaultBool = n == HeroAnimParam.Grounded });
            foreach (string n in IntParams) ctrl.AddParameter(n, AnimatorControllerParameterType.Int);
            foreach (string n in TriggerParams) ctrl.AddParameter(n, AnimatorControllerParameterType.Trigger);

            BuildBase(ctrl, byId);
            BuildAttack(ctrl, byId, slots);
            BuildHurt(ctrl, byId);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null) { importer.userData = signature; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        // Bump when the controller's wiring (states, transitions, parameters, thresholds) changes in code, so existing controllers rebuild once.
        public const int Version = 1;

        static string Signature(List<ClipSlot> slots, Dictionary<string, AnimationClip> byId, ClipResolver clips)
        {
            var sb = new System.Text.StringBuilder("v" + Version + ";");
            foreach (ClipSlot slot in slots)
            {
                AnimationClip clip = byId[slot.Id];
                string clipPath = AssetDatabase.GetAssetPath(clip);
                sb.Append(slot.Id).Append('|').Append(slot.Mirror).Append('|').Append(clipPath).Append('|').Append(clip.name).Append('|')
                  .Append(AssetDatabase.GetAssetDependencyHash(clipPath)).Append(';');
            }
            using (var sha = System.Security.Cryptography.SHA1.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "");
        }

        static void Clear(AnimatorController ctrl, string path)
        {
            ctrl.layers = new AnimatorControllerLayer[0];
            ctrl.parameters = new AnimatorControllerParameter[0];
            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub != null && sub != ctrl) UnityEngine.Object.DestroyImmediate(sub, true);
        }

        // ---- layer 0 ----

        static void BuildBase(AnimatorController ctrl, Dictionary<string, AnimationClip> c)
        {
            AnimatorStateMachine sm = NewLayer(ctrl, "Base", false);

            AnimatorState free = TreeState(ctrl, sm, "Free", T1(ctrl, "Free", HeroAnimParam.Speed,
                (c["Idle"], 0f), (c["Walk"], MovementMath.WalkSpeed), (c["Jog"], MovementMath.JogSpeed), (c["Run"], MovementMath.RunSpeed)));

            var strafe = new List<ChildMotion> { Child(c["LockIdle"], Vector2.zero, 1f) };
            float walkR = MovementMath.WalkSpeed / MovementMath.RunSpeed, jogR = MovementMath.JogSpeed / MovementMath.RunSpeed;
            for (int i = 0; i < 8; i++)
            {
                strafe.Add(Child(c["Walk_" + DirNames[i]], DirVecs[i] * walkR, 1f));
                strafe.Add(Child(c["Run_" + DirNames[i]], DirVecs[i] * jogR, 1f));
                strafe.Add(Child(c["Run_" + DirNames[i]], DirVecs[i], MovementMath.RunSpeed / MovementMath.JogSpeed));   // top speed: the same cycle, faster
            }
            AnimatorState strafeState = TreeState(ctrl, sm, "Strafe", T2(ctrl, "Strafe", BlendTreeType.FreeformDirectional2D, HeroAnimParam.MoveX, HeroAnimParam.MoveY, strafe));

            AnimatorState jumpStart = Clip(sm, "JumpStart", c["JumpStart"]);
            AnimatorState jumpAir = Clip(sm, "JumpAir", c["JumpAir"]);
            AnimatorState jumpLand = Clip(sm, "JumpLand", c["JumpLand"]);

            var dodge = new List<ChildMotion>
            {
                Child(c["DodgeForward"], new Vector2(0f, 1f), 1f), Child(c["DodgeBack"], new Vector2(0f, -1f), 1f),
                Child(c["DodgeLeft"], new Vector2(-1f, 0f), 1f), Child(c["DodgeRight"], new Vector2(1f, 0f), 1f),
            };
            AnimatorState dodgeState = TreeState(ctrl, sm, "Dodge", T2(ctrl, "Dodge", BlendTreeType.SimpleDirectional2D, HeroAnimParam.MoveX, HeroAnimParam.MoveY, dodge));
            dodgeState.timeParameterActive = true;
            dodgeState.timeParameter = HeroAnimParam.DodgeTime;

            AnimatorState guard = Clip(sm, "Guard", c["Guard"]);

            float aimR = 0.34f;
            var aim = new List<ChildMotion> { Child(c["AimHold"], Vector2.zero, 1f) };
            foreach (string d in new[] { "ForwardRight", "Right", "BackwardRight", "BackwardLeft", "Left", "ForwardLeft" })
                aim.Add(Child(c["AimWalk_" + d], DirVecs[Array.IndexOf(DirNames, d)] * aimR, 1f));
            AnimatorState aimState = TreeState(ctrl, sm, "Aim", T2(ctrl, "Aim", BlendTreeType.FreeformDirectional2D, HeroAnimParam.MoveX, HeroAnimParam.MoveY, aim));

            AnimatorState downed = Clip(sm, "Downed", c["Downed"]);
            AnimatorState getUp = Clip(sm, "GetUp", c["GetUp"]);

            sm.defaultState = free;

            // Any state: lying down beats everything; the dodge beats the rest.
            AnimatorStateTransition t = sm.AddAnyStateTransition(downed);
            Setup(t, 0.12f, false, 0f); t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, HeroAnimParam.Downed);
            t = sm.AddAnyStateTransition(dodgeState);
            Setup(t, 0.06f, false, 0f); t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, HeroAnimParam.Dodging);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, HeroAnimParam.Downed);

            // Free and strafe: jump, guard, aim, and each other.
            foreach (AnimatorState from in new[] { free, strafeState })
            {
                t = from.AddTransition(jumpStart); Setup(t, 0.08f, false, 0f);
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, HeroAnimParam.Grounded);
                t = from.AddTransition(guard); Setup(t, 0.12f, false, 0f);
                t.AddCondition(AnimatorConditionMode.If, 0f, HeroAnimParam.Guarding);
                t = from.AddTransition(aimState); Setup(t, 0.12f, false, 0f);
                t.AddCondition(AnimatorConditionMode.If, 0f, HeroAnimParam.Aiming);
            }
            t = free.AddTransition(strafeState); Setup(t, 0.2f, false, 0f);
            t.AddCondition(AnimatorConditionMode.If, 0f, HeroAnimParam.Locked);
            t = strafeState.AddTransition(free); Setup(t, 0.2f, false, 0f);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, HeroAnimParam.Locked);

            // Jump: start > air (or straight to land), land > locomotion.
            t = jumpStart.AddTransition(jumpLand); Setup(t, 0.08f, false, 0f);
            t.AddCondition(AnimatorConditionMode.If, 0f, HeroAnimParam.Grounded);
            t = jumpStart.AddTransition(jumpAir); Setup(t, 0.15f, true, 0.85f);
            t = jumpAir.AddTransition(jumpLand); Setup(t, 0.06f, false, 0f);
            t.AddCondition(AnimatorConditionMode.If, 0f, HeroAnimParam.Grounded);
            t = jumpLand.AddTransition(jumpAir); Setup(t, 0.06f, false, 0f);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, HeroAnimParam.Grounded);
            ExitTo(jumpLand, free, strafeState, true, 0.75f, 0.12f);

            // Dodge, guard and aim return to whichever locomotion matches the lock.
            ExitTo(dodgeState, free, strafeState, false, 0f, 0.1f, (AnimatorConditionMode.IfNot, HeroAnimParam.Dodging));
            ExitTo(guard, free, strafeState, false, 0f, 0.12f, (AnimatorConditionMode.IfNot, HeroAnimParam.Guarding));
            ExitTo(aimState, free, strafeState, false, 0f, 0.12f, (AnimatorConditionMode.IfNot, HeroAnimParam.Aiming));
            t = guard.AddTransition(jumpStart); Setup(t, 0.08f, false, 0f);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, HeroAnimParam.Grounded);
            t = aimState.AddTransition(jumpStart); Setup(t, 0.08f, false, 0f);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, HeroAnimParam.Grounded);

            // Down, then up.
            t = downed.AddTransition(getUp); Setup(t, 0.1f, false, 0f);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, HeroAnimParam.Downed);
            ExitTo(getUp, free, strafeState, true, 0.8f, 0.2f);
        }

        // ---- layer 1 ----

        static void BuildAttack(AnimatorController ctrl, Dictionary<string, AnimationClip> c, List<ClipSlot> slots)
        {
            AnimatorStateMachine sm = NewLayer(ctrl, "Attack", true);
            AnimatorState empty = sm.AddState("Empty");
            sm.defaultState = empty;
            for (int id = 1; id < HeroAnimMap.AttackIdCount; id++)
            {
                string name = HeroClipTable.AttackStateName((byte)id);
                ClipSlot slot = slots.First(s => s.Id == name);
                AnimatorState st = sm.AddState(name);
                st.motion = c[name];
                st.mirror = slot.Mirror;
                st.timeParameterActive = true;
                st.timeParameter = HeroAnimParam.AttackTime;
                st.writeDefaultValues = false;
                AnimatorStateTransition enter = sm.AddAnyStateTransition(st);
                Setup(enter, 0.06f, false, 0f); enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.Equals, id, HeroAnimParam.Attack);
                AnimatorStateTransition exit = st.AddTransition(empty);
                Setup(exit, 0.12f, false, 0f);
                exit.AddCondition(AnimatorConditionMode.Equals, 0f, HeroAnimParam.Attack);
            }
        }

        // ---- layer 2 ----

        static void BuildHurt(AnimatorController ctrl, Dictionary<string, AnimationClip> c)
        {
            AnimatorStateMachine sm = NewLayer(ctrl, "Hurt", true);
            AnimatorState empty = sm.AddState("Empty");
            sm.defaultState = empty;
            foreach ((string name, string trigger) in new[] { ("HurtLight", HeroAnimParam.Hurt), ("HurtHeavy", HeroAnimParam.HurtHeavy) })
            {
                AnimatorState st = Clip(sm, name, c[name]);
                st.writeDefaultValues = false;
                AnimatorStateTransition enter = sm.AddAnyStateTransition(st);
                Setup(enter, 0.05f, false, 0f); enter.canTransitionToSelf = true;
                enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                AnimatorStateTransition exit = st.AddTransition(empty);
                Setup(exit, 0.15f, true, 0.85f);
            }
        }

        // ---- helpers ----

        static AnimatorStateMachine NewLayer(AnimatorController ctrl, string name, bool overrideLayer)
        {
            ctrl.AddLayer(name);
            AnimatorControllerLayer[] layers = ctrl.layers;
            AnimatorControllerLayer layer = layers[layers.Length - 1];
            layer.defaultWeight = 1f;
            layer.blendingMode = overrideLayer ? AnimatorLayerBlendingMode.Override : layer.blendingMode;
            layers[layers.Length - 1] = layer;
            ctrl.layers = layers;
            return ctrl.layers[layers.Length - 1].stateMachine;
        }

        static AnimatorState Clip(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            AnimatorState st = sm.AddState(name);
            st.motion = clip;
            return st;
        }

        static AnimatorState TreeState(AnimatorController ctrl, AnimatorStateMachine sm, string name, BlendTree tree)
        {
            AnimatorState st = sm.AddState(name);
            st.motion = tree;
            return st;
        }

        static BlendTree T1(AnimatorController ctrl, string name, string parameter, params (AnimationClip clip, float threshold)[] kids)
        {
            var tree = NewTree(ctrl, name);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = parameter;
            tree.useAutomaticThresholds = false;
            tree.children = kids.Select(k => new ChildMotion { motion = k.clip, threshold = k.threshold, timeScale = 1f }).ToArray();
            return tree;
        }

        static BlendTree T2(AnimatorController ctrl, string name, BlendTreeType type, string px, string py, List<ChildMotion> kids)
        {
            var tree = NewTree(ctrl, name);
            tree.blendType = type;
            tree.blendParameter = px;
            tree.blendParameterY = py;
            tree.children = kids.ToArray();
            return tree;
        }

        static BlendTree NewTree(AnimatorController ctrl, string name)
        {
            var tree = new BlendTree { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, AssetDatabase.GetAssetPath(ctrl));
            return tree;
        }

        static ChildMotion Child(AnimationClip clip, Vector2 pos, float timeScale) => new ChildMotion { motion = clip, position = pos, timeScale = timeScale };

        static void Setup(AnimatorStateTransition t, float duration, bool exitTime, float exit)
        {
            t.hasExitTime = exitTime;
            t.exitTime = exit;
            t.hasFixedDuration = true;
            t.duration = duration;
        }

        // Leaves `from` for the free or strafe state by the lock flag, optionally after an exit time, optionally also needing a condition.
        static void ExitTo(AnimatorState from, AnimatorState free, AnimatorState strafe, bool exitTime, float exit, float duration, params (AnimatorConditionMode mode, string param)[] extra)
        {
            foreach ((AnimatorState to, bool locked) in new[] { (free, false), (strafe, true) })
            {
                AnimatorStateTransition t = from.AddTransition(to);
                Setup(t, duration, exitTime, exit);
                foreach ((AnimatorConditionMode mode, string param) in extra) t.AddCondition(mode, 0f, param);
                t.AddCondition(locked ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, HeroAnimParam.Locked);
            }
        }

        // Finds the clip behind a slot's sources.
        sealed class ClipResolver
        {
            readonly Dictionary<string, List<AnimationClip>> _cache = new Dictionary<string, List<AnimationClip>>();
            string[] _mixamo;

            List<AnimationClip> ClipsOf(string path)
            {
                if (!_cache.TryGetValue(path, out List<AnimationClip> list))
                {
                    list = File.Exists(BuilderUtil.Abs(path))
                        ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(a => !a.name.StartsWith("__preview__")).ToList()
                        : new List<AnimationClip>();
                    _cache[path] = list;
                }
                return list;
            }

            public AnimationClip Resolve(ClipSlot slot, out string used)
            {
                foreach (string src in slot.Sources)
                {
                    AnimationClip clip = One(src);
                    if (clip != null) { used = src; return clip; }
                }
                used = null;
                return null;
            }

            AnimationClip One(string src)
            {
                int colon = src.IndexOf(':');
                string kind = src.Substring(0, colon), arg = src.Substring(colon + 1);
                switch (kind)
                {
                    case "Q1": return Named(AnimationImportBuilder.Ual1, arg);
                    case "Q2": return Named(AnimationImportBuilder.Ual2, arg);
                    case "KI":
                    {
                        int slash = arg.IndexOf('/');
                        string path = AnimationImportBuilder.KevinFolder + "/" + arg.Substring(0, slash) + "/HumanM@" + arg.Substring(slash + 1) + ".fbx";
                        List<AnimationClip> list = ClipsOf(path);
                        return list.Count > 0 ? list[0] : null;
                    }
                    case "MX":
                    {
                        if (_mixamo == null)
                            _mixamo = AssetDatabase.FindAssets("t:Model", new[] { AnimationImportBuilder.MixamoFolder }).Select(AssetDatabase.GUIDToAssetPath)
                                .Where(p => p != AnimationImportBuilder.YBot).OrderBy(p => p).ToArray();
                        string best = null;
                        int bestRank = int.MaxValue;
                        foreach (string p in _mixamo)
                        {
                            int rank = HeroClipTable.MatchRank(Path.GetFileNameWithoutExtension(p), arg);
                            if (rank >= 0 && rank < bestRank && ClipsOf(p).Count > 0) { best = p; bestRank = rank; }
                        }
                        if (best != null) return ClipsOf(best)[0];
                        return null;
                    }
                }
                return null;
            }

            AnimationClip Named(string path, string clipName)
            {
                foreach (AnimationClip clip in ClipsOf(path))
                    if (clip.name == clipName || clip.name.EndsWith("|" + clipName)) return clip;
                return null;
            }
        }
    }
}
