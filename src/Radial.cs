using System;
using HarmonyLib;
using UnityEngine;

namespace OthDriveSystems
{
    // Adds a "drive mode" entry to the radial menu; tapping it cycles the local vehicle's drive mode.
    internal static class Radial
    {
        static IntPtr itemPtr;

        public static void AddItem(RadialMenu menu)
        {
            menuRef = menu;
            var template = menu.musicMenuItem;
            if (template == null || itemPtr != IntPtr.Zero && menu.radialMenuItemsTransforms != null && Contains(menu, itemPtr)) return;
            Trace.Step("radial: adding item");
            var go = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = "OTHDS_DriveModeItem";
            var item = go.GetComponent<ContextUIRadialMenuItem>();
            var tire = FindIcon("Tire_3");
            if (item.icon != null && tire != null) item.icon.sprite = tire;
            menu.radialMenuItemsTransforms?.Add(item);
            menu.allRadialMenuItemsTransforms?.Add(item);
            itemPtr = item.Pointer;
            Trace.Step("radial: item added");
            Plugin.L.LogInfo($"Radial item added ({menu.radialMenuItemsTransforms?.Count} items)");
        }

        static bool Contains(RadialMenu menu, IntPtr p)
        {
            foreach (var i in menu.radialMenuItemsTransforms) if (i != null && i.Pointer == p) return true;
            return false;
        }

        static Sprite FindIcon(string itemName)
        {
            uint id = 0;
            if (!PlayerInventoryManager.ItemIdLookup(itemName, out id)) return null;
            InventoryItemScriptableObject so = null;
            return PlayerInventoryManager.ItemScriptableObjectLookup(id, out so) ? so.Icon : null;
        }

        static RadialMenu menuRef;
        public static bool MenuOpen
        {
            get { try { return menuRef != null && menuRef.isOpen; } catch { return false; } }
        }

        public static bool IsOurs(ContextUIRadialMenuItem i) => i != null && itemPtr != IntPtr.Zero && i.Pointer == itemPtr;

        public static void Cycle()
        {
            var v = Runner.Local;
            if (v == null) { Plugin.L.LogWarning("Drive mode: no local vehicle"); return; }
            var next = (Mode)(((int)v.Mode + 1) % 5);
            v.SetMode(next);
            var msg = $"{v.System.Name}: {v.System.Label(next)}";
            if (!Plugin.Cfg.DashboardEnabled.Value)
                try { PopupManager.Instance?.ShowPopup(msg); } catch (Exception e) { Plugin.L.LogWarning(e.Message); }
        }
    }

    [HarmonyPatch(typeof(RadialMenu), nameof(RadialMenu.Start))]
    static class RadialStartPatch
    {
        static void Postfix(RadialMenu __instance)
        {
            try { Radial.AddItem(__instance); } catch (Exception e) { Plugin.L.LogError(e); }
        }
    }

    [HarmonyPatch(typeof(RadialMenu), nameof(RadialMenu.RestoreFullRadialMenu))]
    static class RadialRestorePatch
    {
        static void Postfix(RadialMenu __instance)
        {
            try { Radial.AddItem(__instance); } catch (Exception e) { Plugin.L.LogError(e); }
        }
    }

    [HarmonyPatch(typeof(RadialMenu), nameof(RadialMenu.CloseRadialMenu))]
    static class RadialClosePatch
    {
        static void Prefix(RadialMenu __instance, ref bool performSelectedAction)
        {
            try
            {
                if (performSelectedAction && Radial.IsOurs(__instance.currentlySelectedRadialMenuItem))
                {
                    performSelectedAction = false;
                    Radial.Cycle();
                }
            }
            catch (Exception e) { Plugin.L.LogError(e); }
        }
    }
}
