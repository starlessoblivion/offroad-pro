using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Localization;
using Il2CppList = Il2CppSystem.Collections.Generic;

namespace OthDriveSystems
{
    // Creates the mod's upgrade items at runtime and registers them like the game's own VehicleUpgrade assets.
    internal static class Upgrades
    {
        public const uint RearSteerId = 3029270423;   // crc32("OTHDS_RearWheelSteering"), checked against the catalog
        public const uint TankTurnId = 561285334;     // crc32("OTHDS_TankTurn")

        public static VehicleUpgrade RearSteer, TankTurn;
        public static UpgradeType RearSteerType => (UpgradeType)Plugin.Cfg.RearSteerUpgradeType.Value;
        public static UpgradeType TankTurnType => (UpgradeType)Plugin.Cfg.TankTurnUpgradeType.Value;

        static readonly Dictionary<IntPtr, string> Texts = new();
        static bool registered;

        public static void Register()
        {
            if (registered) return;
            var catalog = PlayerInventoryManager.InventoryItemsCatalog;
            if (catalog == null) return;
            Trace.Step("register: catalog found");

            var idToSo = RawFields.Get<InventoryItemsCatalog, Il2CppList.Dictionary<uint, InventoryItemScriptableObject>>(catalog, "IdToSO");
            var idToName = RawFields.Get<InventoryItemsCatalog, Il2CppList.Dictionary<uint, string>>(catalog, "IdToName");
            var nameToId = RawFields.Get<InventoryItemsCatalog, Il2CppList.Dictionary<string, uint>>(catalog, "NameToId");
            if (idToSo == null) { Plugin.L.LogError("Item catalog dictionaries not found"); return; }
            Trace.Step($"register: dictionaries read ({idToSo.Count} items)");

            InventoryItemScriptableObject Find(string name)
            {
                foreach (var kv in idToSo) if (kv.Value != null && kv.Value.name == name) return kv.Value;
                return null;
            }

            RearSteer = Create("OTHDS_RearWheelSteering", RearSteerId, RearSteerType, Plugin.Cfg.RearSteerPrice.Value,
                Find("SuspensionLift_stage_3"), "Rear Wheel Steering",
                "Rear wheels turn opposite to the front wheels for a much tighter turning circle. Fit to switch on, remove to switch off.");
            TankTurn = Create("OTHDS_TankTurn", TankTurnId, TankTurnType, Plugin.Cfg.TankTurnPrice.Value,
                Find("Engine_stage_3"), "Tank Turn",
                $"At a standstill, hold {Plugin.Cfg.TankTurnGamepadButton.Value} (pad) or {Plugin.Cfg.TankTurnKey.Value} (keyboard) and steer to spin in place. Fit to switch on, remove to switch off.");

            Trace.Step("register: items created");
            foreach (var up in new[] { RearSteer, TankTurn })
            {
                var so = up.Cast<InventoryItemScriptableObject>();
                idToSo[up.ItemId] = so;
                idToName[up.ItemId] = up.name;
                nameToId[up.name] = up.ItemId;
            }

            int cars = 0;
            foreach (var kv in idToSo)
            {
                var car = kv.Value?.TryCast<CarInfo>();
                if (car == null || car.UpgradesLists == null || car.name.Contains("[CatalogExcluded]")) continue;
                var parts = RawFields.Get<UpgradesLists, Il2CppList.List<VehiclePartSO>>(car.UpgradesLists, "Parts");
                if (parts == null) continue;
                foreach (var up in new[] { RearSteer, TankTurn })
                {
                    var part = up.Cast<VehiclePartSO>();
                    if (!parts.Contains(part)) parts.Add(part);
                }
                cars++;
            }
            registered = true;
            Trace.Step($"register: done, {cars} vehicles");
            Plugin.L.LogInfo($"Registered upgrades on {cars} vehicles (types {(int)RearSteerType}, {(int)TankTurnType})");
        }

        static VehicleUpgrade Create(string name, uint id, UpgradeType type, int price, InventoryItemScriptableObject template, string title, string desc)
        {
            var up = ScriptableObject.CreateInstance(Il2CppType.Of<VehicleUpgrade>()).Cast<VehicleUpgrade>();
            up.name = name;
            up.hideFlags = HideFlags.DontUnloadUnusedAsset;
            up.ItemId = id;
            up.Type = InventoryItemScriptableObject.ItemType.VehicleUpgrade;
            up.Price = price;
            up.UpgradeType = type;
            if (template != null)
            {
                up.Icon = template.Icon;
                up.RequiredDlc = template.RequiredDlc;
            }
            up.Name = NewText(template?.Name, title);
            up.Description = NewText(template?.Description, desc);
            RawFields.SetPtr(up, "Effects", new Il2CppList.List<UpgradeEffect>().Pointer);
            RawFields.SetPtr(up, "LinkedParts", new Il2CppList.List<VehiclePartSO>().Pointer);
            return up;
        }

        static LocalizedString NewText(LocalizedString template, string text)
        {
            var s = new LocalizedString();
            if (template != null) s.TableReference = template.TableReference;
            Texts[s.Pointer] = text;
            return s;
        }

        public static bool TryText(LocalizedString s, out string text)
        {
            text = null;
            return s != null && Texts.TryGetValue(s.Pointer, out text);
        }

        public static string CategoryName(UpgradeType t) =>
            t == RearSteerType ? "Rear Wheel Steering" : t == TankTurnType ? "Tank Turn" : null;
    }

    [HarmonyPatch(typeof(PlayerInventoryManager), nameof(PlayerInventoryManager.Awake))]
    static class RegisterOnInventoryAwake
    {
        static void Postfix()
        {
            try { Upgrades.Register(); } catch (Exception e) { Plugin.L.LogError(e); }
        }
    }

    [HarmonyPatch(typeof(UpgradeTypeExtensions), nameof(UpgradeTypeExtensions.ToLocalizedString))]
    static class CategoryNamePatch
    {
        static void Postfix(UpgradeType type, ref string __result)
        {
            var n = Upgrades.CategoryName(type);
            if (n != null) __result = n;
        }
    }

    [HarmonyPatch(typeof(LocalizedString), nameof(LocalizedString.GetLocalizedString), new Type[0])]
    static class ItemTextPatch
    {
        static void Postfix(LocalizedString __instance, ref string __result)
        {
            if (Upgrades.TryText(__instance, out var t)) __result = t;
        }
    }
}
