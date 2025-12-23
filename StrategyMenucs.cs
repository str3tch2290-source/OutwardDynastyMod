using UnityEngine;

namespace OutwardDynasty
{
    public class StrategyMenu : MonoBehaviour
    {
        private bool _isOpen = false;
        private Rect _windowRect = new Rect(100, 100, 600, 400);

        void Update()
        {
            // Explicit namespace prevents CS1069 and CS0117
            if (UnityEngine.Input.GetKeyDown(KeyCode.P))
            {
                _isOpen = !_isOpen;
                Cursor.visible = _isOpen;
                Cursor.lockState = _isOpen ? CursorLockMode.None : CursorLockMode.Locked;
            }
        }

        void OnGUI()
        {
            if (!_isOpen) return;
            _windowRect = GUI.Window(1002, _windowRect, DrawWindow, "Dynasty Strategy");
        }

        void DrawWindow(int id)
        {
            GUILayout.Label("Strategy Dashboard Active");
            if (GUILayout.Button("Close")) _isOpen = false;
            GUI.DragWindow();
        }
    }
}