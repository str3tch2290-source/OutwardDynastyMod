using UnityEngine;

namespace OutwardDynasty
{
    public class DynastyHUD : MonoBehaviour
    {
        private DynastyCore _core;
        private GUIStyle _debugStyle;
        private GUIStyle _debugShadow;

        public void Initialize(DynastyCore core) => _core = core;

        private void EnsureStyles()
        {
            if (_debugStyle != null) return;

            _debugStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperRight,
                wordWrap = false
            };
            _debugStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);

            _debugShadow = new GUIStyle(_debugStyle);
            _debugShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        private void OnGUI()
        {
            EnsureStyles();

            // Draw ALWAYS (even if core/data are missing)
            string coreStr = (_core == null) ? "NULL" : "OK";
            var data = DynastyDataAccess.Get();

            // 8 lines
            string msg =
                $"[DynastyHUD ALIVE]\n" +
                $"Core: {coreStr}\n" +
                $"Data: {(data == null ? "NULL" : "OK")}\n" +
                $"Frame: {Time.frameCount}\n" +
                $"Time: {Time.time:0.0}\n" +
                $"Scale: {Time.timeScale:0.00}\n" +
                $"Quests: {CountActiveQuests(data)} active\n" +
                $"Arcs: {CountActiveArcs(data)} active";

            float w = 300f, h = 140f, m = 12f;
            var r = new Rect(Screen.width - w - m, m, w, h);

            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), msg, _debugShadow);
            GUI.Label(r, msg, _debugStyle);
        }

        private static int CountActiveQuests(DynastySaveData data)
        {
            try
            {
                if (data == null || data.DynastyQuests == null) return 0;
                int c = 0;
                foreach (var q in data.DynastyQuests)
                    if (q != null && q.Status == DynastyQuestStatus.Active) c++;
                return c;
            }
            catch { return 0; }
        }

        private static int CountActiveArcs(DynastySaveData data)
        {
            try
            {
                if (data == null || data.DynastyArcs == null) return 0;
                int c = 0;
                foreach (var a in data.DynastyArcs)
                    if (a != null && a.Status == DynastyQuestStatus.Active) c++;
                return c;
            }
            catch { return 0; }
        }
    }
}
