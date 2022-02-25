using BepInEx;
using BepInEx.Logging;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RogueTower_RefundUpgrades
{
    [BepInPlugin("me.tepis.roguetower.refundupgrades", "Refund Upgrades", "1.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private void Awake()
        {
            Log = Logger;
            var harmony = new Harmony("me.tepis.roguetower.refundupgrades");
            harmony.PatchAll();
            Log.LogInfo("Plugin me.tepis.roguetower.refundupgrades is loaded!");
        }
        public static Dictionary<string, Sprite> lockedSprites = new();
        static AccessTools.FieldRef<UpgradeButton, string> unlockStringRef = AccessTools.FieldRefAccess<UpgradeButton, string>("unlockString");
        static AccessTools.FieldRef<UpgradeButton, Image> imgRef = AccessTools.FieldRefAccess<UpgradeButton, Image>("img");
        static AccessTools.FieldRef<UpgradeButton, bool> countsAsCardUnlockRef = AccessTools.FieldRefAccess<UpgradeButton, bool>("countsAsCardUnlock");
        static AccessTools.FieldRef<UpgradeButton, bool> countAsDevelopmentRef = AccessTools.FieldRefAccess<UpgradeButton, bool>("countAsDevelopment");
        static AccessTools.FieldRef<UpgradeButton, GameObject> currentPriceTagRef = AccessTools.FieldRefAccess<UpgradeButton, GameObject>("currentPriceTag");
        static AccessTools.FieldRef<UpgradeButton, UpgradeButton[]> nextRef = AccessTools.FieldRefAccess<UpgradeButton, UpgradeButton[]>("next");
        static AccessTools.FieldRef<UpgradeManager, List<UpgradeButton>> cardsWhichRequireCardCountRef = AccessTools.FieldRefAccess<UpgradeManager, List<UpgradeButton>>("cardsWhichRequireCardCount");

        private static void RestoreSprite(UpgradeButton upgradeButton)
        {
            if (lockedSprites.TryGetValue(unlockStringRef(upgradeButton), out Sprite sprite))
            {
                imgRef(upgradeButton).sprite = sprite;
            }
        }
        private static void Uncount(UpgradeButton upgradeButton)
        {
            if (countsAsCardUnlockRef(upgradeButton))
            {
                UpgradeManager.instance.unlockedCardCount--;
                PlayerPrefs.SetInt("UnlockedCardCount", UpgradeManager.instance.unlockedCardCount);
            }
            if (countAsDevelopmentRef(upgradeButton))
            {
                int @int = PlayerPrefs.GetInt("Development", 0);
                PlayerPrefs.SetInt("Development", @int - 1);
            }
        }
        public static void RefundUpgrade(UpgradeButton upgradeButton, bool skipCheckingCardsWhichRequireCardCount = false)
        {
            if (!upgradeButton.unlocked)
            {
                return;
            }
            Log.LogInfo($"Refunding {unlockStringRef(upgradeButton)}");
            UpgradeManager.instance.AddXP(upgradeButton.xpCost);
            PlayerPrefs.SetInt(unlockStringRef(upgradeButton), 0);
            upgradeButton.unlocked = false;
            RestoreSprite(upgradeButton);
            Uncount(upgradeButton);
            currentPriceTagRef(upgradeButton)?.SetActive(true);
            upgradeButton.CheckEnabled();
            // Refund child upgrades
            foreach (UpgradeButton next in nextRef(upgradeButton))
            {
                if (next.unlocked)
                {
                    RefundUpgrade(next, true);
                }
                else
                {
                    next.CheckEnabled();
                }
            }
            // Refund cards which require a specific card count and such requirement is no longer fulfilled
            if (!skipCheckingCardsWhichRequireCardCount)
            {
                bool refundedSome;
                do
                {
                    refundedSome = false;
                    foreach (UpgradeButton other in cardsWhichRequireCardCountRef(UpgradeManager.instance))
                    {
                        if (other.cardCountRequirement > UpgradeManager.instance.unlockedCardCount) {
                            if (other.unlocked)
                            {
                                RefundUpgrade(other, true);
                            }
                        }
                        other.CheckEnabled();
                    }
                }
                while (refundedSome);
            }
        }
    }

    [HarmonyPatch(typeof(UpgradeButton))]
    [HarmonyPatch(nameof(UpgradeButton.Start))]
    class UpgradeButton_Start
    {
        static AccessTools.FieldRef<UpgradeButton, string> unlockStringRef = AccessTools.FieldRefAccess<UpgradeButton, string>("unlockString");

        static void Prefix(UpgradeButton __instance)
        {
            // Save the locked sprite
            if (!Plugin.lockedSprites.ContainsKey(unlockStringRef(__instance)))
            {
                Plugin.lockedSprites.Add(unlockStringRef(__instance), __instance.GetComponent<Image>().sprite);
            }
        }
    }

    [HarmonyPatch(typeof(UpgradeButton))]
    [HarmonyPatch(nameof(UpgradeButton.Unlock))]
    class UpgradeButton_Unlock
    {
        static bool Prefix(UpgradeButton __instance)
        {
            if (!__instance.unlocked)
            {
                // Proceed as normal if not unlocked
                return true;
            }
            SFXManager.instance.ButtonClick();
            Plugin.RefundUpgrade(__instance);
            return false;
        }

        static AccessTools.FieldRef<UpgradeButton, Button> btnRef = AccessTools.FieldRefAccess<UpgradeButton, Button>("btn");
        static void Postfix(UpgradeButton __instance)
        {
            if (__instance.unlocked)
            {
                // Allow refunding newly purchased upgrades
                btnRef(__instance).enabled = true;
            }
        }
    }

    [HarmonyPatch(typeof(UpgradeButton))]
    [HarmonyPatch(nameof(UpgradeButton.CheckUnlock))]
    class UpgradeButton_CheckUnlock
    {
        static AccessTools.FieldRef<UpgradeButton, Button> btnRef = AccessTools.FieldRefAccess<UpgradeButton, Button>("btn");
        static void Postfix(UpgradeButton __instance)
        {
            // Don't disable the button for unlocked cards
            btnRef(__instance).enabled = true;
        }
    }
}
