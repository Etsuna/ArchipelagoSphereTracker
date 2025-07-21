from worlds.LauncherComponents import Component, components, Type
import os
import sys
import json
import yaml
from dataclasses import fields, MISSING

from BaseClasses import MultiWorld
from Utils import user_path, local_path
from Options import Option
from worlds.AutoWorld import AutoWorldRegister

def instantiate_options(options_cls, yaml_opts):
    if hasattr(options_cls, "from_yaml"):
        return options_cls.from_yaml(yaml_opts)

    kwargs = {}
    try:
        for f in fields(options_cls):
            if f.name in yaml_opts:
                kwargs[f.name] = yaml_opts[f.name]
            elif f.default is not MISSING:
                kwargs[f.name] = f.default
            elif f.default_factory is not MISSING:
                kwargs[f.name] = f.default_factory()
            elif isinstance(f.type, type) and issubclass(f.type, Option):
                try:
                    kwargs[f.name] = f.type.from_any(None)
                except Exception:
                    kwargs[f.name] = getattr(f.type, "default", None)
            else:
                kwargs[f.name] = None
        return options_cls(**kwargs)
    except Exception as e:
        print(f"[WARN] Could not instantiate {options_cls.__name__}: {e}")
        return None

class FakeMultiWorld(MultiWorld):
    def __init__(self):
        super().__init__(1)
        self.player_name = {1: "Player"}
        self.worlds = {}
        self.random = __import__("random")
        self.early_items = {1: {}}
        self.local_early_items = {1: {}}

def load_yaml_options(path):
    with open(path, encoding="utf-8") as f:
        data = yaml.safe_load(f) or {}
    return data.get("options", {})

def categorize_items(world_cls, opts_dict, player=1):
    categories = {k: [] for k in (
        "PROGRESSION", "USEFUL", "FILLER", "TRAP", "PROGRESSION_SKIP_BALANCING")}
    try:
        mw = FakeMultiWorld()
        world = world_cls(mw, player)
        mw.worlds[player] = world

        options_cls = getattr(world_cls, "options_dataclass", None)
        if options_cls:
            options = instantiate_options(options_cls, opts_dict)
            if not options:
                print(f"[WARN] Skipping {world_cls.__name__}: no valid options.")
                return categories
            world.options = options
        else:
            print(f"[WARN] Skipping {world_cls.__name__}: no options_dataclass.")
            return categories

        for name in getattr(world_cls, "item_names", []):
            try:
                item = world.create_item(name)
                if item.advancement:
                    categories["PROGRESSION"].append(name)
                elif item.useful:
                    categories["USEFUL"].append(name)
                else:
                    categories["FILLER"].append(name)
                if item.trap:
                    categories["TRAP"].append(name)
                if getattr(item, "skip_in_prog_balancing", False):
                    categories["PROGRESSION_SKIP_BALANCING"].append(name)
            except Exception as e:
                print(f"[ERROR] Item creation failed for {name} in {world_cls.__name__}: {e}")
    except Exception as e:
        import traceback
        traceback.print_exc()
        print(f"[ERROR] {world_cls.__name__}: {e}")

    return categories

def launch_scan_items():
    TEMPLATES_DIR = user_path("Players", "Templates")
    OUTPUT_DIR = local_path("ItemCategory")
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    OUTPUT_JSON = os.path.join(OUTPUT_DIR, "item_category_by_game.json")

    print("[INFO] Loading available worlds (from templates)...")
    items_by_game = {}

    for file in os.listdir(TEMPLATES_DIR):
        if not file.endswith(".yaml"):
            continue
        game = file[:-5]
        world_cls = AutoWorldRegister.world_types.get(game)
        if not world_cls:
            print(f"[WARN] Unknown game: {game}")
            continue

        opts = load_yaml_options(os.path.join(TEMPLATES_DIR, file))
        cats = categorize_items(world_cls, opts)
        if any(cats.values()):
            print(f"[OK] {game} processed.")
            items_by_game[game] = cats

    with open(OUTPUT_JSON, "w", encoding="utf-8") as f:
        json.dump(items_by_game, f, ensure_ascii=False, indent=4)

    print(f"[DONE] JSON file written to: {OUTPUT_JSON}")

components.append(Component(
    display_name="Scan Items",
    func=launch_scan_items,
    component_type=Type.TOOL,
    description="Scanne les items définis par les templates YAML."
))
