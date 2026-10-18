using System.Collections.Generic;

namespace OthDriveSystems
{
    internal enum Mode { Trail = 0, Mud = 1, Sand = 2, Snow = 3, Ford = 4 }

    internal enum DiffLock { Stock, Open, Locked }

    internal sealed class ModePhysics
    {
        public float Grip = 1f;          // multiplies each wheel's tire force (lower pressure = bigger footprint)
        public float Throttle = 1f;      // scales throttle input
        public DiffLock Axles = DiffLock.Stock;
        public DiffLock Center = DiffLock.Stock;
        public bool Raised;              // extra suspension travel for water crossings
    }

    internal sealed class DriveSystem
    {
        public string Name;
        public string[] Labels;          // indexed by Mode; Ford label null = use the generic config name

        public string Short;            // used when Name does not fit the dial

        public DriveSystem(string name, string trail, string mud, string sand, string snow, string ford = null, string shortName = null)
        {
            Name = name;
            Short = shortName ?? name;
            Labels = new[] { trail, mud, sand, snow, ford };
        }

        public string Label(Mode m) => Labels[(int)m] ?? Plugin.Cfg.FordModeName.Value;
    }

    internal static class DriveModes
    {
        public static readonly ModePhysics[] Physics =
        {
            new ModePhysics(),                                                                                  // Trail: stock
            new ModePhysics { Grip = 1.10f, Axles = DiffLock.Locked, Center = DiffLock.Locked },               // Mud
            new ModePhysics { Grip = 1.15f, Center = DiffLock.Locked },                                        // Sand
            new ModePhysics { Grip = 1.05f, Throttle = 0.6f, Axles = DiffLock.Open, Center = DiffLock.Open },  // Snow
            new ModePhysics { Raised = true },                                                                  // Ford
        };

        static readonly DriveSystem Goat = new("G.O.A.T. Modes", "Normal", "Mud/Ruts", "Sand", "Slippery");
        static readonly DriveSystem FordTms = new("Terrain Management", "Normal", "Mud/Ruts", "Sand", "Snow", shortName: "Terrain Mgmt");
        static readonly DriveSystem LandRover = new("Terrain Response", "Comfort", "Mud Ruts", "Sand", "Snow", "Wade", "Terrain Resp.");
        static readonly DriveSystem Toyota = new("Multi-Terrain Select", "Dirt", "Mud", "Sand", "Deep Snow", shortName: "Multi-Terrain");
        static readonly DriveSystem Jeep = new("Selec-Terrain", "Auto", "Mud", "Sand", "Snow");
        static readonly DriveSystem Vw = new("4MOTION Control", "Offroad", "Mud", "Sand", "Snow", shortName: "4MOTION");
        static readonly DriveSystem Mercedes = new("DYNAMIC SELECT", "G-MODE", "Mud", "Sand", "Snow");
        static readonly DriveSystem Unimog = new("tirecontrol plus", "Trail", "Mud", "Sand", "Snow", shortName: "tirecontrol");
        static readonly DriveSystem Gm = new("Drive Modes", "Trail", "Mud", "Sand", "Snow");
        static readonly DriveSystem Generic = new("Terrain Modes", "Trail", "Mud", "Sand", "Snow");

        // Keyed by CarInfo asset name.
        static readonly Dictionary<string, DriveSystem> ByVehicle = new()
        {
            ["Bull"] = Goat,
            ["Roamer"] = FordTms, ["Timberbox"] = FordTms,
            ["Protector"] = LandRover, ["Marshal"] = LandRover, ["Tinny"] = LandRover,
            ["DingDing"] = Toyota, ["Burro"] = Toyota, ["Brick"] = Toyota,
            ["Woodman"] = Jeep, ["GrandWoodman"] = Jeep,
            ["Lynx"] = Vw,
            ["Wombat"] = Mercedes,
            ["Mammoth"] = Unimog, ["RustOx"] = Unimog,
            ["TrailBurner"] = Gm, ["Cumbre"] = Gm,
        };

        public static DriveSystem For(string carInfoName) =>
            carInfoName != null && ByVehicle.TryGetValue(carInfoName, out var s) ? s : Generic;
    }
}
