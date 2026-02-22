using UnityEngine;

namespace OutwardDynasty
{
    public class AuthorityHUD : MonoBehaviour
    {
        private GUIStyle _style;
        private GUIStyle _shadow;

        private void Ensure()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            _style.normal.textColor = new Color(1f, 1f, 1f, 0.95f);
            _shadow = new GUIStyle(_style);
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
        }

        private void OnGUI()
        {
            Ensure();
            var cc = CompanionClient.Instance;
            if (cc == null) return;
            if (!cc.Halted) return;

            var msg = string.IsNullOrEmpty(cc.LastMessage) ? "Companion authority halted the game (sync required)." : cc.LastMessage;
            float w = Screen.width * 0.9f;
            float h = 80f;
            float x = (Screen.width - w) / 2f;
            float y = 10f;
            var r = new Rect(x, y, w, h);

            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), msg, _shadow);
            GUI.Label(r, msg, _style);
        }
    }
}
