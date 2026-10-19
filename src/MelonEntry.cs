#if MELONLOADER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using OthDriveSystems.Compat;

[assembly: MelonInfo(typeof(OthDriveSystems.Plugin), OthDriveSystems.Plugin.Name, OthDriveSystems.Plugin.Version, "starlessoblivion", "https://github.com/starlessoblivion/offroad-pro")]
[assembly: MelonGame("Funselektor", "over the hill")]
[assembly: HarmonyDontPatchAll] // patched per class in PatchAll so DisabledPatches works

namespace OthDriveSystems
{
    public class Plugin : MelonMod
    {
        public const string Guid = "local.oth.drivesystems";
        public const string Name = "Offroad Pro";
        public const string Version = "0.1.1";

        internal static ManualLogSource L;
        internal static Settings Cfg;

        public override void OnInitializeMelon()
        {
            L = new ManualLogSource(LoggerInstance);
            Trace.Start(MelonEnvironment.UserDataDirectory);
            Trace.Step("config");
            Cfg = new Settings(new ConfigFile(Path.Combine(MelonEnvironment.UserDataDirectory, Guid + ".cfg")));
            foreach (var t in typeof(Plugin).Assembly.GetTypes())
            {
                if (t.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Length == 0) continue;
                if (Cfg.DisabledPatches.Value.Contains(t.Name)) { Trace.Step($"patch {t.Name} skipped (config)"); continue; }
                Trace.Step($"patch {t.Name}");
                HarmonyInstance.CreateClassProcessor(t).Patch();
            }
            Trace.Step("loaded");
            L.LogInfo($"{Name} {Version} loaded");
        }
    }
}

// Stand-ins for the few BepInEx types the shared code uses, so the features compile unchanged for MelonLoader.
namespace OthDriveSystems.Compat
{
    internal class ManualLogSource
    {
        readonly MelonLogger.Instance log;
        public ManualLogSource(MelonLogger.Instance log) => this.log = log;
        public void LogInfo(object o) => log.Msg(o?.ToString());
        public void LogWarning(object o) => log.Warning(o?.ToString());
        public void LogError(object o) => log.Error(o?.ToString());
    }

    internal class ConfigEntry<T>
    {
        public T Value { get; set; }
    }

    // Same file layout as a BepInEx .cfg, so settings can be copied between the two builds.
    internal class ConfigFile
    {
        readonly string path;
        readonly Dictionary<string, string> saved = new();
        readonly List<(string section, string key, string description, string defaultValue, Func<string> current)> entries = new();

        public ConfigFile(string path)
        {
            this.path = path;
            if (!File.Exists(path)) return;
            string section = "";
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) { section = line[1..^1]; continue; }
                int eq = line.IndexOf('=');
                if (eq > 0) saved[section + "\n" + line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }

        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            var e = new ConfigEntry<T> { Value = defaultValue };
            if (saved.TryGetValue(section + "\n" + key, out var s))
            {
                try { e.Value = (T)Convert.ChangeType(s, typeof(T), CultureInfo.InvariantCulture); } catch { }
            }
            entries.Add((section, key, description, Format(defaultValue), () => Format(e.Value)));
            Save();
            return e;
        }

        static string Format(object v) => v switch
        {
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => v?.ToString() ?? "",
        };

        void Save()
        {
            var sb = new StringBuilder();
            string section = null;
            foreach (var g in entries.GroupBy(x => x.section))
            {
                if (section != null) sb.Append('\n');
                section = g.Key;
                sb.Append('[').Append(section).Append("]\n\n");
                foreach (var x in g)
                {
                    sb.Append("## ").Append(x.description).Append('\n');
                    sb.Append("# Default value: ").Append(x.defaultValue).Append('\n');
                    sb.Append(x.key).Append(" = ").Append(x.current()).Append("\n\n");
                }
            }
            try { File.WriteAllText(path, sb.ToString()); } catch { }
        }
    }
}
#endif
