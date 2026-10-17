# Présentation des portails AST

Les trois portails utilisent le thème commun `WebPortalTheme` : fond sombre uni, accents violets AST, bordures discrètes, formulaires compacts et navigation par section. Les liens de navigation ouvrent aussi les sections repliées. Sur petit écran, la navigation devient horizontale et les formulaires s'empilent.

Le portail joueur présente en priorité la progression de la room : nombre de slots, jeux distincts, checks et pourcentage global. Le tableau expose slot, jeu, progression des checks, compte de checks et dernière activité. « Tous les checks faits » ne désigne pas nécessairement le goal du jeu. Les filtres par jeu, progression et slots personnels, la recherche et le tri s'appliquent aux données déjà chargées. Les choix sont conservés à l'actualisation. Les slots personnels sont déduits des associations retournées dans le résumé autorisé du joueur.

Les récaps et hints viennent ensuite. Les associations, patchs, exclusions et outils spoiler sont regroupés dans des sections repliables accessibles depuis la navigation. La dernière synchronisation amont reste visible dans la barre d'actions.

L'administration présente les rooms disponibles dans un tableau avec recherche par nom, puis les formulaires existants sur deux colonnes lorsque la largeur le permet. La gestion de room reprend la même navigation et la même présentation des contrôles. Les routes, contrôles d'accès et actions existants sont conservés.

Les libellés ajoutés sont disponibles en français et anglais. Les noms de slots, jeux et rooms sont insérés comme texte. Les tableaux de slots se parcourent horizontalement sur écran étroit ; la page elle-même reste à la largeur disponible. Les styles sont intégrés au HTML généré, sans dépendance CSS ou JavaScript externe.

## Vérification

- Suite .NET : 298 tests réussis.
- Syntaxe JavaScript vérifiée sur les trois pages générées en FR et EN.
- Vérification dans le navigateur avec 13 slots de démonstration : recherche, filtres, tri, conservation des choix après actualisation et ouverture des sections depuis la navigation.
- Contrôle visuel du portail à 390 px, des pages de gestion et du champ de fréquence automatique/fixe.

Les captures et le serveur de démonstration utilisés pour cette vérification se trouvent dans `artifacts/portal-preview/`, ignoré par Git. Aucune action de production n'a été exécutée.
