using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    /// <summary>
    /// Resolves a user-friendly scene name (with spaces/case/etc.) to an actual build scene name.
    /// Example: "New Sirocco" -> "NewSirocco" if that's what exists in build settings.
    /// </summary>
    public static class SceneNameResolver
    {
        private static List<string> _buildSceneNames;
        private static Dictionary<string, string> _normalizedToReal;

        public static void Warmup()
        {
            if (_buildSceneNames != null && _normalizedToReal != null) return;

            _buildSceneNames = new List<string>();
            _normalizedToReal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrWhiteSpace(path)) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name)) continue;

                _buildSceneNames.Add(name);

                string norm = Normalize(name);
                if (!_normalizedToReal.ContainsKey(norm))
                    _normalizedToReal[norm] = name;
            }
        }

        public static string Resolve(string requested)
        {
            Warmup();

            if (string.IsNullOrWhiteSpace(requested))
                return requested;

            // Direct match
            if (_buildSceneNames.Contains(requested))
                return requested;

            // Normalized match
            string normReq = Normalize(requested);
            if (_normalizedToReal.TryGetValue(normReq, out var real))
                return real;

            // Common fixups: remove spaces, underscores, hyphens, punctuation
            string compact = new string(requested.Where(char.IsLetterOrDigit).ToArray());
            if (_normalizedToReal.TryGetValue(compact, out real))
                return real;

            // Last resort: "contains" match (helpful if someone types partial)
            var contains = _buildSceneNames.FirstOrDefault(s =>
                Normalize(s).Contains(normReq, StringComparison.OrdinalIgnoreCase) ||
                normReq.Contains(Normalize(s), StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(contains) ? requested : contains;
        }

        public static bool IsBuildScene(string sceneName)
        {
            Warmup();
            return _buildSceneNames.Contains(sceneName);
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            // Keep only letters/digits, lowercased
            return new string(s.Where(char.IsLetterOrDigit).ToArray());
        }
    }
}
