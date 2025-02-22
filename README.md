# ArchipelagoSphereTracker 
Un bot Discord conçu pour être lié au sphere_tracker pour Archipelago (Exemple : https://archipelago.gg/sphere_tracker/trackerID).

## Fonctionnalités Actuelles
* Multi Discord et Multi Channel
* Ajouter une URL (Droits d'admin requis)
* Supprimer une URL (Droits d'admin requis)
* Récupérer tous les noms depuis le tracker
* Définir un alias (remplace le nom par celui sur Discord)
* Supprimer son propre alias (Propriétaire et Admin requis)
* Récapituler la table de loot des objets depuis le dernier récapitulatif et nettoyage (uniquement si un alias a été créé).
* Récapituler et nettoyer la table de loot des objets (uniquement si un alias a été créé).
* Envoyer automatiquement des messages concernant les nouveaux objets lootés sur Discord (avec le tag Discord, uniquement si un alias a été créé).
* Envoyer automatiquement un message quand un joueur complète son objectif.
* Lister les items reçus par le nom du joueur (avec l'option d'affichage en retour à la ligne pour chaque item ou séparés par une virgule).
* Lister les hints par receivers ou par finders.

## Jeux Pris en Charge
Tous les jeux pris en charge par le Randomizer Multi-Monde [Archipelago](https://github.com/ArchipelagoMW/Archipelago) sont compatibles et ont une compatibilité MultiWorld complète entre eux.

## Prérequis
```
dotnet-sdk-8.0
Fonctionne sous Linux et Windows
```

## Configuration
Un fichier `.env` est nécessaire dans le répertoire principal du dépôt.

### Exemple de Configuration :
```
DISCORD_TOKEN=YOUR_DISCORD_BOT_TOKEN
APP_ID=YOUR_DISCORD_BOT_APP_ID
```

Si vous souhaitez créer votre propre bot Discord en utilisant le code de ce dépôt, votre bot aura besoin des permissions définies par l'entier `274878032960`.

Les permissions suivantes seront accordées à ArchipelagoSphereTracker :
* Voir les salons  
* Envoyer des messages  
* Envoyer des messages dans les threads  
* Gérer les messages  
* Intégrer des liens  
* Joindre des fichiers  
* Ajouter des réactions  
* Lire l’historique des messages  

## Installation
```
# Clonez le dépôt
git clone https://github.com/Etsuna/ArchipelagoSphereTracker.git

# Entrez dans le répertoire
cd ArchipelagoSphereTracker

# Configurez votre fichier .env
vim .env

# Restaurez le projet
dotnet restore

# Compilez le projet
dotnet build

# Lancez le projet
dotnet run
```
