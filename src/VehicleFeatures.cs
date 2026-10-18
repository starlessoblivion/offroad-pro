using System;
using System.Collections.Generic;
using UnityEngine;
using VehiclePhysics;

namespace OthDriveSystems
{
    // Per-vehicle state: which mod features are active and what was changed, so everything can be put back.
    internal sealed class VehicleState
    {
        public readonly VPVehicleController Vc;
        public readonly VehicleCustomizationController Custom;
        public readonly VehicleInput Input;
        public readonly VehicleWinchSystem Winch;
        public readonly NetworkedVehicleStatus Status;
        public readonly CarAudioManager Audio;
        public readonly DriveSystem System;
        public readonly string CarName;

        public bool RearSteerOn, TankFitted, TankActive;
        public Mode Mode = Mode.Trail;
        public float TankSteer;
        public string TankDebug;

        Rigidbody body;
        public Rigidbody Body
        {
            get
            {
                if (body == null) body = Vc.m_rigidbody ?? Vc.cachedRigidbody ?? Vc.GetComponentInParent<Rigidbody>();
                return body;
            }
        }

        readonly List<(Steering.WheelData wd, VehicleBase.WheelState ws, bool steerable)> rearSteerAdded = new();
        bool ackermanOriginal;
        readonly List<(WheelTransformUpdater w, bool solid)> rearVisualOriginal = new();
        float[] gripBase, gripSet;
        int hookCalls;

        public VehicleState(VPVehicleController vc)
        {
            Vc = vc;
            Custom = vc.GetComponentInParent<VehicleCustomizationController>() ?? vc.transform.root.GetComponentInChildren<VehicleCustomizationController>();
            Input = vc.transform.root.GetComponentInChildren<VehicleInput>();
            Winch = vc.transform.root.GetComponentInChildren<VehicleWinchSystem>();
            Status = vc.transform.root.GetComponentInChildren<NetworkedVehicleStatus>();
            Audio = vc.transform.root.GetComponentInChildren<CarAudioManager>();
            CarName = Custom?.CurrentCarInfo?.name;
            System = DriveModes.For(CarName);
            Trace.Step($"vehicle {CarName}");
            Plugin.L.LogInfo($"Tracking vehicle {CarName} ({System.Name})");
        }

        // m_speed is a Fusion networked property: it throws until the vehicle is spawned (garage dummies, despawning cars).
        public float Speed
        {
            get { try { return Vc.m_speed; } catch { return 0f; } }
        }

        public bool IsLowRange
        {
            get { try { return Status != null && Status.GearboxIsLowTransferCase; } catch { return false; } }
        }

        public bool IsLocal
        {
            get { try { return Input != null && Input.HasInputAuthority; } catch { return false; } }
        }

        // ---------- per-step physics ----------

        public void BeforeBlocks()
        {
            try
            {
                hookCalls++;
                var data = Vc.data;
                var phys = DriveModes.Physics[(int)Mode];

                if (TankActive)
                {
                    // Fronts stay straight; wheels must be free to counter-rotate, so no brakes and no park lock.
                    data.Set(Channel.Input, InputData.Steer, 0);
                    data.Set(Channel.Input, InputData.Brake, 0);
                    data.Set(Channel.Input, InputData.Handbrake, 0);
                    // Open axle diffs let the wheels counter-rotate in any gear; only an automatic's park lock holds them.
                    if (data.Get(Channel.Input, InputData.AutomaticGear) == 1)    // P
                        data.Set(Channel.Input, InputData.AutomaticGear, 3);      // N
                    data.Set(Channel.Input, InputData.Clutch, 10000);              // clutch in so a manual can't stall
                }
                if (phys.Throttle < 1f)
                {
                    int t = data.Get(Channel.Input, InputData.Throttle);
                    data.Set(Channel.Input, InputData.Throttle, (int)(t * phys.Throttle));
                }

                var dl = Vc.m_driveline;
                if (dl != null)
                {
                    var axles = TankActive ? DiffLock.Open : phys.Axles;
                    var center = TankActive ? DiffLock.Stock : phys.Center;
                    if (axles != DiffLock.Stock || center != DiffLock.Stock || overridesTouched)
                    {
                        dl.frontDifferentialOverride = ToOverride(axles);
                        dl.rearDifferentialOverride = ToOverride(axles);
                        dl.drivelineOverride = ToOverride(center);
                        overridesTouched = axles != DiffLock.Stock || center != DiffLock.Stock;
                    }
                }
            }
            catch (Exception e) { LogOnce(e); }
        }

        bool overridesTouched;

        float[] tankOmega = new float[8];

        // Wheel visuals read WheelState.angularVelocity, which VPP refreshes in DoUpdateData; re-apply the turn spin after it.
        public void AfterData()
        {
            if (!TankActive) return;
            try
            {
                var states = Vc.m_wheelState;
                for (int i = 0; i < states.Length && i < tankOmega.Length; i++) states[i].angularVelocity = tankOmega[i];
            }
            catch (Exception e) { LogOnce(e); }
        }

        public void AfterBlocks()
        {
            try
            {
                if (!TankActive) return;
                if (tankOmega.Length < Vc.m_wheels.Length) tankOmega = new float[Vc.m_wheels.Length];
                float raw = Mathf.Clamp(TankSteer, -1f, 1f);
                // Input curve < 1 makes small stick movements bite hard (touchy, like a G-TURN).
                float steer = Mathf.Sign(raw) * Mathf.Pow(Mathf.Abs(raw), Plugin.Cfg.TankTurnInputCurve.Value);
                // Drive the body toward a target yaw rate; tire friction alone can't rotate a heavy truck in place.
                float yawRate = steer * Plugin.Cfg.TankTurnYawRate.Value * Mathf.Deg2Rad;
                var rb = Body;
                if (rb != null)
                {
                    var up = Vc.transform.up;
                    float current = Vector3.Dot(rb.angularVelocity, up);
                    float accel = Mathf.Clamp((yawRate - current) * Plugin.Cfg.TankTurnResponse.Value, -150f, 150f);
                    rb.AddTorque(up * accel, ForceMode.Acceleration);
                    TankDebug = $"steer={steer:F2} targetYaw={yawRate * Mathf.Rad2Deg:F0}deg/s yaw={current * Mathf.Rad2Deg:F0}deg/s accel={accel:F1} kinematic={rb.isKinematic}";
                }
                else TankDebug = $"steer={steer:F2} NO RIGIDBODY";
                // Spin each side in opposite directions to match the turn; open axle diffs let the wheels counter-rotate.
                var wheels = Vc.m_wheels;
                var states = Vc.m_wheelState;
                for (int i = 0; i < wheels.Length; i++)
                {
                    var w = wheels[i];
                    var col = states[i].wheelCol;
                    if (w == null || col == null) continue;
                    float x = Vc.transform.InverseTransformPoint(col.transform.position).x;
                    // Spin faster than the turn needs so the tires break loose: wheelspin drives the game's dirt, spray and scrub effects.
                    float omega = -yawRate * x / Mathf.Max(0.1f, w.radius) * Plugin.Cfg.TankTurnWheelSpin.Value;
                    tankOmega[i] = omega;
                    w.ResetBrakeTorque();   // tire surface speed = yaw rate x distance from centre line
                    w.L = Mathf.Max(0.05f, col.wheelInertia) * omega;
                }
            }
            catch (Exception e) { LogOnce(e); }
        }

        public void ApplyGrip()
        {
            try
            {
                float grip = PressureGrip * (TankActive ? Plugin.Cfg.TankTurnGrip.Value : 1f);
                var states = Vc.m_wheelState;
                if (gripBase == null || gripBase.Length != states.Length) { gripBase = new float[states.Length]; gripSet = new float[states.Length]; for (int i = 0; i < states.Length; i++) gripSet[i] = float.NaN; }
                for (int i = 0; i < states.Length; i++)
                {
                    var ws = states[i];
                    float cur = ws.tireForceMult;
                    // If nobody changed the value since we last wrote it, keep our recorded base; otherwise the game set a new base.
                    if (cur != gripSet[i]) gripBase[i] = cur;
                    float v = gripBase[i] * grip;
                    ws.tireForceMult = v;
                    gripSet[i] = v;
                }
            }
            catch (Exception e) { LogOnce(e); }
        }

        static Driveline.Override ToOverride(DiffLock d) => d switch
        {
            DiffLock.Locked => Driveline.Override.ForceLocked,
            DiffLock.Open => Driveline.Override.ForceUnlocked,
            _ => Driveline.Override.None,
        };

        bool loggedError;
        void LogOnce(Exception e)
        {
            if (loggedError) return;
            loggedError = true;
            Plugin.L.LogError($"[{CarName}] {e}");
        }

        public int TakeHookCalls() { int n = hookCalls; hookCalls = 0; return n; }

        // ---------- rear-wheel steering ----------

        public void SetRearSteer(bool on)
        {
            if (on == RearSteerOn) return;
            var steering = Vc.m_steering;
            if (steering == null) return;
            if (on)
            {
                // The steering block only holds wheels on steerable axles, so the rear wheels are added to it here.
                // Ackermann is switched off: every car shares one reference point (z -1.088) that sits almost on some rear
                // axles, which makes VPP compute wild rear angles. Without it a Ratio wheel is simply ratio x front angle.
                rearSteerAdded.Clear();
                ackermanOriginal = steering.settings.ackerman;
                steering.settings.ackerman = false;
                var states = Vc.m_wheelState;
                var local = new Vector3[states.Length];
                float meanZ = 0f;
                for (int i = 0; i < states.Length; i++)
                {
                    local[i] = Vc.transform.InverseTransformPoint(states[i].wheelCol.transform.position);
                    meanZ += local[i].z;
                }
                meanZ /= Mathf.Max(1, states.Length);
                var listed = new HashSet<IntPtr>();
                foreach (var wd in steering.m_wheels) listed.Add(wd.wheelState.Pointer);
                for (int i = 0; i < states.Length; i++)
                {
                    var ws = states[i];
                    if (local[i].z >= meanZ - 0.1f || listed.Contains(ws.Pointer)) continue;
                    bool steerable = ws.steerable;
                    steering.AddWheel(ws, local[i], Steering.SteeringMode.Ratio, Plugin.Cfg.RearSteerRatio.Value);
                    var wheels = steering.m_wheels;
                    rearSteerAdded.Add((wheels[wheels.Count - 1], ws, steerable));
                    ws.steerable = true;
                }
                rearVisualOriginal.Clear();
                foreach (var u in Vc.transform.root.GetComponentsInChildren<WheelTransformUpdater>(true))
                {
                    if (u.m_isFrontWheel) continue;
                    rearVisualOriginal.Add((u, u.simulateSolidAxleVisual));
                    u.simulateSolidAxleVisual = false;
                }
                var zs = new System.Text.StringBuilder();
                foreach (var wd in steering.m_wheels) zs.Append($"{wd.position.z:F2}/{wd.mode}/{wd.ratio:F2} ");
                Plugin.L.LogInfo($"[{CarName}] rear steering ON ({rearSteerAdded.Count} wheels added, {rearVisualOriginal.Count} visuals; steering wheels z/mode/ratio: {zs})");
            }
            else
            {
                foreach (var (wd, ws, steerable) in rearSteerAdded)
                {
                    steering.m_wheels.Remove(wd);
                    ws.steeringAngle = 0f;
                    ws.steerable = steerable;
                }
                foreach (var (w, solid) in rearVisualOriginal) if (w != null) w.simulateSolidAxleVisual = solid;
                steering.settings.ackerman = ackermanOriginal;
                rearSteerAdded.Clear();
                rearVisualOriginal.Clear();
                Plugin.L.LogInfo($"[{CarName}] rear steering OFF");
            }
            RearSteerOn = on;
        }

        // ---------- CTIS: live tire pressure ----------

        public float Psi = -1f;                 // current pressure; -1 until first update

        // 0 at stock pressure, 1 at the lowest configured pressure (Sand).
        public float Deflation
        {
            get
            {
                float stock = Plugin.Cfg.CtisTrailPsi.Value, min = Plugin.Cfg.CtisSandPsi.Value;
                return stock > min ? Mathf.Clamp01((stock - Psi) / (stock - min)) : 0f;
            }
        }

        public float PressureGrip => 1f + Plugin.Cfg.CtisMaxGripGain.Value * (Psi < 0 ? 0f : Deflation);

        public float TargetPsi()
        {
            var c = Plugin.Cfg;
            float t = Mode switch
            {
                Mode.Mud => c.CtisMudPsi.Value,
                Mode.Sand => c.CtisSandPsi.Value,
                Mode.Snow => c.CtisSnowPsi.Value,
                _ => c.CtisTrailPsi.Value,
            };
            // Like real CTIS: aired-down tires are pumped back up when driving fast.
            if (Mathf.Abs(Speed) * 3.6f > c.CtisSafeSpeedKmh.Value) t = Mathf.Max(t, c.CtisSafePsi.Value);
            return t;
        }

        public void UpdateCtis(float dt)
        {
            if (Plugin.Cfg.CtisEnabled.Value)
            {
                float target = TargetPsi();
                if (Psi < 0f) Psi = target;
                float rate = target < Psi ? Plugin.Cfg.CtisDeflateRate.Value : Plugin.Cfg.CtisInflateRate.Value;
                Psi = Mathf.MoveTowards(Psi, target, rate * dt);
                UpdateHiss(Mathf.Abs(Psi - target) > 0.05f, dt);
            }
            float liftTarget = ModeLift(Mode);
            UpdateLiftSound(!Mathf.Approximately(liftNow, liftTarget));
            liftNow = Mathf.MoveTowards(liftNow, liftTarget, Plugin.Cfg.LiftRate.Value * dt);
            ApplyWheels();
            ApplyCog();
            string state = $"{Mode} {Mathf.RoundToInt(Psi)}psi lift={liftNow:F2}";
            if (wheels.Count > 0 && Mathf.Approximately(liftNow, ModeLift(Mode)) && Mathf.Abs(Psi - TargetPsi()) < 0.01f && state != lastHeightLog)
            {
                lastHeightLog = state;
                var w0 = wheels[0];
                Plugin.L.LogInfo($"[{CarName}] settled: {state} wheel0 travel {w0.BaseTravel:F3}->{w0.C.Travel:F3} radius {w0.BaseRadius:F3}->{w0.C.Radius:F3}");
            }
        }

        // Mode height offset in metres of suspension travel, applied on top of the game's (upgraded) travel.
        static float ModeLift(Mode m) => m switch
        {
            Mode.Mud => Plugin.Cfg.LiftMud.Value,
            Mode.Sand => Plugin.Cfg.LiftSand.Value,
            Mode.Snow => Plugin.Cfg.LiftSnow.Value,
            Mode.Ford => Plugin.Cfg.LiftFord.Value,
            _ => Plugin.Cfg.LiftTrail.Value,
        };

        float liftNow;
        bool liftSound;
        Vector3 cogBase, cogSet;
        bool cogKnown;

        // The game's winch motor loop while the ride height is moving.
        void UpdateLiftSound(bool moving)
        {
            if (moving == liftSound || !Plugin.Cfg.CtisSounds.Value) return;
            liftSound = moving;
            var em = Audio != null ? Audio.winchLoopAudioEmitter : null;
            if (em == null) return;
            try
            {
                if (moving) { try { em.SetParameter(Audio.winchPullID, 1f, true); } catch { } em.Play(false, null); }
                else em.Stop(true);
            }
            catch (Exception e) { LogOnce(e); }
        }

        // Extra centre-of-gravity height on top of the body lift (the body lift itself already raises it),
        // stacked on whatever the game has set, like the wheel offsets.
        void ApplyCog()
        {
            var rb = Body;
            if (rb == null) return;
            try
            {
                var cur = rb.centerOfMass;
                if (!cogKnown || (cur - cogSet).sqrMagnitude > 1e-8f) { cogBase = cur; cogKnown = true; }
                var want = cogBase + Vector3.up * (liftNow * Plugin.Cfg.CogLiftFactor.Value);
                if ((want - cur).sqrMagnitude > 1e-8f) rb.centerOfMass = want;
                cogSet = want;
            }
            catch (Exception e) { LogOnce(e); }
        }
        string lastHeightLog;

        // Per wheel: the game's current base value and the value we last wrote. If the game changes the value
        // (suspension lift or tire upgrade fitted, vehicle respawned) it becomes the new base and our offsets stack on it.
        sealed class WheelTrack
        {
            public WheelAdapter C;
            public Transform Visual;
            public Vector3 VisualScale;
            public float BaseTravel, SetTravel = float.NaN, BaseRadius, SetRadius = float.NaN;
        }
        readonly List<WheelTrack> wheels = new();

        void ApplyWheels()
        {
            if (wheels.Count == 0)
            {
                var states = Vc.m_wheelState;
                var kinds = new System.Text.StringBuilder();
                for (int i = 0; i < states.Length; i++)
                {
                    var c = WheelAdapter.From(states[i].wheelCol);
                    if (c == null) continue;
                    var vis = c.Visual;
                    wheels.Add(new WheelTrack
                    {
                        C = c, Visual = vis,
                        VisualScale = vis != null ? vis.localScale : Vector3.one,
                        BaseTravel = c.Travel, BaseRadius = c.Radius,
                    });
                    kinds.Append($"{c.Kind} r={c.Radius:F3} travel={c.Travel:F3}; ");
                }
                Plugin.L.LogInfo($"[{CarName}] wheels tracked: {wheels.Count} ({kinds})");
                if (wheels.Count == 0) return;
            }
            float d = Plugin.Cfg.CtisEnabled.Value && Psi >= 0f ? Deflation : 0f;
            float radiusScale = 1f - Plugin.Cfg.CtisMaxRadiusDrop.Value * d;
            float bulge = 1f + Plugin.Cfg.CtisMaxBulge.Value * d;
            foreach (var w in wheels)
            {
                var c = w.C;
                if (c == null || !c.Alive) continue;
                bool refresh = false;

                float travel = c.Travel;
                if (float.IsNaN(w.SetTravel) || Mathf.Abs(travel - w.SetTravel) > 1e-4f) w.BaseTravel = travel;
                float wantTravel = Mathf.Max(0.05f, w.BaseTravel + liftNow);
                if (Mathf.Abs(wantTravel - travel) > 1e-4f) { c.Travel = wantTravel; refresh = true; }
                w.SetTravel = wantTravel;

                float radius = c.Radius;
                if (float.IsNaN(w.SetRadius) || Mathf.Abs(radius - w.SetRadius) > 1e-4f) w.BaseRadius = radius;
                float wantRadius = w.BaseRadius * radiusScale;
                if (Mathf.Abs(wantRadius - radius) > 1e-4f) c.SetRadius(wantRadius);   // physics wheel shrinks; full-size tire model meets the ground flattened
                w.SetRadius = c.Radius;

                if (refresh) c.Refresh();
                if (w.Visual != null) w.Visual.localScale = new Vector3(w.VisualScale.x * bulge, w.VisualScale.y, w.VisualScale.z);   // sidewall bulge
            }
        }

        // ---------- drive modes ----------

        bool hissing;

        // The game's snorkel hiss, played once each time a pressure change starts.
        void UpdateHiss(bool changing, float dt)
        {
            if (!changing) { hissing = false; return; }
            if (hissing || !Plugin.Cfg.CtisSounds.Value) return;
            hissing = true;
            var em = Audio != null ? Audio.snorkelHissAudioEmitter : null;
            if (em == null) return;
            try
            {
                try { em.SetParameter(Audio.snorkelEquipID, 1f, true); } catch { }
                em.Play(false, null);
            }
            catch (Exception e) { LogOnce(e); }
        }

        // Same lever clunk the game plays for the High/Low transfer case.
        void PlayModeClunk()
        {
            if (!Plugin.Cfg.CtisSounds.Value) return;
            try { Audio?.commandTracAudioEmitter?.Play(false, null); } catch (Exception e) { LogOnce(e); }
        }

        public void SetMode(Mode m)
        {
            Mode = m;   // pressure and ride height follow continuously in UpdateCtis
            PlayModeClunk();
            Plugin.L.LogInfo($"[{CarName}] drive mode {m}");
        }
    }
}
