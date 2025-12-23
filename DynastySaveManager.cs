// ======================================================
// FILE: DynastySaveManager.cs
// FULL REWRITE
//
// GOALS:
// 1) Per-character dynasty saves:
//      OutwardDynasty/DynastySave_<CharacterUID>.json
// 2) Staging save used ONLY before any character exists:
//      OutwardDynasty/DynastySave_New.json
// 3) Auto-bind staging -> per-character as soon as a local character exists,
//    so the staging file never acts like a "global ban".
// 4) Cache last known UID to survive scene transitions/death timing.
// ======================================================

using System;
using System.IO;
using UnityEngine;

namespace OutwardDynasty
{
    public static class DynastySaveManager
    {
        // Folder: BepInEx/plugins/OutwardDynasty
        private static readonly string FolderPath =
            Path.Combine(BepInEx.Paths.PluginPath, "OutwardDynasty");

        // Staging file (ONLY for "no character exists yet")
        private static readonly string NewRunSaveFilePath =
            Path.Combine(FolderPath, "DynastySave_New.json");

        // Legacy single-file path (kept for older installs)
        private static readonly string LegacySaveFilePath =
            Path.Combine(FolderPath, "DynastySave.json");

        // Cached UID to avoid "no character ID" edge cases during transitions
        private static string _cachedLocalCharacterID = null;

        // Tracks whether we have already bound staging -> per-character this session
        private static bool _hasBoundStagingToCharacter = false;

        // ======================================================
        // PUBLIC API
        // ======================================================

        /// <summary>
        /// Load dynasty data:
        /// - If character exists and has a per-character file -> load it.
        /// - Else if staging exists -> load staging.
        /// - Else if legacy exists -> load legacy.
        /// - Else -> new DynastySaveData().
        /// </summary>
        public static DynastySaveData Load()
        {
            try
            {
                EnsureFolder();

                // Update cache if possible
                string charId = TryGetLocalCharacterIDAndCache();

                // If we have a character, prefer their own file
                if (!string.IsNullOrEmpty(charId))
                {
                    string charPath = GetCharacterSavePath(charId);

                    if (File.Exists(charPath))
                    {
                        Debug.Log("[Dynasty] Dynasty loaded: " + Path.GetFileName(charPath));
                        return ReadJson(charPath);
                    }

                    // If no per-character file exists, fall back to staging/legacy/new
                    if (File.Exists(NewRunSaveFilePath))
                    {
                        Debug.Log("[Dynasty] Dynasty loaded: DynastySave_New.json");
                        return ReadJson(NewRunSaveFilePath);
                    }

                    if (File.Exists(LegacySaveFilePath))
                    {
                        Debug.Log("[Dynasty] Dynasty loaded: DynastySave.json (legacy)");
                        return ReadJson(LegacySaveFilePath);
                    }

                    Debug.Log("[Dynasty] No save found. Creating new dynasty (not started).");
                    return new DynastySaveData();
                }

                // No character yet (boot/menu/DreamWorld):
                if (File.Exists(NewRunSaveFilePath))
                {
                    Debug.Log("[Dynasty] Dynasty loaded: DynastySave_New.json");
                    return ReadJson(NewRunSaveFilePath);
                }

                if (File.Exists(LegacySaveFilePath))
                {
                    Debug.Log("[Dynasty] Dynasty loaded: DynastySave.json (legacy)");
                    return ReadJson(LegacySaveFilePath);
                }

                Debug.Log("[Dynasty] No save found (no character yet). Creating new dynasty (not started).");
                return new DynastySaveData();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] Failed to load dynasty:\n" + ex);
                return new DynastySaveData();
            }
        }

        /// <summary>
        /// Save dynasty data:
        /// - If a character exists (or cached ID exists) -> save to per-character file.
        /// - If no character exists -> only save to staging if DynastyStarted == false.
        ///   (This prevents "global corruption" from permadeath/ended states.)
        /// - ALSO: if we have a character and staging exists, we auto-bind by saving
        ///   per-character and deleting staging/legacy.
        /// </summary>
        public static void Save(DynastySaveData data)
        {
            try
            {
                if (data == null) return;
                EnsureFolder();

                // Keep attempting to bind staging if the character is available now
                AutoBindStagingIfPossible(data);

                string charId = TryGetLocalCharacterIDAndCache();
                if (string.IsNullOrEmpty(charId))
                    charId = _cachedLocalCharacterID;

                if (!string.IsNullOrEmpty(charId))
                {
                    string charPath = GetCharacterSavePath(charId);
                    WriteJson(charPath, data);

                    // If we now have per-character, staging and legacy should not linger
                    SafeDelete(NewRunSaveFilePath);
                    SafeDelete(LegacySaveFilePath);

                    Debug.Log("[Dynasty] Dynasty saved for character: " + charId);
                    return;
                }

                // No character ID (boot/menu):
                // Only allow staging saves when dynasty has NOT started.
                if (!data.DynastyStarted)
                {
                    WriteJson(NewRunSaveFilePath, data);
                    Debug.Log("[Dynasty] Dynasty saved to NewRun staging file (no character yet).");
                }
                else
                {
                    Debug.LogWarning("[Dynasty] Save skipped: DynastyStarted==true but no character ID (avoiding staging corruption).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] Failed to save dynasty:\n" + ex);
            }
        }

        /// <summary>
        /// Deletes ONLY the currently local character's dynasty save (or cached UID).
        /// If no ID exists at all, deletes staging only.
        /// </summary>
        public static void DeleteCurrentCharacterSave()
        {
            EnsureFolder();

            string charId = TryGetLocalCharacterIDAndCache();
            if (string.IsNullOrEmpty(charId))
                charId = _cachedLocalCharacterID;

            if (!string.IsNullOrEmpty(charId))
            {
                string charPath = GetCharacterSavePath(charId);
                SafeDelete(charPath);
                Debug.Log("[Dynasty] Deleted dynasty save for character: " + charId);
                return;
            }

            // No character info, safest fallback: staging only
            SafeDelete(NewRunSaveFilePath);
            Debug.Log("[Dynasty] Deleted NewRun staging dynasty save (no character ID).");
        }

        /// <summary>
        /// Debug/reset: wipes all dynasty-related files.
        /// </summary>
        public static void DeleteAllDynastySaves()
        {
            EnsureFolder();

            SafeDelete(NewRunSaveFilePath);
            SafeDelete(LegacySaveFilePath);

            string[] files = Directory.GetFiles(FolderPath, "DynastySave_*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
                SafeDelete(files[i]);

            _cachedLocalCharacterID = null;
            _hasBoundStagingToCharacter = false;

            Debug.Log("[Dynasty] All dynasty saves deleted.");
        }

        /// <summary>
        /// Allows other systems (like Permadeath) to force-cache an ID right before
        /// saving/deleting during tricky transitions.
        /// </summary>
        public static void ForceCacheCharacterID(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            _cachedLocalCharacterID = uid;
        }

        // ======================================================
        // INTERNAL: AUTO-BIND STAGING
        // ======================================================

        /// <summary>
        /// If staging exists and a character exists now, bind staging -> per-character:
        /// - Write per-character file using current data
        /// - Delete staging and legacy
        /// - Mark bound so we don't spam
        ///
        /// This prevents staging from behaving like a global save once in gameplay.
        /// </summary>
        private static void AutoBindStagingIfPossible(DynastySaveData data)
        {
            if (_hasBoundStagingToCharacter) return;

            // Only bother if staging or legacy exists
            bool hasStaging = File.Exists(NewRunSaveFilePath);
            bool hasLegacy = File.Exists(LegacySaveFilePath);
            if (!hasStaging && !hasLegacy) return;

            string charId = TryGetLocalCharacterIDAndCache();
            if (string.IsNullOrEmpty(charId)) return;

            // Save will write per-character and cleanup files
            string charPath = GetCharacterSavePath(charId);
            WriteJson(charPath, data);

            SafeDelete(NewRunSaveFilePath);
            SafeDelete(LegacySaveFilePath);

            _hasBoundStagingToCharacter = true;

            Debug.Log("[Dynasty] Bound staging/legacy save to character file: " + Path.GetFileName(charPath));
        }

        // ======================================================
        // INTERNAL HELPERS
        // ======================================================

        private static DynastySaveData ReadJson(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                DynastySaveData data = JsonUtility.FromJson<DynastySaveData>(json);

                if (data == null)
                {
                    Debug.LogError("[Dynasty] Save file corrupted: " + path + " | Creating new dynasty.");
                    return new DynastySaveData();
                }

                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] Failed to read save: " + path + "\n" + ex);
                return new DynastySaveData();
            }
        }

        private static void WriteJson(string path, DynastySaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
        }

        private static void EnsureFolder()
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] Failed to delete file: " + path + "\n" + ex);
            }
        }

        private static string GetCharacterSavePath(string charId)
        {
            string safe = SanitizeFileToken(charId);
            return Path.Combine(FolderPath, $"DynastySave_{safe}.json");
        }

        private static string SanitizeFileToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "UNKNOWN";

            foreach (char c in Path.GetInvalidFileNameChars())
                token = token.Replace(c.ToString(), "");

            return token;
        }

        private static string TryGetLocalCharacterIDAndCache()
        {
            try
            {
                Character c = CharacterManager.Instance != null
                    ? CharacterManager.Instance.GetFirstLocalCharacter()
                    : null;

                if (c == null) return null;

                string id = c.UID.ToString();
                if (!string.IsNullOrEmpty(id))
                    _cachedLocalCharacterID = id;

                return id;
            }
            catch
            {
                return null;
            }
        }
    }
}
