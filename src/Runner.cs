using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using VehiclePhysics;

namespace OthDriveSystems
{
    // Ticked from a postfix on the game's VehicleInput.Update; il2cpp class injection is unreliable on this Unity build.
    internal static class Runner
    {
        internal static readonly Dictionary<IntPtr, VehicleState> Vehicles = new();
        static float nextScan, nextDebug, nextTankLog;
        static bool refusalLogged;
        static int lastFrame = -1;

        internal static VehicleState Local
        {
            get { foreach (var v in Vehicles.Values) if (v.IsLocal) return v; return null; }
        }

        static bool firstFrame = true;

        public static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (firstFrame) { firstFrame = false; Trace.Step("runner: first update"); }
                if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1f; Scan(); }
                foreach (var v in Vehicles.Values) { Reconcile(v); v.UpdateCtis(Time.deltaTime); }
                UpdateTankInput();
                Dashboard.Update(Local);
                if (Plugin.Cfg.DebugLog.Value && Time.unscaledTime >= nextDebug) { nextDebug = Time.unscaledTime + 10f; DebugReport(); }
            }
            catch (Exception e) { Plugin.L.LogError(e); }
        }

        static void Scan()
        {
            Upgrades.Register();
            var dead = new List<IntPtr>();
            foreach (var kv in Vehicles) if (kv.Value.Vc == null || kv.Value.Vc.WasCollected) dead.Add(kv.Key);
            foreach (var k in dead) Vehicles.Remove(k);

            foreach (var o in UnityEngine.Object.FindObjectsByType(Il2CppType.Of<VPVehicleController>(), FindObjectsSortMode.None))
            {
                var vc = o.Cast<VPVehicleController>();
                if (Vehicles.ContainsKey(vc.Pointer)) continue;
                Trace.Step("scan: new vehicle");
                Vehicles[vc.Pointer] = new VehicleState(vc);
            }
        }

        static void Reconcile(VehicleState v)
        {
            if (v.Custom == null || Upgrades.RearSteer == null) return;
            v.SetRearSteer(v.Custom.HasUpgradeOfTypeMounted(Upgrades.RearSteerType));
            v.TankFitted = v.Custom.HasUpgradeOfTypeMounted(Upgrades.TankTurnType);
        }

        static void UpdateTankInput()
        {
            var v = Local;
            if (v == null) return;
            bool held = false;
            var pad = Gamepad.current;
            if (pad != null)
            {
                var c = pad.TryGetChildControl(Plugin.Cfg.TankTurnGamepadButton.Value)?.TryCast<ButtonControl>();
                held |= c != null && c.isPressed;
            }
            var kb = Keyboard.current;
            if (kb != null && Enum.TryParse<Key>(Plugin.Cfg.TankTurnKey.Value, out var key))
                held |= kb[key].isPressed;

            bool winching = false;
            try { winching = v.Winch != null && v.Winch.WinchIsAttached; } catch { }
            float speed = v.Speed;
            bool kinematic = v.Body != null && v.Body.isKinematic;   // garage / frozen vehicle
            bool active = v.TankFitted && held && !winching && !kinematic && IsDriving() && Mathf.Abs(speed) < Plugin.Cfg.TankTurnMaxSpeed.Value;
            if (v.Input != null) v.TankSteer = v.Input.prevFrameSteering;
            if (active && Plugin.Cfg.DebugLog.Value && Time.unscaledTime >= nextTankLog)
            {
                nextTankLog = Time.unscaledTime + 0.5f;
                Plugin.L.LogInfo($"[{v.CarName}] tank: input steer={v.TankSteer:F2} {v.TankDebug}");
            }
            if (held && !active && !refusalLogged)
            {
                refusalLogged = true;
                string map;
                try { map = $"driving={GameInput.IsMapActive(GameInput.Map.Driving)} ui={GameInput.IsMapActive(GameInput.Map.UI)}"; } catch { map = "?"; }
                Plugin.L.LogInfo($"[{v.CarName}] tank turn refused: fitted={v.TankFitted} rearSteer={v.RearSteerOn} map={map} winching={winching} kinematic={kinematic} radial={Radial.MenuOpen} speed={speed:F2}");
            }
            if (!held) refusalLogged = false;
            if (active != v.TankActive)
            {
                v.TankActive = active;
                Plugin.L.LogInfo($"[{v.CarName}] tank turn {(active ? "engaged" : "released")} (speed {speed:F2})");
            }
        }

        // A is also the menu select button; only treat it as tank turn while the Driving input map is active.
        static bool IsDriving()
        {
            try
            {
                // The UI map can be active alongside Driving during normal play, so only the radial menu is excluded here.
                return GameInput.IsMapActive(GameInput.Map.Driving) && !Radial.MenuOpen;
            }
            catch { return false; }
        }

        static void DebugReport()
        {
            foreach (var v in Vehicles.Values)
            {
                var ws = v.Vc.m_wheelState;
                string grip = ws.Length > 0 ? ws[0].tireForceMult.ToString("F2") : "-";
                Plugin.L.LogInfo($"[{v.CarName}] local={v.IsLocal} hooks/10s={v.TakeHookCalls()} mode={v.Mode} rws={v.RearSteerOn} tank={v.TankFitted}/{v.TankActive} grip0={grip}");
            }
        }
    }
}
