# Modèles de véhicules — licences

## muscle_blue (modèle actif)

- **Source :** « 5 Free Low-Poly Toy Cars (CC0) » — Popyca Studio
  https://github.com/Vuena/free-low-poly-toy-cars-cc0
- **Licence : CC0 1.0 Universal (domaine public).**
  Dédicace complète : voir `LICENSE-CC0.txt` dans ce dossier.
  Usage commercial autorisé, aucune attribution requise, modification libre.
- **Fichier d'origine :** `FBX/02_muscle/muscle_blue.fbx` (3 110 sommets, 6 664 triangles).
- **Conversion :** FBX → OBJ (carrosserie + 4 roues séparées), axes et unités
  conservés (1 unité = 1 mètre, Y-up, origine au sol, avant sur +Z).
  Réalisée avec la bibliothèque assimp, sans modification de la géométrie.
- **Textures :** `muscle_palette.png` (palette 16 couleurs, filtrage Point) et
  `muscle_mr.png` (metallic/roughness) sont les fichiers d'origine du pack.

### Comment changer de véhicule

Le pack contient 4 autres modèles (police, monster truck, camion de pompiers,
camion de glace). Pour en utiliser un autre :

1. copier ses fichiers dans `Resources/Vehicles/<nom>/`,
2. exporter la carrosserie et les 4 roues en OBJ séparés (nommés
   `muscle_body.obj`, `wheel_FL/FR/RL/RR.obj` ou adapter
   `VehicleModelLoader.ModelFolder`),
3. renseigner les positions des roues dans `VehicleModelLoader.wheelPositions`,
4. ajuster `VehicleModelFolder` si besoin.

### Repli sans modèle

Si les fichiers sont absents, `PlayerVehicle` bascule automatiquement sur le
châssis low-poly procédural (aucune erreur, le jeu reste jouable).
