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

## Room preparation / Préparation des rooms

```mermaid
flowchart TD
    A["/organize-room → message in current channel / salon courant"] --> B["Register / S’inscrire"]
    B --> C["YAML: command or Web / commande ou Web"]
    C --> D["Validation → Ready / Prêt"]
    D --> E["Organizer generates / L’organisateur génère"]
    E --> F{"Generation OK?"}
    F -->|No / Non| G["Keep preparation for correction / Conserver pour correction"]
    F -->|Yes / Oui| H{"ZIP attachment accepted?"}
    H -->|Yes / Oui| I["Send ZIP / Envoyer le ZIP"]
    H -->|No / Non| J["Publish 24-hour link / Publier un lien valable 24 h"]
    I --> K["Delete preparation data / Supprimer les données"]
    J -->|"Published / Publié"| K
    J -->|"Delivery failed / Échec d’envoi"| L["Keep and retry delivery / Conserver et réessayer"]
    K --> M["Channel reusable / Salon réutilisable"]
    A -->|"Cancel before generation / Annuler avant génération"| K
```

## More diagrams / Autres diagrammes

- [[Room creation and adaptive polling / Création et polling adaptatif|Room-tracking]]
- [[Portals and token rotation / Portails et rotation des tokens|Web-portal]]
- [[YAML/APWorld/generation workflow|Archipelago-mode]]
- [[Room preparation, ZIP and cleanup / Préparation, ZIP et nettoyage|Room-preparation]]
- [[Authorization levels / Niveaux d’autorisation|Administration-and-security]]
- [[Storage and migration / Stockage et migration|Data-and-migrations]]
