using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class GenerateItemsTable
{
    public static string pythonCode = @"
import os
import importlib.util
import sys
import json
import re
from pathlib import Path
from types import ModuleType
from BaseClasses import ItemClassification

BASE_DIR = Path(r""{WORLD_PATH}"")
output = {}
error_games = []

# List of possible item table names
CUSTOM_ITEM_TABLE_NAMES = [
    ""item_table"",
    ""ahit_items"",
    ""items"",
    ""links_awakening_items"",
    ""ITEM_TABLE"",
    ""l2ac_item_table"",
    ""item_info"",
    ""cvcotm_item_info"",
    ""filler_items""
]

# Dictionary to keep track of already processed games
processed_games = set()

def extract_true_game_name(code: str) -> str | None:
    # First, look for a direct assignment to a string
    match_direct = re.search(r'game\s*(?::\s*\w+)?\s*=\s*[""\']([^""\']+)[""\']', code)
    if match_direct:
        return match_direct.group(1).strip()

    # Look for ClassVar declarations
    match_classvar = re.search(r'game\s*:\s*ClassVar\s*\[\w+\]\s*=\s*[""\']([^""\']+)[""\']', code)
    if match_classvar:
        return match_classvar.group(1).strip()

    # Otherwise, look for game = CONSTANT
    match_var = re.search(r'game\s*(?::\s*\w+)?\s*=\s*([A-Z_]+)', code)
    if match_var:
        const_name = match_var.group(1).strip()
        # Search for the definition of this constant
        match_const = re.search(rf'{const_name}\s*=\s*[""\']([^""\']+)[""\']', code)
        if match_const:
            return match_const.group(1).strip()

    return None

def patch_relative_imports(code: str, game_name: str) -> str:
    rel_prefix = f""worlds.{game_name}""

    # Case: from .foo.bar import baz
    code = re.sub(r""from\s+\.(\w+(?:\.\w+)+)\s+import"",
                  rf""from {rel_prefix}.\1 import"", code)

    # Case: from . import A, B
    code = re.sub(r""from\s+\.\s+import\s+([a-zA-Z0-9_,\s]+)"",
                  rf""from {rel_prefix} import \1"", code)

    # Case: from .X import Y
    code = re.sub(r""from\s+\.(\w+)\s+import"",
                  rf""from {rel_prefix}.\1 import"", code)

    # Case: import .X
    code = re.sub(r""import\s+\.(\w+)"",
                  rf""import {rel_prefix}.\1"", code)

    # Special case for TYPE_CHECKING
    code = re.sub(r""from\s+\.(\w+)\s+import\s+(\w+)"",
                  rf""from {rel_prefix}.\1 import \2"", code)

    return code

def remove_type_checking_blocks(code: str) -> str:
    lines = code.splitlines()
    result = []
    skip_block = False
    indent_level = None

    for line in lines:
        stripped = line.strip()
        if stripped.startswith(""if TYPE_CHECKING:""):
            skip_block = True
            indent_level = len(line) - len(line.lstrip())
            continue

        if skip_block:
            current_indent = len(line) - len(line.lstrip())
            if current_indent > indent_level or not stripped:
                continue  # skip lines inside TYPE_CHECKING block
            else:
                skip_block = False  # End of block

        result.append(line)

    return ""\n"".join(result)

def load_module_force(path: Path, game_name: str) -> ModuleType | None:
    try:
        # Manually define '__file__' in the module
        module_name = f""temp_module_{game_name}""
        sys.modules[module_name] = ModuleType(module_name)
        
        # Manually set __file__
        sys.modules[module_name].__file__ = str(path)

        # Raw read the file
        with open(path, ""r"", encoding=""utf-8"") as f:
            code = f.read()

        # Patch relative imports to absolute ones
        rel_prefix = f""worlds.{game_name}""
        code = re.sub(r""from\s+\.(\w+(?:\.\w+)+)\s+import"", rf""from {rel_prefix}.\1 import"", code)
        code = re.sub(r""from\s+\.\s+import\s+([a-zA-Z0-9_,\s]+)"", rf""from {rel_prefix} import \1"", code)
        code = re.sub(r""from\s+\.(\w+)\s+import"", rf""from {rel_prefix}.\1 import"", code)
        code = re.sub(r""import\s+\.(\w+)"", rf""import {rel_prefix}.\1"", code)
        code = re.sub(r""from\s+\.\s+import\s+(\w+)"", rf""from worlds.{game_name} import \1"", code)

        # Execute the code in the module
        exec(code, sys.modules[module_name].__dict__)

        return sys.modules[module_name]
    
    except Exception as e:
        print(f""[ERROR] Forced load failed for {path}: {e}"")
        return None


def extract_items(game_name: str, module: ModuleType, true_game_name: str):
    # Check if the game has already been processed
    if game_name in processed_games:
        print(f""[INFO] Skipping already processed game '{game_name}'"")
        return

    # SPECIFIC PATCH FOR LADX
    if game_name.lower() == ""ladx"":
        true_game_name = ""Links Awakening DX""
        if hasattr(module, ""links_awakening_items""):
            print(f""[INFO] Using custom patch for LADX"")
            item_list = getattr(module, ""links_awakening_items"")
            items_by_type = {
                ""progression"": [],
                ""useful"": [],
                ""filler"": [],
                ""trap"": [],
                ""progression_skip_balancing"": []
            }

            for item in item_list:
                try:
                    name = item.item_name
                    classification = item.classification
                    class_str = classification.name if isinstance(classification, ItemClassification) else str(classification)
                    if class_str not in items_by_type:
                        items_by_type[class_str] = []
                    items_by_type[class_str].append(name)
                except Exception as e:
                    print(f""[ERROR] LADX item parse fail for {item}: {e}"")
                    continue

            output[true_game_name] = items_by_type
            processed_games.add(game_name)
            return

    # PATCH FOR DOOM AND HERETIC
    if game_name.lower() in (""doom_1993"", ""doom_ii"", ""heretic"", ""hylics2""):
        if hasattr(module, ""item_table""):
            print(f""[INFO] Using custom patch for DOOM and HERETIC"")
            table = getattr(module, ""item_table"")

            # List only progression items by name
            progression_names = [
                v[""name""] for k, v in table.items()
                if v.get(""classification"") == ItemClassification.progression
            ]

            output[true_game_name] = {
                ""progression"": progression_names
            }
            processed_games.add(game_name)
            return

    # PATCH FOR SUBNAUTICA
    if game_name.lower() == ""subnautica"":
        if hasattr(module, ""item_table""):
            print(f""[INFO] Using custom patch for Subnautica"")
            table = getattr(module, ""item_table"")

            items_by_type = {
                ""progression"": [],
                ""useful"": [],
                ""filler"": [],
                ""trap"": [],
                ""progression_skip_balancing"": []
            }

            for item_id, item in table.items():
                try:
                    class_str = item.classification.name if isinstance(item.classification, ItemClassification) else str(item.classification)
                    if class_str not in items_by_type:
                        items_by_type[class_str] = []
                    items_by_type[class_str].append(item.name)
                except Exception as e:
                    print(f""[ERROR] Subnautica item parse fail for {item_id}: {e}"")
                    continue

            output[true_game_name] = items_by_type
            processed_games.add(game_name)
            return

    # PATCH FOR KH2
    if game_name.lower() == ""kh2"":
        print(f""[INFO] Using custom patch for KH2"")

        try:
            item_dict = getattr(module, ""item_dictionary_table"", {})
            progression_names = set(getattr(module, ""Progression_Table"", {}).keys())
            useful_names = set(getattr(module, ""Usefull_Table"", {}).keys())
            filler_values = set(getattr(module, ""filler_items"", []))  # These are values like ""Potion"", not keys

            # Inverser le mapping de ItemName (valeur → clé)
            itemname_value_map = {
                v: k for k, v in module.__dict__.get(""ItemName"", {}).__dict__.items()
                if not k.startswith(""__"")
            } if hasattr(module, ""ItemName"") else {}

            # Ou à défaut, faire une simulation basique si ItemName pas dispo
            def key_to_value(key):
                try:
                    return getattr(module.ItemName, key)
                except Exception:
                    return key

            items_by_type = {
                ""progression"": [],
                ""useful"": [],
                ""filler"": [],
            }

            for name, item in item_dict.items():
                try:
                    readable_value = key_to_value(name)

                    if name in progression_names:
                        items_by_type[""progression""].append(name)
                    elif name in useful_names:
                        items_by_type[""useful""].append(name)
                    elif readable_value in filler_values:
                        items_by_type[""filler""].append(name)
                except Exception as e:
                    print(f""[ERROR] KH2 item classification failed for {name}: {e}"")
                    continue

            output[true_game_name] = items_by_type
            processed_games.add(game_name)
            return

        except Exception as e:
            print(f""[ERROR] Failed custom KH2 patch: {e}"")
            output[true_game_name] = {""filler"": []}
            processed_games.add(game_name)
            return

    table = None
    table_name_used = None

    for table_name in CUSTOM_ITEM_TABLE_NAMES:
        if hasattr(module, table_name):
            candidate = getattr(module, table_name)
            if isinstance(candidate, (dict, list)):
                table = candidate
                table_name_used = table_name
                break

    if table is None:
        print(f""[WARN] No usable item table found in {game_name}"")
        output[true_game_name] = {""filler"": []}
        processed_games.add(game_name)
        return

    print(f""[INFO] Using table '{table_name_used}' for game '{game_name}_{true_game_name}'"")

    items_by_type = {
        ""progression"": [],
        ""useful"": [],
        ""filler"": [],
        ""trap"": [],
        ""progression_skip_balancing"": []
    }

    if isinstance(table, dict):
        items_iter = table.items()
    elif isinstance(table, list):
        items_iter = ((entry[""name""], entry) for entry in table if isinstance(entry, dict) and ""name"" in entry)
    else:
        print(f""[ERROR] Unsupported item table format in {game_name}"")
        output[true_game_name] = {""filler"": []}
        processed_games.add(game_name)
        return

    for name, data in items_iter:
        try:
            if hasattr(data, ""classification""):
                classification = data.classification
            elif isinstance(data, dict) and ""classification"" in data:
                classification = data[""classification""]
            elif isinstance(data, tuple) and len(data) >= 2:
                classification = classify_from_tuple(name, data)
            else:
                continue

            class_str = classification.name if isinstance(classification, ItemClassification) else str(classification)
            if class_str not in items_by_type:
                items_by_type[class_str] = []
            items_by_type[class_str].append(name)
        except Exception as e:
            print(f""[ERROR] Item parse fail in {game_name} for {name}: {e}"")
            continue

    output[true_game_name] = items_by_type
    processed_games.add(game_name)

def classify_from_tuple(name, data, force_not_advancement=False):
    _, advancement, *_ = data
    if force_not_advancement:
        return ItemClassification.useful
    if name == ""Ice Trap"":
        return ItemClassification.trap
    if name in {'Gold Skulltula Token', 'Triforce Piece'}:
        return ItemClassification.progression_skip_balancing
    if advancement:
        return ItemClassification.progression
    return ItemClassification.filler

# List of games to exclude
excluded_games = [""noita"", ""raft"", ""sm"", ""smz3""]

# Scan all item.py / items.py files
for root, dirs, files in os.walk(BASE_DIR):
    for file in files:
        # Exclude problematic folders
        if any(excluded_game in root for excluded_game in excluded_games):
            continue

        if file.lower() in (""item.py"", ""items.py""):
            path = Path(root) / file
            # Read __init__.py in the parent folder to extract the true game name
            init_path = Path(root) / ""__init__.py""
            true_game_name = None

            if init_path.exists():
                try:
                    with open(init_path, ""r"", encoding=""utf-8"") as init_file:
                        init_code = init_file.read()
                        true_game_name = extract_true_game_name(init_code)
                except Exception as e:
                    print(f""[WARN] Failed to read {init_path}: {e}"")

            # Fallback on folder name if not found
            game_name = true_game_name or path.parent.name
                        
            # Check if this game has already been processed
            if game_name in processed_games:
                print(f""[INFO] Skipping already processed game '{game_name}'"")
                continue
            
            mod = load_module_force(path, path.parent.name)
            if mod:
                extract_items(path.parent.name, mod, true_game_name)
            else:
                print(f""[ERROR] Failed to process game: {game_name}"")
                error_games.append(game_name)
                output[true_game_name] = {""filler"": []}
                processed_games.add(game_name)


# Final JSON export
with open(""all_items_by_game.json"", ""w"", encoding=""utf-8"") as f:
    json.dump(output, f, indent=2, ensure_ascii=False)

with open(""error_games.json"", ""w"", encoding=""utf-8"") as f:
    json.dump(error_games, f, indent=2, ensure_ascii=False)

print("" - Extraction complete. Files saved:"")
print("" - all_items_by_game.json"")
print("" - error_games.json"")
";
}
