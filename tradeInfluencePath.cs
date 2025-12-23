// ======================================================
// ✅ TradeInfluencePatch.cs
// (FULL REWRITE - SAFE HARMONY PATCH, WON’T CRASH IF METHOD CHANGED/MISSING)
// Fixes startup crash:
// - "Undefined target method for patch method ... TradeInfluencePatch::Postfix(...)"
// - "Could not find method for type ShopMenu and name OnBuyItem ..."
// ======================================================

using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Adds Dynasty rewards (Influence/Bonds/etc) when the player buys items.
    /// SAFE PATCH: will only apply if a matching ShopMenu method exists in the current game version.
    /// </summary>
    [HarmonyPatch]
    public static class TradeInfluencePatch
    {
        // ---------- CONFIG ----------
        private const int InfluencePerPurchase = 1; // change later if you want scaling
        private const int MinQuantityToCount = 1;

        /// <summary>
        /// Harmony calls Prepare BEFORE patching.
        /// If this returns false, Harmony SKIPS patching this class (no crash).
        /// </summary>
        private static bool Prepare()
        {
            var m = TargetMethod();
            if (m == null)
            {
                Debug.LogWarning("[Dynasty] TradeInfluencePatch skipped: could not find ShopMenu buy method for this game version.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// We resolve the target method dynamically so it works across versions.
        /// Your log shows OnBuyItem doesn't exist in your build.
        /// We'll try a small list of common method names and select the best match.
        /// </summary>
        private static MethodBase TargetMethod()
        {
            // ShopMenu is a game type (Assembly-CSharp). It should exist at runtime.
            Type shopMenuType = AccessTools.TypeByName("ShopMenu");
            if (shopMenuType == null)
                return null;

            // Try likely method names across Outward builds/mod variants
            string[] candidates =
            {
                "OnBuyItem",
                "BuyItem",
                "TryBuyItem",
                "DoBuy",
                "OnPurchase",
                "PurchaseItem"
            };

            foreach (string name in candidates)
            {
                // Any overload is fine because we’ll read args generically in Postfix
                var methods = shopMenuType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == name)
                    .ToArray();

                if (methods.Length == 0)
                    continue;

                // Prefer a method that has at least 1 parameter (likely includes Item/qty)
                var best = methods
                    .OrderByDescending(m => m.GetParameters().Length)
                    .FirstOrDefault();

                if (best != null)
                {
                    Debug.Log($"[Dynasty] TradeInfluencePatch targeting: {shopMenuType.FullName}.{best.Name}({best.GetParameters().Length} params)");
                    return best;
                }
            }

            return null;
        }

        /// <summary>
        /// Postfix runs AFTER purchase.
        /// We do NOT hard-require exact parameters (avoids signature mismatch issues).
        /// We scan __args for an Item and an int quantity if present.
        /// </summary>
        private static void Postfix(object __instance, object[] __args)
        {
            try
            {
                DynastyCore core = DynastyCore.Instance;
                if (core == null || core.MasterData == null) return;
                if (!core.IsDynastyModeEnabled) return;
                if (!core.MasterData.DynastyStarted) return;
                if (core.MasterData.PermadeathBanned) return;

                // Find an Item argument if it exists
                Item boughtItem = null;
                int quantity = 1;

                if (__args != null)
                {
                    foreach (object a in __args)
                    {
                        if (a == null) continue;

                        if (boughtItem == null && a is Item it)
                            boughtItem = it;

                        // first int we see becomes quantity (common pattern)
                        if (a is int i)
                            quantity = i;
                    }
                }

                if (quantity < MinQuantityToCount) return;

                // Reward influence (you can change to Bonds or both later)
                core.MasterData.Influence += InfluencePerPurchase;

                // Optional: tiny debug
                // Debug.Log($"[Dynasty] Purchase detected -> +{InfluencePerPurchase} Influence (qty={quantity}, item={(boughtItem != null ? boughtItem.Name : "unknown")})");

                core.SaveDynasty();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] TradeInfluencePatch error (non-fatal): " + ex);
            }
        }
    }
}
