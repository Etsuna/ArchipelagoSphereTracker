using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class GenerateItemsTable
{
    public static string pythonCode = @"
import os
import sys
import importlib
import json
sys.stdout.reconfigure(encoding='utf-8')

from BaseClasses import MultiWorld
from worlds.AutoWorld import AutoWorldRegister

# 📁 Adapte ce chemin si besoin
ARCHIPELAGO_ROOT = r""{ARCHIPELAGO_PATH}""
WORLDS_PATH = os.path.join(ARCHIPELAGO_ROOT, ""worlds"")

# 📌 Ajoute Archipelago à sys.path pour les imports
sys.path.insert(0, ARCHIPELAGO_ROOT)

# 🧪 Faux environnement minimal pour instancier un World
class FakeMultiWorld(MultiWorld):
    def __init__(self):
        super().__init__(1)
        self.player_name = {1: ""Player""}
        self.worlds = {}
        self.random = __import__(""random"")
        self.early_items = {1: {}}
        self.local_early_items = {1: {}}

def import_world_modules(worlds_path):
    for game_name in os.listdir(worlds_path):
        game_path = os.path.join(worlds_path, game_name)
        if not os.path.isdir(game_path) or game_name.startswith(""__""):
            continue

        try:
            module_name = f""worlds.{game_name}""
            importlib.import_module(module_name)
        except Exception as e:
            print(f""Erreur lors de l'import de {module_name}: {e}"")

def categorize_items(world_class, player: int = 1):
    """"""Catégorise les objets d'un monde donné.""""""
    from types import SimpleNamespace

    # Patch d'options personnalisées pour chaque jeu connu
    DEFAULT_WORLD_OPTIONS = {
        ""civviworld"": {""boostsanity"": False},
        ""darksouls3world"": {
            ""randomize_weapon_level"": ""none"",
            ""randomize_infusion"": False,
            ""enable_dlc"": True,
            ""enable_ngp"": True
        },
        ""lingoworld"": {
            ""shuffle_paintings"": 0
        },
        ""MessengerWorld"": {
            ""logic_level"": 0
            },
        ""ShortHikeWorld"": {
            ""easier_races"": False
            },
        ""timespinnerworld"": {
            ""prism_break"": False,
            ""gate_keep"": False,
            ""gyre_archives"": False,
            ""eye_spy"": True,
            ""lock_key_amadeus"": True,
            ""downloadable_items"": True,
            ""unchained_keys"": True  # Default value, adjust as needed
        },
        ""tunicworld"": {
            ""combat_logic"": True,
            ""grass_randomizer"": False,
            ""breakable_shuffle"": False,
            ""gyre_archives"": False,
            ""downloadable_items"": True,
        }
        # Tu peux ajouter d'autres ici...
    }

    # Liste des options par défaut à vérifier et appliquer si manquantes
    DEFAULT_OPTIONS_CHECK = {
        ""logic_level"": 0,
        ""easier_races"": True,
        ""shuffle_paintings"": 0
    }

    categorized = {
        ""progression"": [],
        ""useful"": [],
        ""filler"": [],
        ""trap"": [],
        ""progression_skip_balancing"": []
    }

    try:
        mw = FakeMultiWorld()
        world_instance = world_class(mw, player)
        mw.worlds[player] = world_instance

        # Appliquer options si nécessaires
        class_name = world_class.__name__.lower()
        default_opts = DEFAULT_WORLD_OPTIONS.get(class_name, {})

        # Appliquer les valeurs par défaut génériques si non présentes
        for option, default_value in DEFAULT_OPTIONS_CHECK.items():
            if option not in default_opts:
                default_opts[option] = default_value

        # Appliquer les options spécifiques au monde
        options = SimpleNamespace(**default_opts)
        world_instance.options = options

        # Catégoriser les items
        for item_name in getattr(world_class, ""item_names"", []):
            item = world_instance.create_item(item_name)
            if item.advancement:
                categorized[""progression""].append(item.name)
            elif item.useful:
                categorized[""useful""].append(item.name)
            else:
                categorized[""filler""].append(item.name)

            if item.trap:
                categorized[""trap""].append(item.name)
            if getattr(item, ""skip_in_prog_balancing"", False):
                categorized[""progression_skip_balancing""].append(item.name)

    except Exception as e:
        import traceback
        traceback.print_exc()
        print(f""Erreur en catégorisant les items de {world_class.__name__}: {e}"")

    return categorized

def main():
    import_world_modules(WORLDS_PATH)

    all_worlds_items = {}

    for world_name, world_class in AutoWorldRegister.world_types.items():
        if world_name == ""Archipelago"":
            continue
        
        print(f""Traitement de {world_name}..."")
        categorized = categorize_items(world_class)
        if any(categorized.values()):
            all_worlds_items[world_name] = categorized


    with open(""all_items_by_game.json"", ""w"", encoding=""utf-8"") as f:
        json.dump(all_worlds_items, f, indent=4, ensure_ascii=False)

    print(""Extraction done : categorized_items.json"")

if __name__ == ""__main__"":
    main()
";
}
