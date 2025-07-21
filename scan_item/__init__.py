from worlds.LauncherComponents import Component, components, Type, launch_subprocess
import os
import tempfile
import shutil

def launch_scan_items():
    # Récupère le dossier dans le zip
    current_dir = os.path.dirname(__file__)
    script_path = os.path.join(current_dir, "scan_items.py")

    if not os.path.isfile(script_path):
        print(f"[ERROR] scan_items.py not found at {script_path}")
        return

    # Extraction vers dossier temporaire
    temp_dir = tempfile.mkdtemp(prefix="ap_scan_items_")
    shutil.copy(script_path, os.path.join(temp_dir, "scan_items.py"))

    print(f"[DEBUG] Extracted scan_items.py to: {os.path.join(temp_dir, 'scan_items.py')}")

    # Lance la fonction main() du script
    launch_subprocess(("scan_items.py", "main"), name="ScanItems", base_path=temp_dir)

# Ajoute le composant
components.append(Component(
    display_name="Scan Items",
    func=launch_scan_items,
    component_type=Type.TOOL,
    description="Scanne les items définis par les templates YAML."
))
