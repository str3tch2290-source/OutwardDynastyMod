using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OutwardDynasty
{
    public class CompanionClient : MonoBehaviour
    {
        public static CompanionClient Instance;

        private const string BaseUrl = "http://127.0.0.1:9876";
        private const string HostToken = "changeme";

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SendDynastySnapshot()
        {
            DynastySaveData data = DynastyDataAccess.Get();
            if (data == null) return;

            string json = JsonUtility.ToJson(new Snapshot
            {
                day_count = data.DayCount,
                apocalypse_active = data.IsApocalypseActive,
                scourge_multiplier = data.ScourgeMultiplier,
                dynasty = data
            });

            StartCoroutine(Post(json));
        }

        private IEnumerator Post(string json)
        {
            UnityWebRequest req = null;
            try
            {
                req = new UnityWebRequest(BaseUrl + "/host/state", "POST");
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Host-Token", HostToken);

                yield return req.SendWebRequest();
            }
            finally
            {
                if (req != null)
                    req.Dispose();
            }
        }

        [Serializable]
        private class Snapshot
        {
            public int day_count;
            public bool apocalypse_active;
            public float scourge_multiplier;
            public DynastySaveData dynasty;
        }
    }
}
