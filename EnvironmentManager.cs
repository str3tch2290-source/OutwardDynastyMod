using UnityEngine;
using OutwardDynasty;

namespace OutwardDynasty
{
    public class EnvironmentManager : MonoBehaviour
    {
        private DynastyCore _core;

        private Light _sun;
        private Color _normalSunColor;
        private Color _normalAmbient;
        private bool _sunCached = false;

        public void Initialize(DynastyCore core)
        {
            _core = core;
            CacheSun();
        }

        private void CacheSun()
        {
            if (_sunCached) return;

            _sun = RenderSettings.sun;
            _normalAmbient = RenderSettings.ambientLight;

            if (_sun != null)
            {
                _normalSunColor = _sun.color;
            }

            _sunCached = true;
        }

        private void Update()
        {
            if (_core == null) return;

            // Remove fog entirely in Dynasty mode
            if (_core.IsDynastyModeEnabled)
            {
                RenderSettings.fog = false;
            }

            ApplyScourgeLighting();
        }

        private void ApplyScourgeLighting()
        {
            if (!_sunCached)
            {
                CacheSun();
            }

            if (_sun == null) return;

            if (_core.MasterData.IsApocalypseActive)
            {
                // Purple sun during Scourge
                _sun.color = new Color(0.6f, 0.2f, 0.8f);
                _sun.intensity = 1.2f;
                RenderSettings.ambientLight = new Color(0.4f, 0.1f, 0.6f);
            }
            else
            {
                // Restore normal lighting
                _sun.color = _normalSunColor;
                _sun.intensity = 1.0f;
                RenderSettings.ambientLight = _normalAmbient;
            }
        }
    }
}
