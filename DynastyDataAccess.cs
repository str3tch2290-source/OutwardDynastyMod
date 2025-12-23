using System;
using System.Reflection;

namespace OutwardDynasty
{
    public static class DynastyDataAccess
    {
        public static DynastySaveData Get()
        {
            DynastySaveData data = TryGetFromCore();
            if (data != null) return data;

            return DynastySaveManager.Load();
        }

        private static DynastySaveData TryGetFromCore()
        {
            DynastyCore core = DynastyCore.Instance;
            if (core == null) return null;

            string[] names = { "MasterData", "DynastyData", "SaveData", "Data" };
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (string name in names)
            {
                var field = core.GetType().GetField(name, flags);
                if (field != null && field.FieldType == typeof(DynastySaveData))
                    return (DynastySaveData)field.GetValue(core);

                var prop = core.GetType().GetProperty(name, flags);
                if (prop != null && prop.PropertyType == typeof(DynastySaveData))
                    return (DynastySaveData)prop.GetValue(core);
            }

            return null;
        }
    }
}
