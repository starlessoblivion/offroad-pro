# Offroad Pro

A BepInEx mod for **Over the Hill** (Funselektor, 2026) that adds real-world off-road systems: rear wheel steering, tank turning, terrain modes with a central tire inflation system (CTIS), and a drivetrain dial on the HUD.

Tested with MelonLoader 0.7.3 on the Windows build of the game under Proton (Steam build 26047935) and on the native Linux build (Steam build 25992246), and with BepInEx 6 bleeding edge #789 on native Linux. The game is Unity 6000.0.75, IL2CPP.

## Features

### Rear Wheel Steering (upgrade, all vehicles)
Buy and fit it in the garage's performance upgrades (75 pebbles). The rear wheels counter-steer at 35% of the front angle for a much tighter turning circle, with animated rear wheels. Remove the upgrade to switch it off.

### Tank Turn (upgrade, all vehicles)
Buy and fit it in the performance upgrades (100 pebbles). At a standstill, hold **A** on a gamepad or **Left Alt** on keyboard and steer to spin in place. The wheels on each side counter-rotate and break loose, throwing dirt like a G-Class G-TURN. No throttle needed. It only engages while driving (not in menus, the garage or the radial menu) and stays off while the winch is attached.

### Terrain modes and CTIS (built in, all vehicles)
A new entry in the radial menu cycles **Trail, Mud, Sand, Snow, Ford**. Each vehicle uses the terrain system and mode names of its real-world counterpart (G.O.A.T. Modes, Terrain Response, Multi-Terrain Select, Selec-Terrain, DYNAMIC SELECT and others).

| Mode | Tire pressure | Diffs | Other |
|---|---|---|---|
| Trail | 32 psi | stock | game default |
| Mud | 20 psi | front, rear and center locked | |
| Sand | 15 psi | center locked | |
| Snow | 22 psi | open | softer throttle |
| Ford / Wade | 32 psi | stock | +12 cm ride height, higher center of gravity |

- Tires deflate and inflate over time like a real CTIS. The physics radius shrinks so the vehicle sits lower and the tire model flattens against the ground, and grip rises as pressure drops.
- Aired-down tires pump back up to a safe pressure above 60 km/h.
- Ride height and tire changes stack on top of the game's own Suspension Lift and tire upgrades.
- Ford mode raises the suspension but also the center of gravity, so it is a trade-off for water crossings, not a free height bonus.
- Sounds: the transfer-case clunk on mode change, a hiss when pressure starts changing, and the winch motor while the suspension moves.

### Drivetrain dial (HUD)
A third dial to the right of the pitch and roll indicators:
- Top-down drivetrain with live steering angles. Front tires green, rear tires green when Rear Wheel Steering is fitted.
- Shafts white in High range, amber in Low range. Diff markers light up when a mode locks them.
- The four mode names around the curved rim, active mode highlighted.
- Distance to your map waypoint with an orange direction wedge, and current tire pressure.
- The terrain system name along the flat bottom edge.
- Units follow your system locale (imperial or metric), or set them in the config.
- Your waypoint clears itself when you get within 20 ft (6 m) of it.

## Installing (players)

Requires **MelonLoader 0.7.3 or newer** (Windows, Linux under Proton, or native Linux).

1. Download `OffroadPro_<version>_MelonLoader_build<game build>.zip` from the releases page.
2. Put `OffroadPro.dll` in the game's `Mods` folder and launch the game.

Settings are in `UserData/local.oth.drivesystems.cfg` after the first run.

### BepInEx (alternative)

The mod also builds as a BepInEx 6 plugin. BepInEx and MelonLoader cannot share a game folder, so use one or the other.


1. Install **BepInEx 6 bleeding edge, IL2CPP**, from https://builds.bepinex.dev/projects/bepinex_be. Use the `BepInEx-Unity.IL2CPP` build for your platform (`linux-x64` or `win-x64`) and extract its contents directly into the game folder (not into a subfolder named after the zip).
2. **Linux only:** in `run_bepinex.sh` set `executable_name="over the hill.x86_64"`, then set the Steam launch option to:
   ```
   "/path/to/steamapps/common/over the hill/run_bepinex.sh" %command%
   ```
3. Launch the game once and quit at the main menu. BepInEx generates its interop files on this first run, which takes a few minutes.
4. **Required:** open `BepInEx/config/BepInEx.cfg` and set `Enabled = false` under `[Logging.Console]`. With the console logger on, the game crashes at boot on Linux.
5. Copy `OthDriveSystems.dll` into `BepInEx/plugins/OthDriveSystems/`.

## Building from source

Requirements: .NET SDK 6 or newer, and the game with BepInEx installed and run once (the build references the interop assemblies BepInEx generates from your copy of the game; none of the game's files are in this repo).

```
git clone https://github.com/starlessoblivion/offroad-pro
cd offroad-pro
dotnet build -c Release -p:GameDir="/path/to/steamapps/common/over the hill"
```

Without `-p:GameDir`, the build looks in the default Steam library for your OS. The output is `bin/Release/net6.0/OthDriveSystems.dll`.

The MelonLoader build uses the same source and the same BepInEx interop assemblies, plus MelonLoader 0.7.3's `MelonLoader/net6` folder (extract it to `.cache/MelonLoader-0.7.3/` or pass `-p:MelonLoaderDir=...`; `tools/package.sh` downloads it):
```
dotnet build -c Release -p:GameDir="/path/to/steamapps/common/over the hill" -p:Loader=MelonLoader
```
After compiling, `tools/ml-rewrite` renames the game references to MelonLoader's naming (MelonLoader generates its interop assemblies with an `Il2Cpp` prefix on every namespace and assembly outside `Assembly-CSharp*` and `Unity*`, BepInEx does not). The output is `bin/Release-MelonLoader/net6.0/OffroadPro.dll`.

`tools/package.sh <version> [GameDir]` builds all three release zips into `dist/`.

`tools/interop-dump` is a small metadata reader for exploring the generated interop assemblies:
```
cd tools/interop-dump && dotnet build -c Release -o out
./out/interop-dump "<game>/BepInEx/interop/Assembly-CSharp.dll" VehicleUpgrade VehiclePhysics.Steering
```

## Configuration

After the first run with the mod, settings are in `BepInEx/config/local.oth.drivesystems.cfg`. They include prices, rear steer ratio, tank turn button, key, speed, wheelspin and touchiness, every mode's tire pressure and ride height, inflate and deflate rates, center of gravity penalty, dial text size and placement, units and the waypoint clear distance.

## Troubleshooting

- **Crash at boot right after "Parsing command line args":** the BepInEx console logger is still on (install step 4).
- **Occasional crash at startup:** this comes from the .NET runtime bundled with BepInEx, not from the mod. Relaunching works. On Linux the full .NET error is in the system journal (`journalctl --user`), not in Player.log.
- **Diagnostics:** set `VerboseLog = true` under `[Debug]`. `BepInEx/OthDriveSystems.trace.log` records each startup step and is flushed immediately, so its last line shows where a native crash happened. `DisabledPatches` switches individual patches off by class name.

## Uninstalling

1. Remove the mod's items from your save so the unmodded game loads it cleanly. Close the game first:
   ```
   python3 tools/remove_mod_items.py "/path/to/My Games/over the hill/Saves/Player1.json"
   ```
   It writes a backup next to the save before changing it.
2. Delete `BepInEx/plugins/OthDriveSystems/`. To remove BepInEx too, delete its files and clear the launch option.

## Notes

- The two upgrades use the game's unused `Drone` and `Health` upgrade categories, which nothing else in the game reads.
- In co-op, the features act on your own vehicle. Other players without the mod may not see your rear wheels turn.
- A game update can break the mod until it is rebuilt against the new interop files.

## License

MIT, see [LICENSE](LICENSE). Over the Hill and its assets belong to Funselektor; this repo contains none of the game's files.
