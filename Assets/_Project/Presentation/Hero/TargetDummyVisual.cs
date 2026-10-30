// Authority: none (local, cosmetic). Makes the elite dummy look different from the plain ones: another material and a larger body.
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class TargetDummyVisual : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material eliteMaterial;
        [SerializeField] private Transform scaleRoot;
        [SerializeField] private float eliteScale = 1.35f;

        private Targetable _targetable;

        public void Configure(Renderer[] bodyRenderers, Material normal, Material elite, Transform root)
        {
            renderers = bodyRenderers;
            normalMaterial = normal;
            eliteMaterial = elite;
            scaleRoot = root;
        }

        private void OnEnable()
        {
            _targetable = GetComponent<Targetable>();
            if (_targetable == null) return;
            _targetable.EliteChanged += Apply;
            Apply(_targetable.IsElite);
        }

        private void OnDisable()
        {
            if (_targetable != null) _targetable.EliteChanged -= Apply;
        }

        private void Apply(bool elite)
        {
            if (renderers != null)
                foreach (Renderer r in renderers)
                    if (r != null) r.sharedMaterial = elite ? eliteMaterial : normalMaterial;
            if (scaleRoot != null) scaleRoot.localScale = Vector3.one * (elite ? eliteScale : 1f);
        }
    }
}
