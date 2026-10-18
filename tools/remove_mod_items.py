#!/usr/bin/env python3
"""Remove OTH Drive Systems items from an Over the Hill save so the game loads it without the mod.

Usage: python3 remove_mod_items.py [path/to/Player1.json]
Close the game first. Writes Player1.json.pre-mod-removal next to the save before changing it.
"""
import json, shutil, sys
from pathlib import Path

MOD_IDS = {3029270423, 561285334}   # Rear Wheel Steering, Tank Turn

path = Path(sys.argv[1] if len(sys.argv) > 1 else Path.home() / "My Games/over the hill/Saves/Player1.json")
data = json.loads(path.read_text())
items = data["Profile"]["Inventory"]["Items"]
kept = [i for i in items if i.get("Data", {}).get("ItemId") not in MOD_IDS]
removed = len(items) - len(kept)
if removed:
    shutil.copy2(path, path.with_name(path.name + ".pre-mod-removal"))
    data["Profile"]["Inventory"]["Items"] = kept
    path.write_text(json.dumps(data, separators=(",", ":"), ensure_ascii=False))
print(f"removed {removed} mod item(s) from {path}")
