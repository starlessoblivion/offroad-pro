namespace OthDriveSystems
{
    // The game has no imperial/metric setting, so follow the config, defaulting to the system region.
    internal static class Units
    {
        public static bool Imperial
        {
            get
            {
                var mode = Plugin.Cfg.UnitSystem.Value;
                if (mode == "Imperial") return true;
                if (mode == "Metric") return false;
                // BepInEx's .NET runs in invariant-culture mode, so read the locale from the environment instead.
                var loc = System.Environment.GetEnvironmentVariable("LC_MEASUREMENT")
                          ?? System.Environment.GetEnvironmentVariable("LC_ALL")
                          ?? System.Environment.GetEnvironmentVariable("LANG") ?? "";
                return loc.Contains("_US") || loc.Contains("_LR") || loc.Contains("_MM");
            }
        }

        public static string Distance(float metres)
        {
            if (Imperial)
            {
                float feet = metres * 3.28084f;
                return feet >= 1000f ? $"{metres / 1609.344f:0.00} mi" : $"{feet:0} ft";
            }
            return metres >= 1000f ? $"{metres / 1000f:0.00} km" : $"{metres:0} m";
        }

        public static string Pressure(float psi) => Imperial ? $"{psi:0} psi" : $"{psi * 0.0689476f:0.0} bar";
    }
}
