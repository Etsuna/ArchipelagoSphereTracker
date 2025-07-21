from worlds.LauncherComponents import Component, components, Type
import Utils
from Options import generate_yaml_templates

def generate_templates():
    target = Utils.user_path("Players", "Templates")
    generate_yaml_templates(target, False)
    print(f"[OK] YAML templates generated at: {target}")

components.append(Component(
    display_name="Generate Templates",
    func=generate_templates,
    component_type=Type.TOOL,
    description="Generate YAML templates without opening Explorer."
))
