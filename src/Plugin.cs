using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace OthDriveSystems
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "local.oth.drivesystems";
        public const string Name = "Offroad Pro";
        public const string Version = "0.1.0";

        internal static ManualLogSource L;
        internal static Settings Cfg;

        public override void Load()
        {
            L = Log;
            Trace.Start();
            Trace.Step("config");
            Cfg = new Settings(Config);
            var harmony = new Harmony(Guid);
            foreach (var t in typeof(Plugin).Assembly.GetTypes())
            {
                if (t.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                if (Cfg.DisabledPatches.Value.Contains(t.Name)) { Trace.Step($"patch {t.Name} skipped (config)"); continue; }
                Trace.Step($"patch {t.Name}");
                harmony.CreateClassProcessor(t).Patch();
            }
            Trace.Step("loaded");
            L.LogInfo($"{Name} {Version} loaded");
        }
    }

    // Crash-safe progress trace: each line is flushed to disk immediately, so the last line shows where a native crash happened.
    internal static class Trace
    {
        static string path;

        public static void Start()
        {
            path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "OthDriveSystems.trace.log");
            System.IO.File.WriteAllText(path, $"{System.DateTime.Now:HH:mm:ss} start\n");
        }

        public static void Step(string s)
        {
            if (path != null) System.IO.File.AppendAllText(path, $"{System.DateTime.Now:HH:mm:ss.fff} {s}\n");
        }
    }

    internal class Settings
    {
        public readonly ConfigEntry<int> RearSteerPrice, TankTurnPrice;
        public readonly ConfigEntry<float> RearSteerRatio;
        public readonly ConfigEntry<int> RearSteerUpgradeType, TankTurnUpgradeType;
        public readonly ConfigEntry<string> TankTurnGamepadButton, TankTurnKey;
        public readonly ConfigEntry<float> TankTurnYawRate, TankTurnResponse, TankTurnGrip, TankTurnMaxSpeed, TankTurnWheelSpin, TankTurnInputCurve;
        public readonly ConfigEntry<string> FordModeName;
        public readonly ConfigEntry<bool> CtisEnabled, CtisSounds;
        public readonly ConfigEntry<string> UnitSystem;
        public readonly ConfigEntry<float> WaypointClearDistance;
        public readonly ConfigEntry<float> CtisTrailPsi, CtisMudPsi, CtisSandPsi, CtisSnowPsi, CtisDeflateRate, CtisInflateRate,
            CtisMaxRadiusDrop, CtisMaxBulge, CtisMaxGripGain, CtisSafeSpeedKmh, CtisSafePsi;
        public readonly ConfigEntry<bool> DashboardEnabled, DashboardDiffMarkers, DashboardHighlightUseSky;
        public readonly ConfigEntry<float> DashboardGraphicScale, DashboardGraphicOffset, DashboardNameOffset, DashboardTileSplit, DashboardTileOuterEnd, DashboardTextInset, DashboardTextSize;
        public readonly ConfigEntry<float> LiftTrail, LiftMud, LiftSand, LiftSnow, LiftFord, LiftRate, CogLiftFactor;
        public readonly ConfigEntry<bool> DebugLog, EnableRunner;
        public readonly ConfigEntry<string> DisabledPatches;

        public Settings(ConfigFile c)
        {
            RearSteerPrice = c.Bind("RearWheelSteering", "Price", 75, "Price in pebbles.");
            RearSteerRatio = c.Bind("RearWheelSteering", "Ratio", -0.35f, "Rear steer angle as a fraction of the front angle. Negative = opposite direction.");
            RearSteerUpgradeType = c.Bind("RearWheelSteering", "UpgradeType", 1, "Upgrade category for the menu entry. Must be an existing UpgradeType value the game does not use itself (1 = Drone, 3 = Health).");
            TankTurnPrice = c.Bind("TankTurn", "Price", 100, "Price in pebbles.");
            TankTurnUpgradeType = c.Bind("TankTurn", "UpgradeType", 3, "Upgrade category for the menu entry. Must be an existing UpgradeType value the game does not use itself (1 = Drone, 3 = Health).");
            TankTurnGamepadButton = c.Bind("TankTurn", "GamepadButton", "buttonSouth", "Hold to tank turn (Unity Input System gamepad control name, e.g. buttonSouth, dpad/up).");
            TankTurnKey = c.Bind("TankTurn", "KeyboardKey", "LeftAlt", "Hold to tank turn on keyboard (Unity Input System Key name).");
            TankTurnYawRate = c.Bind("TankTurn", "YawRate", 90f, "Turning speed in degrees per second at full steering input.");
            TankTurnResponse = c.Bind("TankTurn", "Response", 60f, "How hard the turn controller pushes toward the target turning speed.");
            TankTurnGrip = c.Bind("TankTurn", "TireGrip", 0.5f, "Tire grip multiplier while tank turning (tires scrub sideways during a pivot).");
            TankTurnWheelSpin = c.Bind("TankTurn", "WheelSpin", 3f, "Wheel spin relative to the speed the turn needs (>1 = tires break loose and throw dirt).");
            TankTurnInputCurve = c.Bind("TankTurn", "InputCurve", 0.7f, "Steering response curve (<1 = small inputs already turn hard).");
            TankTurnMaxSpeed = c.Bind("TankTurn", "MaxVehicleSpeed", 1.5f, "Tank turn only engages below this vehicle speed (m/s).");
            DashboardEnabled = c.Bind("Dashboard", "Enabled", true, "Show the drivetrain dial right of the pitch and roll indicators (replaces the mode popup).");
            DashboardDiffMarkers = c.Bind("Dashboard", "DiffLockMarkers", true, "Show front/center/rear diff markers on the dial (red-orange = locked by the drive mode).");
            DashboardGraphicScale = c.Bind("Dashboard", "GraphicScale", 0.45f, "Drivetrain drawing size as a fraction of the dial, used only if the game's vehicle image can't be measured.");
            DashboardGraphicOffset = c.Bind("Dashboard", "GraphicOffsetY", 0f, "Extra vertical shift of the drivetrain drawing in UI pixels (it is placed where the game's vehicle sits in the roll dial).");
            DashboardHighlightUseSky = c.Bind("Dashboard", "HighlightUseSkyGrey", false, "Active mode tile colour: false = the pitch indicator's ground grey, true = its sky grey.");
            DashboardNameOffset = c.Bind("Dashboard", "NameOffsetY", 0f, "Fine vertical shift of the system name in the flat bottom band, UI pixels.");
            DashboardTileSplit = c.Bind("Dashboard", "TileSplitDegrees", 58f, "Angle from 12 o'clock where the Sand/Snow tiles end and the Mud/Ford tiles begin.");
            DashboardTileOuterEnd = c.Bind("Dashboard", "TileOuterEndDegrees", 124f, "How far down the rim the Mud/Ford tiles reach (the flat bottom starts near 135).");
            DashboardTextInset = c.Bind("Dashboard", "TextInset", 12f, "Distance of the mode label baseline inside the rim, in UI pixels.");
            DashboardTextSize = c.Bind("Dashboard", "TextSize", 11f, "Mode label font size (shrinks to fit long names).");
            WaypointClearDistance = c.Bind("Dashboard", "WaypointClearDistance", 6.1f, "Clear your map waypoint when you get within this many metres of it (6.1 m = 20 ft; 0 = never).");
            UnitSystem = c.Bind("Dashboard", "Units", "Auto", "Auto (follow system region), Imperial (psi, mi/ft) or Metric (bar, km/m). The game has no units setting of its own.");
            CtisEnabled = c.Bind("CTIS", "Enabled", true, "Live tire pressure per drive mode: tires deflate/inflate over time, shrink physically and gain grip.");
            CtisSounds = c.Bind("CTIS", "Sounds", true, "Play the game's snorkel hiss once per pressure change and the transfer-case clunk on mode change.");
            CtisTrailPsi = c.Bind("CTIS", "TrailPsi", 32f, "Stock pressure (Trail and Ford modes).");
            CtisMudPsi = c.Bind("CTIS", "MudPsi", 20f, "Mud mode pressure.");
            CtisSandPsi = c.Bind("CTIS", "SandPsi", 15f, "Sand mode pressure (also the lowest pressure; full deflation effect).");
            CtisSnowPsi = c.Bind("CTIS", "SnowPsi", 22f, "Snow mode pressure.");
            CtisDeflateRate = c.Bind("CTIS", "DeflateRate", 2.5f, "Psi per second when airing down.");
            CtisInflateRate = c.Bind("CTIS", "InflateRate", 1.2f, "Psi per second when pumping up (compressor).");
            CtisMaxRadiusDrop = c.Bind("CTIS", "MaxRadiusDrop", 0.08f, "Tire radius reduction at the lowest pressure (fraction).");
            CtisMaxBulge = c.Bind("CTIS", "MaxBulge", 0.08f, "Tire model widening at the lowest pressure (fraction).");
            CtisMaxGripGain = c.Bind("CTIS", "MaxGripGain", 0.15f, "Extra tire grip at the lowest pressure (fraction); replaces the per-mode grip values.");
            CtisSafeSpeedKmh = c.Bind("CTIS", "SafeSpeedKmh", 60f, "Above this speed aired-down tires are pumped back up.");
            CtisSafePsi = c.Bind("CTIS", "SafePsi", 25f, "Minimum pressure above SafeSpeedKmh.");
            FordModeName = c.Bind("DriveModes", "FordModeName", "Ford", "Generic label for the raised water-crossing mode.");
            LiftTrail = c.Bind("DriveModes", "Lift.Trail", 0f, "Ride height offset (m of suspension travel) on top of the vehicle's current, upgraded height.");
            LiftMud = c.Bind("DriveModes", "Lift.Mud", 0f, "Ride height offset for Mud (negative lowers).");
            LiftSand = c.Bind("DriveModes", "Lift.Sand", 0f, "Ride height offset for Sand (negative lowers).");
            LiftSnow = c.Bind("DriveModes", "Lift.Snow", 0f, "Ride height offset for Snow (negative lowers).");
            LiftFord = c.Bind("DriveModes", "Lift.Ford", 0.12f, "Ride height offset for the water-crossing mode.");
            CogLiftFactor = c.Bind("DriveModes", "CogLiftFactor", 2f, "Extra centre-of-gravity rise per metre of mode lift, on top of the body lift. Makes Ford/Wade a top-heavy trade-off instead of a free height bonus (0 = physics only).");
            LiftRate = c.Bind("DriveModes", "LiftRate", 0.05f, "How fast the ride height changes, m/s (air suspension style).");
            DebugLog = c.Bind("Debug", "VerboseLog", false, "Write extra diagnostics to the BepInEx log.");
            EnableRunner = c.Bind("Debug", "EnableRunner", true, "Run the per-frame vehicle logic.");
            DisabledPatches = c.Bind("Debug", "DisabledPatches", "", "Comma-separated patch class names to skip (troubleshooting).");
        }
    }
}
