
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public static class DynastyWorldContext
    {
        // Minimal mapping for v1; can be expanded with Outward ID master later.
        public static string GetCurrentTownName()
        {
            string scene = SceneManager.GetActiveScene().name ?? "";
            if (string.IsNullOrEmpty(scene)) return "";

            if (scene.IndexOf("Cierzo", StringComparison.OrdinalIgnoreCase) >= 0) return "Cierzo";
            if (scene.IndexOf("Berg", StringComparison.OrdinalIgnoreCase) >= 0) return "Berg";
            if (scene.IndexOf("Monsoon", StringComparison.OrdinalIgnoreCase) >= 0) return "Monsoon";
            if (scene.IndexOf("Levant", StringComparison.OrdinalIgnoreCase) >= 0) return "Levant";

            // Fallback: treat scene itself as town key
            return scene;
        }

        public static string GetTownOwnerFaction(DynastySaveData data, string townName)
        {
            if (data == null || data.Towns == null) return "NONE";
            for (int i = 0; i < data.Towns.Count; i++)
            {
                var t = data.Towns[i];
                if (t == null) continue;
                if (string.Equals(t.TownName, townName, StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrEmpty(t.OwnerFaction) ? "NONE" : t.OwnerFaction;
            }
            return "NONE";
        }

        public static int GetRenderNpcCap(DynastySaveData data)
        {
            if (data == null) return 12;
            string town = GetCurrentTownName();
            int pop = 0;

            // Prefer faction population stat (your rule), else fall back to town population.
            string owner = GetTownOwnerFaction(data, town);
            if (!string.IsNullOrEmpty(owner) && data.Factions != null)
            {
                for (int i = 0; i < data.Factions.Count; i++)
                {
                    var f = data.Factions[i];
                    if (f == null) continue;
                    if (string.Equals(f.Name, owner, StringComparison.OrdinalIgnoreCase))
                    {
                        pop = f.Population;
                        break;
                    }
                }
            }

            if (pop <= 0 && data.Towns != null)
            {
                for (int i = 0; i < data.Towns.Count; i++)
                {
                    var t = data.Towns[i];
                    if (t == null) continue;
                    if (string.Equals(t.TownName, town, StringComparison.OrdinalIgnoreCase))
                    {
                        pop = t.Population;
                        break;
                    }
                }
            }

            // Population 100..1200 => 1..12, hard cap 12
            int cap = Mathf.Clamp(pop / 100, 1, 12);
            return cap;
        }
    }

    public static class DynastyNameGen
    {
        private static readonly string[] First = new[]
        {
            "Ari","Bren","Cora","Dain","Eira","Fenn","Garr","Hale","Iris","Jory","Kara","Lenn","Mira","Nash","Orin","Perr","Quin","Rhea","Soren","Tali","Ulric","Vera","Wren","Yara","Zev"
        };

        private static readonly string[] Last = new[]
        {
            "Ashford","Briar","Crowe","Dusk","Ember","Frost","Grove","Hollow","Ivory","Jade","Kestrel","Lark","Morrow","North","Oak","Pike","Quarry","Reed","Sable","Thorne","Umber","Vale","Wilder","Yew","Zephyr"
        };

        public static string Generate(System.Random rng)
        {
            if (rng == null) rng = new System.Random();
            return First[rng.Next(First.Length)] + " " + Last[rng.Next(Last.Length)];
        }
    }
}
