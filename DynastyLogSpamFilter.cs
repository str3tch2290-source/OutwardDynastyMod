using System;
using UnityEngine;

namespace OutwardDynasty
{
    // This actually filters BEFORE Unity prints.
    public class DynastyLogSpamFilter : MonoBehaviour
    {
        private ILogHandler _prev;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            _prev = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new FilteringHandler(_prev);

            Debug.Log("[Dynasty] LogSpamFilter installed (pre-print).");
        }

        private void OnDestroy()
        {
            if (_prev != null && Debug.unityLogger.logHandler is FilteringHandler)
                Debug.unityLogger.logHandler = _prev;
        }

        private class FilteringHandler : ILogHandler
        {
            private readonly ILogHandler _inner;

            public FilteringHandler(ILogHandler inner) => _inner = inner;

            public void LogException(Exception exception, UnityEngine.Object context)
                => _inner.LogException(exception, context);

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                // never hide errors/exceptions/asserts
                if (logType == LogType.Error || logType == LogType.Exception || logType == LogType.Assert)
                {
                    _inner.LogFormat(logType, context, format, args);
                    return;
                }

                string msg;
                try { msg = (args != null && args.Length > 0) ? string.Format(format, args) : format; }
                catch { msg = format; }

                if (IsSpammy(msg))
                    return; // swallow

                _inner.LogFormat(logType, context, format, args);
            }

            private static bool IsSpammy(string s)
            {
                if (string.IsNullOrEmpty(s)) return false;

                if (s.IndexOf("Failed to create agent because it is not close enough to the NavMesh", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (s.IndexOf("is registered with more than one LODGroup", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (s.IndexOf("Particle System is trying to spawn on a mesh with zero surface area", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (s.IndexOf("BoxColliders does not support negative scale or size", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                return false;
            }
        }
    }
}
