# Quarantaine et validation des téléversements

Ce lot PR7 durcit les entrées YAML, APWorld, ZIP de génération et spoiler sans modifier les commandes ni les chemins de fichiers actifs.

## Flux appliqué

1. AST vérifie le nom simple, l'extension et la taille annoncée.
2. Le flux est copié avec une limite stricte dans `extern/upload-quarantine`, sous un nom GUID opaque terminé par `.quarantine`.
3. Le fichier fermé est validé selon son type.
4. Seul un fichier accepté est déplacé atomiquement vers sa destination. En cas de refus, dépassement, annulation ou erreur, le fichier de quarantaine est supprimé et l'ancien fichier actif reste intact.

Le démarrage et chaque nouveau téléversement suppriment au mieux les résidus expirés. Aucun nom fourni par l'utilisateur n'est utilisé dans la quarantaine.

## Contrôles par type

- YAML : texte UTF-8 non vide, sans octet nul.
- APWorld : archive ZIP lisible, au plus 500 entrées et 256 Mio décompressés, sans chemin absolu ni composant `..`.
- ZIP de génération : mêmes limites, uniquement des YAML à la racine de l'archive.
- Spoiler : nom `.txt` ou `.json`, texte UTF-8 non vide; le JSON doit avoir un objet ou un tableau à la racine. La nouvelle version n'efface l'ancienne qu'après validation.

Ces contrôles limitent les fichiers mal formés, les traversées de chemin et les bombes ZIP. Ils ne constituent ni une analyse antivirus ni une preuve que le code contenu dans un APWorld est digne de confiance; son installation reste réservée au propriétaire de l'instance.

## Configuration et exploitation

```dotenv
WEB_MAX_UPLOAD_BYTES=67108864
UPLOAD_QUARANTINE_RETENTION_MINUTES=60
```

L'intervalle accepté est de 5 à 1440 minutes. Le spoiler actif et les YAML n'expirent pas ; ils sont supprimés avec la room. Aucun changement de schéma SQLite n'est requis.

Pour revenir au comportement précédent, restaurer les appels directs de copie dans les quatre gestionnaires de fichiers et retirer le nettoyage de quarantaine au démarrage.

## Vérification

```bash
dotnet test tests/ArchipelagoSphereTracker.Tests/ArchipelagoSphereTracker.Tests.csproj -c Release
dotnet build ArchipelagoSphereTracker.sln -c Release
```
