// DynastyCompanionLauncher.cs
using System;
using System.Diagnostics;
using System.IO;
using BepInEx;
using UnityDebug = UnityEngine.Debug;

namespace OutwardDynasty
{
    internal static class DynastyCompanionLauncher
    {
        private static Process _process;
        private static DateTime _lastAttempt = DateTime.MinValue;

        private const string CompanionExeName = "OutwardDynastyCompanion.exe";

        
public static void EnsureRunning(bool isHost)
{
    // Auto-launch disabled by design (per project rules).
    // We only warn if the Companion app isn't already running / reachable.
    if (!IsAlreadyRunning())
    {
        string exePath = FindCompanionExe();
        if (string.IsNullOrEmpty(exePath))
        {
            UnityDebug.LogWarning("[Dynasty] Companion app not found (auto-launch disabled). Expected at: " +
                                  Path.Combine(Paths.PluginPath, "OutwardDynasty", CompanionExeName));
        }
        else
        {
            UnityDebug.LogWarning("[Dynasty] Companion app is not running (auto-launch disabled). Please start: " + exePath);
        }
    }
}

public static void Shutdown        public static void Shutdown()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch { }
        }

        private static bool IsAlreadyRunning()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    return true;

                // Detect external/manual launches too
                string procName = Path.GetFileNameWithoutExtension(CompanionExeName);
                return Process.GetProcessesByName(procName).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string FindCompanionExe()
        {
            // 1) Preferred: BepInEx/plugins/OutwardDynasty/OutwardDynastyCompanion.exe
            string preferred = Path.Combine(Paths.PluginPath, "OutwardDynasty", CompanionExeName);
            if (File.Exists(preferred))
                return preferred;

            // 2) Fallback: game root (same folder as Outward.exe)
            string fallback = Path.Combine(Paths.GameRootPath, CompanionExeName);
            if (File.Exists(fallback))
                return fallback;

            return null;
        }
    }
}
