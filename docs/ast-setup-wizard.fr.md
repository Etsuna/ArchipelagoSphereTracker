# PR 8 — Assistant Discord `/ast-setup`

## Problème

La création d'une room imposait de connaître les six paramètres de `/add-url`. Une erreur de canal, de type de thread ou de fréquence n'était visible qu'après l'envoi de la commande. L'URL privée pouvait aussi être reprise dans le message de succès historique.

## Architecture retenue

`/ast-setup` ouvre un assistant éphémère Discord. Une session en mémoire, liée à l'utilisateur, au serveur et au canal d'interaction, contient le brouillon pendant 15 minutes d'inactivité. Les composants portent uniquement un identifiant de session aléatoire et une action ; l'URL de room n'est jamais placée dans un `custom_id`, un aperçu ou un log.

Le parcours permet de :

1. choisir un canal texte du serveur avec un sélecteur natif ;
2. saisir l'URL WebHost et le nom du thread dans une modale ;
3. choisir un thread privé, public, ou public avec ajout des membres ;
4. choisir les notifications normales ou silencieuses ;
5. définir la fréquence minimale de polling ;
6. vérifier l'aperçu, puis confirmer ou annuler.

L'aperçu ne montre que le nom d'hôte validé. À la confirmation, AST retire la session pour empêcher un double clic, revalide le rôle `GuildManager`, le serveur et le canal cible, puis appelle le même service que `/add-url`. La validation SSRF/URL, la lecture des API publiques WebHost, la création du thread, l'écriture chiffrée et le démarrage du suivi restent donc centralisés. AST ne se connecte pas au protocole Archipelago.

L'association Discord ↔ slots et les niveaux de détail de notification ne sont pas simulés ici : ils nécessitent le modèle persistant de préférences prévu par les PR dédiées. L'assistant pourra les intégrer lorsque ces données existeront.

## Permissions et audit

- Démarrage et confirmation : rôle applicatif `GuildManager` obligatoire.
- Le contrôle est exécuté côté serveur ; l'état des boutons ne constitue pas une autorisation.
- Le canal sélectionné doit être un canal texte du même serveur et ne peut pas être un thread.
- Les tentatives refusées et les confirmations sont auditées comme `RoomAdd`, avec identifiant de corrélation et sans argument sensible.
- Les réponses de succès de l'ancien `/add-url` ne répètent plus l'URL privée complète.

## Fichiers touchés

- `src/Bot/AstSetupWizard.cs` : sessions, composants, modale, aperçu et confirmation.
- `src/Bot/BotCommands.cs` : enregistrement des interactions Discord.
- `src/Bot/SlashCommandDefinitions.cs` : définition de `/ast-setup`.
- `src/Bot/UrlClass.cs` : résultat structuré réutilisable et message sans secret.
- `src/Security/SecurityAuditLog.cs` : classification d'audit.
- `tests/ArchipelagoSphereTracker.Tests/AstSetupWizardTests.cs` et tests de définition/autorisation.
- `README.md` et cette documentation bilingue.

## Migration

Aucune migration de base n'est nécessaire. Les brouillons ne survivent volontairement pas à un redémarrage ; les rooms confirmées utilisent le stockage existant.

## Validation

```powershell
dotnet build ArchipelagoSphereTracker.sln --configuration Release
dotnet test tests\ArchipelagoSphereTracker.Tests\ArchipelagoSphereTracker.Tests.csproj --configuration Release
```

Les tests couvrent l'isolation par utilisateur/serveur/canal, l'expiration, l'invalidation d'une ancienne session, le retrait atomique à la confirmation, le masquage de l'identifiant privé, les composants et la classification d'audit.

## Risques et rollback

Les sessions sont locales à l'instance : un redémarrage oblige à relancer l'assistant. Une création Discord ou WebHost peut encore échouer après confirmation ; l'utilisateur reçoit alors un message générique ou la réponse contrôlée du service existant. Le rollback consiste à retirer la commande et ses trois gestionnaires d'interaction, puis à rétablir le type de retour interne de `UrlClass` ; aucune donnée ne doit être migrée ou supprimée.
