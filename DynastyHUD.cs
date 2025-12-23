using UnityEngine;

namespace OutwardDynasty
{
    public class DynastyHUD : MonoBehaviour
    {
        public void Initialize(DynastyCore core) { }

        private void Update()
        {
            DynastySaveData data = DynastyDataAccess.Get();
            if (data == null) return;
        }
    }
}
