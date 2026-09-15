# Diagrams / Diagrammes

The Wiki documents both the `/ast` command center and the restored direct commands. Native Mermaid diagrams remain easier to keep current and edit directly in Markdown than command screenshots.

Le Wiki documente à la fois le centre `/ast` et les commandes directes restaurées. Les diagrammes Mermaid natifs restent plus simples à maintenir à jour et à modifier que des captures de commandes.

## Overview / Vue d’ensemble

```mermaid
flowchart LR
    U["Discord /ast"] --> B["AST"]
    C["Direct slash commands / Commandes directes"] --> B
    P["Private portal / Portail privé"] --> B
    B --> W["WebHost Archipelago"]
    B --> D[("AST.db")]
    B --> N["Discord notifications / Notifications Discord"]
    B -. "Archipelago Mode" .-> F["YAML · APWorld · generation / génération"]
```

## Navigation `/ast`

```mermaid
flowchart TD
    A["/ast"] --> P["My space / Mon espace"]
    A --> R["The room / La room"]
    A --> M["Manage / Gérer"]
    A --> T["Archipelago tools / Outils Archipelago"]
    A --> G["Administration AST"]
    A --> I["Instance AST"]
    T --> Y["YAML · APWorld · Generation / Génération · Templates / Modèles"]
```

## More diagrams / Autres diagrammes

- [[Room creation and adaptive polling / Création et polling adaptatif|Room-tracking]]
- [[Portals and token rotation / Portails et rotation des tokens|Web-portal]]
- [[YAML/APWorld/generation workflow|Archipelago-mode]]
- [[Authorization levels / Niveaux d’autorisation|Administration-and-security]]
- [[Storage and migration / Stockage et migration|Data-and-migrations]]
