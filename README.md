# ArchipelagoSphereTracker 
Un bot Discord conçu pour être lié a la room pour Archipelago (Exemple : https://archipelago.gg/room/trackerID).

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
* Lister le lien des Patchs.
* Récupérer le port de connexion.
* Suppression automatique du fil après 1 semaine d'inactivité.
* Envoyer des Yamls au server filtré par le channel.
* Envoyer des Apworld au server.
* Backup des Yamls envoyés au channel
* Backup des Apwrolds envoyés au channel
* Générer à partir du dossier du server un Multiworld.
* Générer à partir du server une fichier Zip contenant tous les Yamls compris dans le Zip.
* Lister les Yamls filtré par le channel.
* Lister les Appworlds présent dans le server.
* Gestion automatique de la compatibilité Windows et Linux pour la generation des Multiworld.

## Jeux Pris en Charge
Tous les jeux pris en charge par le Randomizer MultiWorld [Archipelago](https://github.com/ArchipelagoMW/Archipelago) sont compatibles et ont une compatibilité MultiWorld complète entre eux.

## Prérequis
```
dotnet-sdk-8.0
Fonctionne sous Linux et Windows
Python 3.13
python3-pip
python3-venv
```

## Configuration
Un fichier `.env` est nécessaire dans le répertoire principal du dépôt.

### Exemple de Configuration :
```
DISCORD_TOKEN=YOUR_DISCORD_BOT_TOKEN
APP_ID=YOUR_DISCORD_BOT_APP_ID
```

Si vous souhaitez créer votre propre bot Discord en utilisant le code de ce dépôt, votre bot aura besoin des permissions définies par l'entier `395137117248`.

Les permissions suivantes seront accordées à ArchipelagoSphereTracker :
* Voir les salons  
* Envoyer des messages
* Creer des fils publics
* Creer des fils privés  
* Envoyer des messages dans les threads  
* Gérer les messages
* Gérer les fils  
* Intégrer des liens  
* Joindre des fichiers  
* Ajouter des réactions  
* Lire l’historique des messages  

## Execution sans installation
```
Téléchargez la version Windows "ast-win-x64-vX.X.X.zip" ou Linux "ast-linux-x64-vX.X.X.tar.gz" depuis la page des releases.
Décompressez dans un dossier
Ajoutez dans la même répertoire le fichier .env correctement configuré
Executez le programme ArchipelagoSphereTracker.
```

## Installation avec Dotnet
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

# Lancez le bot
dotnet run
```
