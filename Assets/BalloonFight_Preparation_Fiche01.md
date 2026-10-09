# Balloon Fight — préparation avant `BF-S02-B01-F01`

Ce package contient uniquement les éléments fournis avant la première fiche :

- la scène `Assets/Scenes/BalloonFight_Offline.unity` ;
- le prefab visuel `Assets/Prefabs/BalloonFighter.prefab` ;
- les maps `BalloonP1` et `BalloonP2` dans `Assets/InputSystem_Actions.inputactions` ;
- `SimVector2`, son assembly runtime et ses trois tests EditMode ;
- l'assembly `Engine`, prêt à recevoir les scripts Unity des fiches suivantes.

Après l'import, ouvrir la scène et vérifier que la Console ne contient aucune erreur rouge. Dans **Edit > Project Settings > Player**, activer **Run In Background**. Les trois tests `SimVector2Tests` doivent être verts.

Les fichiers créés pendant `BF-S02-B01-F01` ne sont pas inclus.
