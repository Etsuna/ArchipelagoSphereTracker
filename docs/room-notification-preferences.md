# Notifications de room / Room notification preferences

## Français

Dans `/ast`, sélectionnez la room puis **Gérer → Plus / Paramètres avancés**.
Le portail de gestion de la room propose les mêmes réglages :

- **Joueurs annoncés** : tous les joueurs ou uniquement les alias associés.
- **Catégories d'objets** : tous les objets, utiles + progression, ou progression
  uniquement. Ce filtre s'applique aux objets reçus et aux objets des indices.
  Les objets combinant les indicateurs utile et progression restent des objets
  de progression.
- **Notifications d'indices** : activation indépendante des nouveaux indices et
  de leurs mises à jour. Les deux sont activés ou désactivés ensemble.

Les gestionnaires de room peuvent modifier ces paramètres. Un filtre personnel
peut restreindre davantage les notifications d'objets d'un alias ; il ne permet
jamais de contourner le filtre global. En mode alias associés uniquement, un
indice doit concerner au moins un joueur associé pour être annoncé. Les
notifications d'objectif suivent le choix des joueurs, indépendamment du filtre
d'objets et du réglage des indices.

Le suivi, l'inventaire, les indices et les récapitulatifs restent complets.
Ces paramètres modifient les annonces Discord, pas les données conservées ni
le tracker Web Archipelago. Ils s'appliquent aux nouveaux lots de notifications ;
les lots déjà préparés gardent leurs réglages pour permettre une reprise fiable
des envois après une interruption.

La migration **5.0.20** est automatique et conserve les comportements existants :
un ancien mode normal devient « tous / tous / indices activés », et un ancien
mode silencieux devient « alias associés / tous / indices désactivés ».
Les réglages deviennent indépendants dès leur modification dans la nouvelle
interface. L'ancienne commande `update-silent-option` reste compatible ; après
configuration explicite, elle ne modifie que le choix des joueurs annoncés.

## English (US)

Open `/ast`, select the room, then **Manage → More / Advanced settings**.
The room management portal provides the same controls:

- **Players announced**: all players or linked aliases only.
- **Item categories**: all items, useful + progression, or progression only.
  Applies to received items and the items in hint notifications. Items carrying
  both useful and progression flags still count as progression items.
- **Hint notifications**: independently enable or disable new and updated hints
  together.

Only room managers can change these settings. Personal alias item filters can
narrow the global rules further and cannot override them. With linked aliases
only, a hint must involve at least one linked participant to be announced.
Goal notifications follow the player selection, independently of item categories
and hint notifications.

Tracking data, inventory, hints, and recaps remain complete. These controls affect
Discord announcements, not stored data or the Archipelago web tracker. Changes
apply to newly prepared notification batches; existing batches retain their
settings for reliable recovery of interrupted sends.

Automatic database migration **5.0.20** preserves existing behavior: normal mode
starts as “all players / all items / hints on”; Silent mode starts as “linked
aliases / all items / hints off”. The new interface makes these settings
independent. The legacy `update-silent-option` command remains compatible; after
explicit configuration, it changes only the player selection.
