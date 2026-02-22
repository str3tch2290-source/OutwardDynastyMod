using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Bottom-left movable "Force Resync" button shown only when Dynasty Mode is enabled.
    /// Calls Companion resync_request.
    /// </summary>
    public class ForceResyncWidget : MonoBehaviour
    {
        private Rect _rect = new Rect(20, 0, 180, 60);
        private bool _drag;
        private Vector2 _dragOffset;

        private string _status = "";

        private void Start()
        {
            // place at bottom left initially
            _rect.y = Screen.height - _rect.height - 20;
        }

        private void OnGUI()
        {
            var core = DynastyCore.Instance;
            if (core == null || !core.IsDynastyModeEnabled) return;

            GUI.Box(_rect, "");

            var btnRect = new Rect(_rect.x + 10, _rect.y + 10, _rect.width - 20, 25);
            if (GUI.Button(btnRect, "Force Resync"))
            {
                var cc = CompanionClient.Instance;
                if (cc != null)
                {
                    if (cc.RequestResync(out var s)) _status = "Resync requested.";
                    else _status = s ?? "Resync failed.";
                }
                else _status = "No Companion client.";
            }

            GUI.Label(new Rect(_rect.x + 10, _rect.y + 38, _rect.width - 20, 18), _status);

            HandleDrag();
        }

        private void HandleDrag()
        {
            var e = Event.current;
            if (e == null) return;

            if (e.type == EventType.MouseDown && _rect.Contains(e.mousePosition))
            {
                _drag = true;
                _dragOffset = e.mousePosition - new Vector2(_rect.x, _rect.y);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _drag = false;
            }
            else if (e.type == EventType.MouseDrag && _drag)
            {
                _rect.x = e.mousePosition.x - _dragOffset.x;
                _rect.y = e.mousePosition.y - _dragOffset.y;

                // clamp
                _rect.x = Mathf.Clamp(_rect.x, 0, Screen.width - _rect.width);
                _rect.y = Mathf.Clamp(_rect.y, 0, Screen.height - _rect.height);
                e.Use();
            }
        }
    }
}
