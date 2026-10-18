using System;
using HarmonyLib;
using VehiclePhysics;

namespace OthDriveSystems
{
    [HarmonyPatch(typeof(VehicleInput), nameof(VehicleInput.Update))]
    static class TickPatch
    {
        static void Postfix()
        {
            if (Plugin.Cfg.EnableRunner.Value) Runner.Tick();
        }
    }

    [HarmonyPatch(typeof(VPVehicleController), nameof(VPVehicleController.DoUpdateBlocks))]
    static class PhysicsStepPatch
    {
        static void Prefix(VPVehicleController __instance)
        {
            if (Runner.Vehicles.TryGetValue(__instance.Pointer, out var v)) v.BeforeBlocks();
        }

        static void Postfix(VPVehicleController __instance)
        {
            if (!Runner.Vehicles.TryGetValue(__instance.Pointer, out var v)) return;
            v.AfterBlocks();
            v.ApplyGrip();
        }
    }

    [HarmonyPatch(typeof(VPVehicleController), nameof(VPVehicleController.DoUpdateData))]
    static class DataStepPatch
    {
        static void Postfix(VPVehicleController __instance)
        {
            if (Runner.Vehicles.TryGetValue(__instance.Pointer, out var v)) v.AfterData();
        }
    }
}
