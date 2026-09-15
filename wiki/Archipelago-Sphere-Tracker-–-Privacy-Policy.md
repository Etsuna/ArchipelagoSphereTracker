<details open>
<summary>🇬🇧 English</summary>
# Archipelago Sphere Tracker – Privacy Policy (EN)

_Last updated: 2026-09-07_

This Privacy Policy explains how **Archipelago Sphere Tracker** (the “Service”) collects, uses, and stores information when you interact with the Discord bot, related tools, and documentation.

The Service is operated by the individual developer(s) of the open‑source project available at:  
https://github.com/Etsuna/ArchipelagoSphereTracker

By using the Service, you agree to the practices described in this Privacy Policy. If you do not agree, you should stop using the Service and remove the bot from your Discord server.

> This document is a template and does not constitute legal advice. You should adapt it to your situation and, if possible, have it reviewed by a legal professional.

---

## 1. Data Controller

For the purposes of applicable data protection laws (including the GDPR), the data controller is:

- The maintainer(s) of the **Archipelago Sphere Tracker** project (“we”, “us”).  
- Contact:  
  - GitHub Issues: https://github.com/Etsuna/ArchipelagoSphereTracker/issues
  - Discord: [AST](https://discord.gg/PJfWRKVyEW)


The Service is hobby, non‑commercial software maintained on a best‑effort basis.

---

## 2. Data We Collect

When you or your Discord server use the Service, we may process the following categories of data:

1. **Discord identifiers and metadata**
   - User IDs, usernames, nicknames
   - Server (guild) IDs, server names
   - Channel IDs and, where required, channel names
   - Role IDs or similar metadata if needed for permissions

2. **Interaction data**
   - Commands you send to the bot (including slash commands)
   - Relevant parts of messages that are required to process a command
   - Configuration data (for example, server‑specific settings for the bot)

3. **Archipelago‑related data**
   - Room IDs or connection identifiers
   - Tracker state and game‑related metadata required for the bot to function
   - Technical information necessary to synchronize tracker data with Archipelago

4. **Technical logs**
   - Timestamps of interactions
   - Error messages and stack traces
   - Basic runtime information required to diagnose issues and prevent abuse

5. **Persistent bot data and uploaded files**
   - Discord-to-slot associations, mention preferences and personal exclusions
   - Received items, hints, game status, recaps and room polling state
   - Delegated AST manager assignments and security audit events
   - Portal-token hashes and expiration dates (the clear portal token is not stored)
   - Room-related YAML, spoiler, generated archives, and APWorld files where those features are used

The Service **does not intentionally collect** real names, postal addresses, payment information, or other special categories of personal data. Any such data that you voluntarily share in Discord messages is your responsibility.

---

## 3. Legal Bases for Processing (EU / France)

Where EU or French data protection law applies (including the GDPR), we rely on the following legal bases:

1. **Performance of a contract or pre‑contract steps**  
   To provide the functionalities of the bot that you request (e.g. processing commands, maintaining tracker state).

2. **Legitimate interests**  
   To maintain and improve the Service, prevent abuse, and ensure security and stability. We consider these interests not overridden by your rights and freedoms.

3. **Consent**  
   In cases where your explicit consent is required by law (for example, if additional optional features are introduced in the future), we will ask for it separately via the relevant interface.

---

## 4. How We Use the Data

We use the collected data for the following purposes:

1. **To operate the Service**
   - Process commands and update tracker information.
   - Display information in Discord (embeds, messages, status updates, etc.).

2. **To maintain and improve the Service**
   - Debug and fix errors.
   - Monitor stability and performance.
   - Adjust features or configuration to handle abuse (e.g. spam, misuse).

3. **To ensure security and prevent abuse**
   - Detect and mitigate hostile or automated behavior that could overload the Service.
   - Enforce rate limits or block specific servers/users if necessary.

We do **not** use your data for advertising or commercial profiling.

---

## 5. Data Sharing

We may share or disclose data in the following limited situations:

1. **With infrastructure providers**  
   If hosting or logging services are used (for example, a VPS provider or database host), they may technically have access to stored logs or runtime data as part of their service. Such providers are bound by their own terms and privacy policies.

2. **With open‑source contributors**  
   If you open an issue on GitHub and include logs or screenshots, those will be publicly available according to GitHub’s terms. Do not include sensitive data in public issues.

3. **When required by law**  
   We may disclose data if we are legally required to do so (for example, in response to a lawful request by public authorities).

We do not sell or rent your data to third parties.

---

## 6. Data Retention

1. **Ephemeral Discord interface sessions** are stored in memory and expire after 15 minutes of inactivity.
2. **Tracked-room data** remains in the local SQLite database while the room is configured. For tracked Discord threads, AST warns after 7 days without a newly received item and automatically removes that room's AST data and local files after 14 days. A room manager or the instance owner may also remove it earlier.
3. **Portal tokens** issued by current versions expire after the configured `PORTAL_TOKEN_LIFETIME_DAYS` period (30 days by default) and may be rotated or revoked at any time.
4. **Security audit events** are retained according to `AUDIT_RETENTION_DAYS` (90 days by default).
5. **Interrupted upload quarantine files** are removed according to `UPLOAD_QUARANTINE_RETENTION_MINUTES` (60 minutes by default). Active room spoilers, YAML files, and generated downloads have no separate fixed expiry and are removed with the room. Global custom worlds require manual removal.
6. **Console or infrastructure logs** may be retained by the instance operator or hosting provider for a limited period needed for debugging, security, and stability.
7. We may delete some or all stored data at any time in the course of maintenance, without prior notice.

Configuration values may differ between self-hosted instances. We aim not to keep unnecessary data longer than needed.

---

## 7. International Transfers

Depending on the hosting provider used, data may be processed in countries outside of your own, and possibly outside the European Economic Area (EEA). In such cases, we aim to rely on providers that offer appropriate safeguards (for example, standard contractual clauses or equivalent mechanisms).

However, as a non‑commercial hobby project, the infrastructure may change over time. If you are concerned about international transfers, you may choose not to use the Service.

---

## 8. Your Rights (GDPR / EU / France)

Where the GDPR or similar laws apply, you may have the following rights regarding your personal data, subject to certain conditions and limitations:

1. **Right of access** – to obtain confirmation whether we process data about you and, if so, to receive a copy.
2. **Right to rectification** – to have inaccurate data corrected.
3. **Right to erasure** – to request deletion of your data where there is no compelling reason for us to keep it.
4. **Right to restriction of processing** – to request limitation of how we use your data.
5. **Right to object** – to object to processing based on legitimate interests.
6. **Right to data portability** – to receive your data in a commonly used format when technically feasible.

To exercise any of these rights, you can contact us via:

- GitHub Issues: https://github.com/Etsuna/ArchipelagoSphereTracker/issues
- Discord: [AST](https://discord.gg/PJfWRKVyEW)

Because we mainly process Discord identifiers and technical logs, in practice this may mean blocking or removing those identifiers from our logs where technically feasible.

You also have the right to lodge a complaint with a supervisory authority. In France, this is the **CNIL** (Commission Nationale de l’Informatique et des Libertés).

---

## 9. Children

The Service is intended for users who are allowed to use Discord under Discord’s own Terms of Service. We do not knowingly collect personal data from children who are not permitted to use Discord. If you believe we have collected information about a minor in violation of applicable law, please contact us so we can delete it where appropriate.

---

## 10. Third‑Party Services

Your data is also processed by third‑party services you use together with the bot, including but not limited to:

- **Discord** (messages, user IDs, servers, channels, roles, etc.)  
- **Archipelago** (game rooms, multiworld data, etc.)  
- **GitHub** (issues, pull requests, comments)

These services have their own terms and privacy policies. We are not responsible for their practices.

---

## 11. Security

We implement reasonable technical and organizational measures to protect data against accidental or unlawful destruction, loss, alteration, or unauthorized access. However, no system is completely secure, and we cannot guarantee absolute security.

Portal tokens are randomly generated, stored only as SHA-256 hashes, and revalidated against current Discord access. Sensitive actions are authorized server-side and audited without their command arguments. Room IDs, tracker IDs, and patch links are intentionally stored in plaintext in the local SQLite database; instance operators are responsible for restricting access to that database and its backups. The Discord bot token remains an environment secret and is not copied into SQLite.

As a user, you should also avoid sharing unnecessary personal information via bot commands or in public channels.

---

## 12. Changes to This Privacy Policy

We may update this Privacy Policy from time to time. When we do, we will update the “Last updated” date at the top of this document. Material changes may also be announced via GitHub or Discord where appropriate.

If you continue to use the Service after changes are published, you are deemed to accept the updated Privacy Policy.

---

## 13. Contact

If you have any questions, requests, or concerns about this Privacy Policy or our data practices, you can contact us at:

- GitHub Issues: https://github.com/Etsuna/ArchipelagoSphereTracker/issues
- Discord: [AST](https://discord.gg/PJfWRKVyEW)

---
</details>

<details>
<summary>🇫🇷 Français</summary>


# Archipelago Sphere Tracker – Politique de confidentialité (FR)

_Dernière mise à jour : 07/09/2026_

La présente Politique de confidentialité explique comment **Archipelago Sphere Tracker** (le « Service ») collecte, utilise et conserve les informations lorsque vous interagissez avec le bot Discord, les outils associés et la documentation.

Le Service est exploité par le ou les développeurs du projet open source disponible à l’adresse :  
https://github.com/Etsuna/ArchipelagoSphereTracker

En utilisant le Service, vous acceptez les pratiques décrites dans la présente Politique de confidentialité. Si vous n’êtes pas d’accord, vous devez cesser d’utiliser le Service et retirer le bot de votre serveur Discord.

> Ce document est un modèle et ne constitue pas un conseil juridique. Vous devez l’adapter à votre situation et, si possible, le faire relire par un professionnel du droit.

---

## 1. Responsable du traitement

Au sens des lois applicables en matière de protection des données (y compris le RGPD), le responsable du traitement est :

- Le ou les mainteneurs du projet **Archipelago Sphere Tracker** (« nous »).  
- Contact :  
  - GitHub Issues: https://github.com/Etsuna/ArchipelagoSphereTracker/issues
  - Discord: [AST](https://discord.gg/PJfWRKVyEW)

Le Service est un logiciel non commercial, maintenu à titre hobby et sur la base du meilleur effort.

---

## 2. Données collectées

Lorsque vous ou votre serveur Discord utilisez le Service, nous pouvons traiter les catégories de données suivantes :

1. **Identifiants et métadonnées Discord**
   - Identifiants d’utilisateurs (user IDs), pseudonymes, surnoms
   - Identifiants de serveurs (guild IDs), noms de serveurs
   - Identifiants de salons (channel IDs) et, le cas échéant, noms de salons
   - Identifiants de rôles ou métadonnées similaires si nécessaires pour la gestion des permissions

2. **Données d’interaction**
   - Commandes envoyées au bot (y compris slash commands)
   - Parties pertinentes des messages nécessaires au traitement d’une commande
   - Données de configuration (par exemple, paramètres propres à un serveur pour le bot)

3. **Données liées à Archipelago**
   - Identifiants de salons ou de connexions
   - États de tracker et métadonnées de partie nécessaires au fonctionnement du bot
   - Informations techniques permettant de synchroniser les données du tracker avec Archipelago

4. **Journaux techniques**
   - Horodatage des interactions
   - Messages d’erreur et traces techniques
   - Informations de fonctionnement nécessaires au diagnostic des problèmes et à la prévention des abus

5. **Données persistantes et fichiers téléversés**
   - Associations Discord ↔ slots, préférences de mention et exclusions personnelles
   - Objets reçus, hints, statuts de jeu, récaps et état de polling des rooms
   - Attributions de responsables AST délégués et événements d’audit de sécurité
   - Hashes et dates d’expiration des tokens de portail (le token en clair n’est pas stocké)
   - YAML, spoilers, archives générées et APWorld liés aux rooms lorsque ces fonctions sont utilisées

Le Service **ne collecte pas intentionnellement** de nom réel, adresse postale, information de paiement ou autres catégories particulières de données personnelles. Toute information de ce type partagée volontairement dans des messages Discord relève de votre responsabilité.

---

## 3. Bases juridiques (UE / France)

Lorsque le droit de l’Union européenne ou le droit français s’applique (y compris le RGPD), nous nous appuyons sur les bases juridiques suivantes :

1. **Exécution d’un contrat ou mesures précontractuelles**  
   Pour fournir les fonctionnalités du bot que vous sollicitez (ex. traitement de commandes, maintien de l’état du tracker).

2. **Intérêts légitimes**  
   Pour maintenir et améliorer le Service, prévenir les abus et assurer la sécurité et la stabilité. Nous estimons que ces intérêts ne sont pas supplantés par vos droits et libertés.

3. **Consentement**  
   Dans les cas où votre consentement explicite est requis par la loi (par exemple si des fonctionnalités optionnelles supplémentaires sont introduites à l’avenir), celui‑ci sera demandé séparément via l’interface concernée.

---

## 4. Utilisation des données

Nous utilisons les données collectées pour les finalités suivantes :

1. **Fonctionnement du Service**
   - Traiter les commandes et mettre à jour les informations du tracker.
   - Afficher des informations dans Discord (embeds, messages, mises à jour de statut, etc.).

2. **Maintenance et amélioration du Service**
   - Déboguer et corriger les erreurs.
   - Surveiller la stabilité et les performances.
   - Adapter certaines fonctionnalités ou configurations pour gérer les abus (spam, mauvaise utilisation, etc.).

3. **Sécurité et prévention des abus**
   - Détecter et atténuer les comportements hostiles ou automatisés susceptibles de surcharger le Service.
   - Appliquer des limites de fréquence ou bloquer certains serveurs/utilisateurs si nécessaire.

Nous **n’utilisons pas** vos données à des fins publicitaires ni de profilage commercial.

---

## 5. Partage des données

Nous pouvons partager ou divulguer des données dans les cas limités suivants :

1. **Avec des prestataires d’infrastructure**  
   Si des services d’hébergement ou de journalisation sont utilisés (par exemple un fournisseur de VPS ou de base de données), ceux‑ci peuvent techniquement avoir accès aux journaux ou aux données de fonctionnement dans le cadre de leur prestation. Ces prestataires sont soumis à leurs propres conditions et politiques de confidentialité.

2. **Avec la communauté open source**  
   Si vous ouvrez un ticket (issue) sur GitHub et y joignez des journaux ou captures d’écran, ces informations seront publiques conformément aux conditions de GitHub. Évitez d’y inclure des données sensibles.

3. **Obligations légales**  
   Nous pouvons divulguer des données si la loi nous y oblige (par exemple, en réponse à une demande légale émanant d’une autorité publique).

Nous ne vendons ni ne louons vos données à des tiers.

---

## 6. Durée de conservation

1. **Sessions éphémères de l’interface Discord** : stockées en mémoire et expirant après 15 minutes d’inactivité.
2. **Données de room** : conservées dans la base SQLite locale tant que la room est configurée. Pour les threads Discord suivis, AST avertit après 7 jours sans nouvel objet reçu et supprime automatiquement les données et fichiers AST de cette room après 14 jours. Un gestionnaire de room ou le propriétaire d’instance peut aussi les supprimer plus tôt.
3. **Tokens de portail** : les liens émis par les versions actuelles expirent selon `PORTAL_TOKEN_LIFETIME_DAYS` (30 jours par défaut) et peuvent être renouvelés ou révoqués à tout moment.
4. **Événements d’audit de sécurité** : conservés selon `AUDIT_RETENTION_DAYS` (90 jours par défaut).
5. **Fichiers d’upload interrompu en quarantaine** : supprimés selon `UPLOAD_QUARANTINE_RETENTION_MINUTES` (60 minutes par défaut). Les spoilers actifs, YAML et téléchargements générés n’ont pas d’expiration fixe séparée et sont supprimés avec la room. Les custom worlds globaux doivent être retirés manuellement.
6. **Logs console ou d’infrastructure** : peuvent être conservés par l’exploitant de l’instance ou l’hébergeur pendant une durée limitée nécessaire au diagnostic, à la sécurité et à la stabilité.
7. Nous pouvons supprimer tout ou partie des données stockées à tout moment dans le cadre de la maintenance, sans préavis.

Les valeurs de configuration peuvent différer entre les instances auto-hébergées. Nous visons à ne pas conserver de données inutiles plus longtemps que nécessaire.

---

## 7. Transferts internationaux

Selon le prestataire d’hébergement utilisé, les données peuvent être traitées dans des pays autres que le vôtre, y compris en dehors de l’Espace économique européen (EEE). Dans la mesure du possible, nous privilégions des prestataires offrant des garanties appropriées (par exemple, clauses contractuelles types ou mécanismes équivalents).

Cependant, en tant que projet non commercial, l’infrastructure peut évoluer avec le temps. Si vous êtes préoccupé par ces transferts, vous pouvez choisir de ne pas utiliser le Service.

---

## 8. Vos droits (RGPD / UE / France)

Lorsque le RGPD ou des lois similaires s’appliquent, vous pouvez bénéficier des droits suivants, sous réserve de certaines conditions et limitations :

1. **Droit d’accès** – obtenir la confirmation que nous traitons des données vous concernant et, le cas échéant, en recevoir une copie.
2. **Droit de rectification** – faire corriger des données inexactes.
3. **Droit à l’effacement** – demander la suppression de vos données lorsqu’il n’existe plus de raison impérieuse de les conserver.
4. **Droit à la limitation du traitement** – demander la limitation de l’utilisation de vos données.
5. **Droit d’opposition** – vous opposer au traitement fondé sur nos intérêts légitimes.
6. **Droit à la portabilité** – recevoir vos données dans un format couramment utilisé lorsque cela est techniquement possible.

Pour exercer ces droits, vous pouvez nous contacter via :

- GitHub Issues: https://github.com/Etsuna/ArchipelagoSphereTracker/issues
- Discord: [AST](https://discord.gg/PJfWRKVyEW)

En pratique, compte tenu du type de données (identifiants Discord et journaux techniques), cela pourra notamment consister à bloquer ou supprimer ces identifiants de nos journaux lorsque c’est techniquement possible.

Vous disposez également du droit d’introduire une réclamation auprès d’une autorité de contrôle. En France, il s’agit de la **CNIL** (Commission Nationale de l’Informatique et des Libertés).

---

## 9. Mineurs

Le Service s’adresse aux utilisateurs autorisés à utiliser Discord conformément aux Conditions d’utilisation de Discord. Nous ne collectons pas sciemment de données personnelles auprès de mineurs qui ne seraient pas autorisés à utiliser Discord. Si vous pensez que nous avons collecté des informations concernant un mineur en violation du droit applicable, contactez‑nous afin que nous puissions les supprimer le cas échéant.

---

## 10. Services tiers

Vos données sont également traitées par les services tiers que vous utilisez conjointement avec le bot, notamment :

- **Discord** (messages, identifiants d’utilisateurs, serveurs, salons, rôles, etc.)  
- **Archipelago** (salons de jeu, données multiworld, etc.)  
- **GitHub** (issues, pull requests, commentaires)

Ces services disposent de leurs propres conditions et politiques de confidentialité. Nous ne sommes pas responsables de leurs pratiques.

---

## 11. Sécurité

Nous mettons en œuvre des mesures techniques et organisationnelles raisonnables pour protéger les données contre la destruction, la perte, l’altération ou l’accès non autorisé, accidentels ou illicites. Toutefois, aucun système n’est totalement sécurisé et nous ne pouvons garantir une sécurité absolue.

Les tokens de portail sont générés aléatoirement, conservés uniquement sous forme de hash SHA-256 et revérifiés avec les accès Discord actuels. Les actions sensibles sont autorisées côté serveur et auditées sans leurs arguments. Les identifiants de room, de tracker et les liens de patch sont volontairement stockés en clair dans la base SQLite locale ; l’exploitant de l’instance doit limiter l’accès à cette base et à ses sauvegardes. Le token du bot Discord reste un secret d’environnement et n’est pas copié dans SQLite.

En tant qu’utilisateur, vous devez également éviter de partager des informations personnelles inutiles via les commandes du bot ou dans des salons publics.

---

## 12. Modifications de la présente Politique de confidentialité

Nous pouvons mettre à jour la présente Politique de confidentialité de temps à autre. En cas de modification, la date de « Dernière mise à jour » en haut du document sera ajustée. Les changements importants pourront également être annoncés via GitHub ou Discord lorsqu’approprié.

Si vous continuez à utiliser le Service après la publication de modifications, vous êtes réputé accepter la Politique de confidentialité ainsi mise à jour.

---

## 13. Contact

Pour toute question, demande ou préoccupation concernant la présente Politique de confidentialité ou nos pratiques en matière de données, vous pouvez nous contacter :

- GitHub Issues: https://github.com/Etsuna/ArchipelagoSphereTracker/issues
- Discord: [AST](https://discord.gg/PJfWRKVyEW)
</details>
