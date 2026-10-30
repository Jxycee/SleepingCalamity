// Authority: none (local, cosmetic, every peer). Builds the held heirloom out of primitives at runtime (axe, staff, dagger and shield, sword and bow), swings it
// from the hero's attack frame (the owner's AttackMachine, or the replicated NetSwing clock on a remote copy), shows the shield while guarding and the bow
// while aiming, and lays the mannequin down while the hero is downed. Greybox. With the Animator rig (the Y Bot and its clips) the weapon, shield and
// bow are parented to the hands and the clips do the swinging and the lying down; the procedural swing below is then the fallback for the mannequin.
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [DefaultExecutionOrder(110)]
    public sealed class HeroWeaponVisual : MonoBehaviour
    {
        [SerializeField] private Material weaponMaterial, accentMaterial;
        [SerializeField] private Transform visual;
        [SerializeField] private HandGrip rightGrip, leftGrip, shieldGrip;   // set by the builder from the model's rest pose (T-pose); see PrefabBuilder

        // Where a held thing sits in a bone's local space: the builder measures it from the rest pose so it does not depend on how the rig's bones are oriented.
        [System.Serializable]
        public struct HandGrip
        {
            public Quaternion rotation;
            public Vector3 position;
            public bool valid;
        }

        private Animator _rig;
        private Transform _rightHand, _leftHand, _leftForearm;

        private HeroCombat _combat;
        private HeroVitals _vitals;
        private Transform _pivot, _shield, _bow;
        private GameObject[] _weapons;
        private int _shown = -1;

        public void Configure(Material weapon, Material accent, Transform visualRoot)
        {
            weaponMaterial = weapon;
            accentMaterial = accent;
            visual = visualRoot;
        }

        public void ConfigureGrips(HandGrip right, HandGrip left, HandGrip shield)
        {
            rightGrip = right;
            leftGrip = left;
            shieldGrip = shield;
        }

        private void Awake()
        {
            _combat = GetComponent<HeroCombat>();
            _vitals = GetComponent<HeroVitals>();
        }

        private void Build()
        {
            _pivot = new GameObject("WeaponPivot").transform;
            _pivot.SetParent(transform, false);
            _pivot.localPosition = new Vector3(0.38f, 1.05f, 0.15f);
            _weapons = new GameObject[4];

            // Axe: a shaft with a broad head.
            _weapons[0] = Holder("Axe");
            Prim(_weapons[0], PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.07f, 0.95f, 0.07f), weaponMaterial);
            Prim(_weapons[0], PrimitiveType.Cube, new Vector3(0f, 0.85f, 0.12f), new Vector3(0.07f, 0.26f, 0.32f), accentMaterial);

            // Staff: a long pole with an orb.
            _weapons[1] = Holder("Staff");
            Prim(_weapons[1], PrimitiveType.Cylinder, new Vector3(0f, 0.7f, 0f), new Vector3(0.05f, 0.85f, 0.05f), weaponMaterial);
            Prim(_weapons[1], PrimitiveType.Sphere, new Vector3(0f, 1.6f, 0f), new Vector3(0.2f, 0.2f, 0.2f), accentMaterial);

            // Dagger (the shield is separate so it can come up in front while guarding).
            _weapons[2] = Holder("Dagger");
            Prim(_weapons[2], PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f), new Vector3(0.05f, 0.42f, 0.03f), accentMaterial);
            Prim(_weapons[2], PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.14f, 0.04f, 0.05f), weaponMaterial);

            // Sword (the bow comes out while aiming).
            _weapons[3] = Holder("Sword");
            Prim(_weapons[3], PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.06f, 0.82f, 0.02f), accentMaterial);
            Prim(_weapons[3], PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(0.22f, 0.05f, 0.05f), weaponMaterial);

            _shield = new GameObject("Shield").transform;
            _shield.SetParent(transform, false);
            GameObject disc = Prim(_shield.gameObject, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.7f, 0.04f, 0.7f), accentMaterial);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _shield.gameObject.SetActive(false);

            _bow = new GameObject("Bow").transform;
            _bow.SetParent(transform, false);
            Prim(_bow.gameObject, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.04f, 1.1f, 0.05f), weaponMaterial);
            Prim(_bow.gameObject, PrimitiveType.Cube, new Vector3(0f, 0.5f, -0.1f), new Vector3(0.03f, 0.3f, 0.03f), weaponMaterial).transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            Prim(_bow.gameObject, PrimitiveType.Cube, new Vector3(0f, -0.5f, -0.1f), new Vector3(0.03f, 0.3f, 0.03f), weaponMaterial).transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            _bow.gameObject.SetActive(false);
            AttachToRig();
        }

        // With a humanoid Animator under the visual and grips from the builder, hang the pivot on the right hand, the bow on the left hand and the shield
        // on the left forearm. The parents' own scale is cancelled so the primitives keep their metre sizes.
        private void AttachToRig()
        {
            Animator rig = visual != null ? visual.GetComponentInChildren<Animator>(true) : null;
            if (rig == null || !rig.isHuman || rig.runtimeAnimatorController == null || !rightGrip.valid) return;
            _rightHand = rig.GetBoneTransform(HumanBodyBones.RightHand);
            _leftHand = rig.GetBoneTransform(HumanBodyBones.LeftHand);
            _leftForearm = rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (_rightHand == null || _leftHand == null || _leftForearm == null) return;
            _rig = rig;
            Hang(_pivot, _rightHand, rightGrip);
            Hang(_bow, _leftHand, leftGrip);
            Hang(_shield, _leftForearm, shieldGrip);
        }

        private static void Hang(Transform item, Transform bone, HandGrip grip)
        {
            item.SetParent(bone, false);
            Vector3 s = bone.lossyScale;
            item.localScale = new Vector3(s.x != 0f ? 1f / s.x : 1f, s.y != 0f ? 1f / s.y : 1f, s.z != 0f ? 1f / s.z : 1f);
            item.localPosition = grip.position;   // measured in the bone's own local space, so its scale is already in it
            item.localRotation = grip.rotation;
        }

        private GameObject Holder(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_pivot, false);
            go.SetActive(false);
            return go;
        }

        private static GameObject Prim(GameObject parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material material)
        {
            GameObject p = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(p.GetComponent<Collider>());   // not Destroy: a collider that lives to the end of the frame can shove a character controller
            p.transform.SetParent(parent.transform, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            if (material != null) p.GetComponent<Renderer>().sharedMaterial = material;
            return p;
        }

        private void LateUpdate()
        {
            if (_combat == null || !_combat.IsSpawned) return;
            if (_pivot == null) Build();
            int idx = Mathf.Clamp(_combat.WeaponIndex, 0, 3);
            if (idx != _shown)
            {
                for (int i = 0; i < _weapons.Length; i++) _weapons[i].SetActive(i == idx);
                _shown = idx;
            }
            WeaponFamily family = _combat.Family;
            bool guard = family == WeaponFamily.Dagger && _combat.ShowGuard;
            bool aim = family == WeaponFamily.Bow && _combat.ShowAim;
            _shield.gameObject.SetActive(family == WeaponFamily.Dagger);
            _bow.gameObject.SetActive(aim);
            if (_rig != null && family == WeaponFamily.Bow) _weapons[idx].SetActive(!aim);   // the sword is put away while the bow is drawn
            if (_rig == null && family == WeaponFamily.Dagger)
            {
                _shield.localPosition = guard ? new Vector3(0f, 1.1f, 0.5f) : new Vector3(-0.42f, 1.0f, 0.12f);
                _shield.localRotation = guard ? Quaternion.identity : Quaternion.Euler(0f, 70f, 0f);
            }
            if (_rig == null && aim)
            {
                _bow.localPosition = new Vector3(-0.1f, 1.25f, 0.55f);
                _bow.localRotation = Quaternion.identity;
            }

            if (_rig != null) return;   // the clips swing the arm and lay the hero down

            _pivot.localRotation = PivotRotation(_combat.CurrentAttack, _combat.AttackFrame, aim);

            // Downed: lie the mannequin down (the feeder's lean runs first, this overrides it).
            if (visual != null && _vitals != null && !_vitals.IsUp)
            {
                visual.localRotation = Quaternion.Euler(80f, 0f, 0f);
                visual.localPosition = new Vector3(0f, 0.35f, 0f);
            }
        }

        private static Quaternion PivotRotation(AttackData a, float frame, bool aiming)
        {
            if (aiming) return Quaternion.Euler(90f, 0f, 0f);
            Quaternion rest = Quaternion.Euler(35f, 0f, -8f);
            if (a == null) return rest;
            float start = a.HitOn.Start, end = a.HitOn.End, total = a.TotalFrames;
            float wind = Mathf.Clamp01(frame / Mathf.Max(1f, start));
            float act = Mathf.Clamp01((frame - start) / Mathf.Max(1f, end - start));
            float rec = Mathf.Clamp01((frame - end) / Mathf.Max(1f, total - end));
            bool second = a.Id.EndsWith("_2") || a.Id.Contains("_2_");
            float dir = second ? -1f : 1f;

            if (a.Shape == HitShape.Ray) return Quaternion.Euler(90f, 0f, 0f);
            Quaternion cocked, swing;
            switch (a.Kind)
            {
                case AttackKind.Finisher:
                    cocked = Quaternion.Euler(-50f, 0f, 0f);
                    swing = Quaternion.Euler(Mathf.Lerp(-50f, 135f, act), 0f, 0f);
                    break;
                case AttackKind.Spell:
                    cocked = Quaternion.Euler(-10f, 0f, 0f);
                    swing = Quaternion.Euler(Mathf.Lerp(-10f, 110f, act), 0f, 0f);
                    break;
                default:
                    if (a.Shape == HitShape.Circle)
                    {
                        cocked = Quaternion.Euler(0f, -40f, 0f) * Quaternion.Euler(90f, 0f, 0f);
                        swing = Quaternion.Euler(0f, Mathf.Lerp(-40f, 320f, act), 0f) * Quaternion.Euler(90f, 0f, 0f);
                    }
                    else
                    {
                        float half = Mathf.Min(80f, a.HalfAngleDeg);
                        cocked = Quaternion.Euler(0f, -half * dir, 0f) * Quaternion.Euler(90f, 0f, 0f);
                        swing = Quaternion.Euler(0f, Mathf.Lerp(-half * dir, half * dir, act), 0f) * Quaternion.Euler(90f, 0f, 0f);
                    }
                    break;
            }
            if (frame < start) return Quaternion.Slerp(rest, cocked, wind);
            if (frame < end) return swing;
            return Quaternion.Slerp(swing, rest, rec);
        }
    }
}
