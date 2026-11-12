// Not networked; local to each peer. View of the PL-3 UI Toolkit spike (task 3.0): finds the button named ButtonName in the UIDocument's tree,
// forwards Button.clicked to MenuSpikeModel.Activate() and focuses the button so a keyboard or gamepad Submit reaches it.
// Button.clicked fires for a pointer click AND for a NavigationSubmitEvent (what the Input System UI module sends for Enter / gamepad south).
// Keyboard and gamepad events go to the EventSystem's SELECTED object, and a mouse click is the only thing that selects the panel by itself, so the
// view selects the panel's PanelEventHandler object once it exists (a menu with several panels will need to pick the right one).
// The UIDocument rebuilds its visual tree when its UXML or panel changes, so the binding is re-checked each frame by root identity.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace SleepingCalamity.App
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuSpikeView : MonoBehaviour
    {
        public const string ButtonName = "spike-button";

        private UIDocument document;
        private VisualElement boundRoot;
        private Button button;
        private bool panelSelected;

        public MenuSpikeModel Model { get; } = new MenuSpikeModel();

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            if (document != null && document.rootVisualElement != boundRoot) Bind();
            if (!panelSelected && button != null) SelectPanel();
        }

        private void SelectPanel()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            PanelEventHandler[] handlers = FindObjectsByType<PanelEventHandler>(FindObjectsSortMode.None);
            if (handlers.Length == 0) return;
            eventSystem.SetSelectedGameObject(handlers[0].gameObject);
            panelSelected = true;
        }

        private void Bind()
        {
            Unbind();
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return;
            boundRoot = root;
            button = root.Q<Button>(ButtonName);
            if (button == null) return;
            button.clicked += OnClicked;
            button.Focus();
        }

        private void Unbind()
        {
            if (button != null) button.clicked -= OnClicked;
            button = null;
            boundRoot = null;
            panelSelected = false;
        }

        private void OnClicked()
        {
            Model.Activate();
        }
    }
}
