using System;
using System.IO;
using UnityEngine;

namespace OutwardDynasty
{
    public static class DynastySaveManager
    {
        // ======================================================
        // PATHS
        // ======================================================

        // Folder: BepInEx/plugins/OutwardDynasty
        private static readonly string FolderPath =
            Path.Combine(BepInEx.Paths.PluginPath, "OutwardDynasty");

        // Staging file (only before any character exists)
        private static readonly string NewRunSaveFilePath =
            Path.Combine(FolderPath, "DynastySave_New.json");

        // Legacy single-file save
        private static readonly string LegacySaveFilePath =
            Path.Combine(FolderPath, "DynastySave.json");

        // ======================================================
        // STATE
        // ======================================================

        private static string _cachedLocalCharacterID;
        private static bool _hasBoundStagingToCharacter;

        // ======================================================
        // PUBLIC API
        // ======================================================

        public static DynastySaveData Load()
        {
            try
            {
                EnsureFolder();

                string charId = TryGetLocalCharacterIDAndCache();

                // Per-character save
                if (!string.IsNullOrEmpty(charId))
                {
                    string charPath = GetCharacterSavePath(charId);

                    if (File.Exists(charPath))
                        return ReadJson(charPath);

                    if (File.Exists(NewRunSaveFilePath))
                        return ReadJson(NewRunSaveFilePath);

                    if (File.Exists(LegacySaveFilePath))
                        return ReadJson(LegacySaveFilePath);

                    return new DynastySaveData();
                }

                // No character yet
                if (File.Exists(NewRunSaveFilePath))
                    return ReadJson(NewRunSaveFilePath);

                if (File.Exists(LegacySaveFilePath))
                    return ReadJson(LegacySaveFilePath);

                return new DynastySaveData();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] Load failed, wiping dynasty saves\n" + ex);
                DeleteAllDynastySaves();

                return new DynastySaveData
                {
                    DynastyEnabled = false,
                    DynastyStarted = false,
                    PlayerPlaced = false
                };
            }
        }

        public static void Save(DynastySaveData data)
        {
            if (data == null) return;

            try
            {
                EnsureFolder();
                AutoBindStagingIfPossible(data);

                string charId = TryGetLocalCharacterIDAndCache();
                if (string.IsNullOrEmpty(charId))
                    charId = _cachedLocalCharacterID;

                if (!string.IsNullOrEmpty(charId))
                {
                    WriteJson(GetCharacterSavePath(charId), data);
                    SafeDelete(NewRunSaveFilePath);
                    SafeDelete(LegacySaveFilePath);
                    return;
                }

                // No character → only allow staging if dynasty not started
                if (!data.DynastyStarted)
                    WriteJson(NewRunSaveFilePath, data);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] Save failed\n" + ex);
            }
        }

        public static void DeleteCurrentCharacterSave()
        {
            EnsureFolder();

            string charId = TryGetLocalCharacterIDAndCache();
            if (string.IsNullOrEmpty(charId))
                charId = _cachedLocalCharacterID;

            if (!string.IsNullOrEmpty(charId))
            {
                SafeDelete(GetCharacterSavePath(charId));
                return;
            }

            SafeDelete(NewRunSaveFilePath);
        }

        public static void DeleteAllDynastySaves()
        {
            EnsureFolder();

            SafeDelete(NewRunSaveFilePath);
            SafeDelete(LegacySaveFilePath);

            foreach (var file in Directory.GetFiles(FolderPath, "DynastySave_*.json"))
            {
                SafeDelete(file);
            }

            _cachedLocalCharacterID = null;
            _hasBoundStagingToCharacter = false;
        }

        public static void ForceCacheCharacterID(string uid)
        {
            if (!string.IsNullOrEmpty(uid))
                _cachedLocalCharacterID = uid;
        }

        // ======================================================
        // INTERNAL
        // ======================================================

        private static void AutoBindStagingIfPossible(DynastySaveData data)
        {
            if (_hasBoundStagingToCharacter)
                return;

            if (!File.Exists(NewRunSaveFilePath) && !File.Exists(LegacySaveFilePath))
                return;

            string charId = TryGetLocalCharacterIDAndCache();
            if (string.IsNullOrEmpty(charId))
                return;

            WriteJson(GetCharacterSavePath(charId), data);
            SafeDelete(NewRunSaveFilePath);
            SafeDelete(LegacySaveFilePath);

            _hasBoundStagingToCharacter = true;
        }

        private static DynastySaveData ReadJson(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<DynastySaveData>(json);
                return data ?? new DynastySaveData();
            }
            catch
            {
                return new DynastySaveData();
            }
        }

        private static void WriteJson(string path, DynastySaveData data)
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
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
            catch { }
        }

        private static string GetCharacterSavePath(string charId)
        {
            return Path.Combine(FolderPath, $"DynastySave_{SanitizeFileToken(charId)}.json");
        }

        private static string SanitizeFileToken(string token)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                token = token.Replace(c.ToString(), "");

            return string.IsNullOrEmpty(token) ? "UNKNOWN" : token;
        }

        private static string TryGetLocalCharacterIDAndCache()
        {
            try
            {
                var cm = CharacterManager.Instance;
                if (cm == null) return null;

                var c = cm.GetFirstLocalCharacter();
                if (c == null) return null;

                _cachedLocalCharacterID = c.UID.ToString();
                return _cachedLocalCharacterID;
            }
            catch
            {
                return null;
            }
        }
    }
}
