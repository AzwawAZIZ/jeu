# AETHER DRIFT : SHADOWS OF THE CROWN

> Jeu Unity C# — véhicule runique, drift arcade, monde ouvert low-poly, boss mécaniques.

**Pitch :** Tu incarnes un pilote-mercenaire dans un royaume médiéval-fantastique déchu, au volant d'un bolide runique capable de drifter et d'escalader les ruines. Explore librement une vallée ouverte (façon *Elden Ring*), accomplis des contrats de livraison / assassinat motorisé (façon *GTA* / *Assassin's Creed*), et affronte des « Seigneurs Mécaniques » géants en arène ouverte grâce à la vitesse, au drift et aux lames énergétiques.

---

## 🎯 Objectifs du projet

- **Game feel immédiat :** drift arcade + jauge d'endurance/boost qui sert à la fois à drifter et à déclencher un dash d'invulnérabilité (façon esquive *Elden Ring*).
- **Zéro barrière d'assets :** style **Low-Poly / Cel-Shading** (façon *Sable* / *Art of Rally*) généré procéduralement ou avec des primitives géométriques simples — tout le rendu visuel est créé par code C#, sans artiste 3D.
- **Boucle courte et rejouable :** sessions de 15 à 20 minutes, chaque boss vaincu débloque une amélioration permanente (grappin façon *Assassin's Creed*, turbo runique, lames de drift).

---

## 🚀 Étape 1 — Ce qui est fait (cette branche)

Cette étape initialise le projet et génère **tout le contenu de la scène par code** : la scène ne contient qu'un GameObject `Bootstrapper` (créé par un menu dédié), et tout le reste apparaît au ▶ Play.

| Élément | Fichier | Rôle |
|---|---|---|
| **Bootstrapper** | `Assets/_Project/Scripts/Core/GameBootstrapper.cs` | Au `Awake()`, génère le sol, le véhicule, la caméra et la lumière via `GameObject.CreatePrimitive`. |
| **Caméra fluide** | `Assets/_Project/Scripts/Player/CameraFollow.cs` | Orbite libre à 360° (souris), recentrage automatique, anti-traversée de décor, zoom et FOV dynamiques. |
| **Véhicule (base)** | `Assets/_Project/Scripts/Player/PlayerVehicle.cs` | Sphère physique invisible (`Rigidbody` + `SphereCollider`) + châssis low-poly détaché (il suit la sphère et s'oriente vers la trajectoire). |
| **Modèle 3D** | `Assets/_Project/Resources/Vehicles/` | Bolide low-poly CC0 (Popyca Studio), 4 roues séparées qui tournent et braquent. Repli automatique sur le châssis procédural si absent. |
| **Conduite** | `Assets/_Project/Scripts/Player/CarController.cs` | Physique *sphere car* (`AddForce`), direction en lacet piloté, adhérence/drift, boost d'éther, dash d'invincibilité, trails procéduraux. |
| **Ressources** | `Assets/_Project/Scripts/Player/PlayerStats.cs` | Jauges d'éther et d'endurance, régénération, i-frames, événements pour le HUD. |
| **Sol / arène** | généré par le Bootstrapper | Grand plan sombre + ruines décoratives (piliers) pour donner l'échelle de la vallée. |
| **Lumière** | générée par le Bootstrapper | Lumière directionnelle avec ombres douces + brouillard d'ambiance. |
| **Outil éditeur** | `Assets/_Project/Editor/BootSceneCreator.cs` | Menu `Aether Drift ▸ Créer la scène de boot` : crée et sauvegarde la scène en un clic. |

### Arborescence

```
Assets/
└── _Project/
    ├── Editor/    → BootSceneCreator.cs (menu de création de scène)
    ├── Scenes/    → Boot.unity (créée via le menu)
    └── Scripts/
        ├── Core/      → GameBootstrapper.cs, LowPolyMesh.cs, MeshUtils.cs, ProceduralMaterials.cs
        ├── Player/    → CameraFollow.cs, PlayerVehicle.cs, CarController.cs, PlayerStats.cs…
        ├── World/     → WorldGenerator.cs, SyncTower.cs, ContractManager.cs, DescentHelper.cs,
        │                ArenaEnvironment.cs
        ├── Enemies/   → ChaserAI.cs (Étape 3) — Seigneurs Mécaniques à l'Étape 4
        └── UI/        → (Étape 5 : HUD, menus, jauge boost/drift)
```

---

## 🛠️ Comment ouvrir le projet (Unity)

> 📖 **Débutant complet ?** Suis le guide illustré pas-à-pas : **[docs/ETAPE1-GUIDE.md](docs/ETAPE1-GUIDE.md)** (installation Unity, création de la scène, lancement).
>
> 🌍 **Nouveau : le monde ouvert de l'Étape 3** est expliqué dans **[docs/ETAPE3-GUIDE.md](docs/ETAPE3-GUIDE.md)** — comment monter une tour, remplir un contrat, semer les chasseurs, et régler le monde sans écrire une ligne de code.

1. **Unity version recommandée :** 2022.3 LTS (ou Unity 6). Le code est compatible Built-in et URP (le Bootstrapper choisit automatiquement le shader `Universal Render Pipeline/Lit` si disponible, sinon `Standard`).
2. **Ouvre le dossier** du dépôt dans Unity Hub → *Add* → sélectionne la racine du repo (celle qui contient `Assets/`, `ProjectSettings/`, `Packages/`).
3. **Crée la scène en un clic :** menu **`Aether Drift ▸ Créer la scène de boot (Étape 1)`**.
   *(Alternative manuelle : `File → New Scene → Empty`, GameObject vide nommé `Bootstrapper`, glisser `GameBootstrapper.cs` dessus, sauvegarder sous `Assets/_Project/Scenes/Boot.unity`.)*
4. **Appuie sur ▶ Play.** Tout est généré automatiquement :
   - un grand sol sombre,
   - le véhicule runique au centre (sphère invisible + châssis + lames cyan lumineuses),
   - la caméra qui suit le véhicule,
   - une lumière directionnelle avec ombres.
> 💡 **Le fichier `Boot.unity` n'est pas dans le dépôt** : c'est volontaire. Le Bootstrapper est la source de vérité — le contenu se régénère à chaque Play, et la scène reste réduite à un seul GameObject (donc sans conflit Git). Les fichiers `.meta` sont générés par Unity à la première ouverture, puis versionnés.

## 🌍 Étape 3 — Le monde ouvert (cette section)

Le monde n'est plus une arène de 220 m : c'est une **carte de 1 200 m × 1 200 m**
(1,2 km de côté), générée entièrement par code au ▶ Play.

### Les trois zones

| Zone | Où | Contenu | À quoi ça sert |
|---|---|---|---|
| **1. Cœur des ruines** | Centre, rayon ≈ 180 m | 260 structures : piliers à tambours (certains brisés), arches franchissables, murets bas, amas de blocs | L'arène de départ, dense mais toujours praticable |
| **2. Terres de Lancement** | Nord (Z ≈ 450) | 34 rampes de saut (dont 35 % de grandes), buttes de terre | Sauts, vitesse, figures |
| **3. Chemins anciens** | Anneau + 4 rayons | Routes plates bordées de **bornes lumineuses cyan** | Repères de navigation à 150 km/h |

Tout est **fusionné en un seul maillage par zone** : 4-5 draw calls pour un
kilomètre de décor, au lieu de plusieurs centaines d'objets séparés.

### Les tours de synchronisation (ADN *Assassin's Creed*)

Trois grandes tours lumineuses (42 à 50 m) sont réparties autour du cœur des
ruines, à 45°, 165° et 285° — jamais sur un chemin.

1. **Monte** la rampe en spirale (3,5 tours, 12 à 14° de pente — vérifié au
   calcul : à 2,5 tours la pente montait à 16° et la voiture patinait).
2. **Atteins le sommet** : la tour passe du bleu dormant à l'**or runique**,
   trois anneaux lumineux s'élèvent, et la caméra tremble.
3. **Les icônes de boss et de contrats sont révélées** sur la carte
   (la mini-map elle-même arrive à l'Étape 5 ; la Console confirme la progression).
4. **Appuie sur `R`** au sommet pour une **descente guidée** : la voiture glisse
   jusqu'au pied de la tour, puis le contrôle est rendu. Pas de téléportation :
   tu vois la descente.

> 🛠️ `R` ne fonctionne **que depuis le sommet** : ailleurs, il ne fait rien.

### Les contrats de livraison (ADN *GTA*)

Trois balises **cyan** sont posées dans le monde. Traverse-en une :

- un **chronomètre** démarre (15 s de marge + trajet à 43 km/h de moyenne :
  37 s pour 260 m, 55 s pour 480 m — de quoi rater un virage sans perdre),
- la balise de destination s'allume en **or**,
- **deux chasseurs** apparaissent derrière toi et tentent de te percuter.

Les chasseurs (coque en coin, œil rouge) poursuivent, chargent quand ils sont
alignés, puis récupèrent après une charge ratée — cette récupération est ta
fenêtre pour t'échapper. Une **esquive `Shift` au bon moment annule l'impact**
grâce aux i-frames de l'Étape 2. Les chasseurs distancés de plus de 400 m se
retirent d'eux-mêmes.

À l'arrivée : contrat rempli, chasseurs retirés. En cas d'échec : le contrat
redevient disponible — on ne perd jamais définitivement une mission.

### Repères de conduite (ce qui a été soigné)

Le générateur **n'est pas un semis aléatoire** : il garantit que la carte reste
roulable, ce qui est la seule chose qui compte en monde ouvert.

| Garde-fou | Effet |
|---|---|
| Aucun décor à moins de **13 m d'un chemin** (distance au *segment*, pas au nœud) | On ne percute jamais un pilier en suivant une route |
| Emplacements des tours et des balises **réservés avant la génération** | Rien ne pousse dans une rampe d'accès ni dans une balise |
| **Murs invisibles** au bord de la carte | Impossible de tomber dans le vide à 42 m/s |
| Sol **carré subdivisé** (texture de gravier répétée tous les 40 m) | Pas de trou, pas de scintillement |
| Ombres limitées à **150 m** autour du joueur | Un kilomètre de décor, sans écrouler le framerate |

### Performance visée

| Élément | Coût approximatif |
|---|---|
| Sol (1,2 km, 24×24 quads) | 1 draw call, 1 152 triangles |
| Zone ruines | 1 draw call, ~20 000 triangles |
| Zone rampes | 1 draw call, ~5 000 triangles |
| Chemins + bornes | 2 draw calls |
| Tours (×3) | ~6 draw calls chacune |
| Chasseurs (×2 max) | ~4 draw calls chacun |

Environ **40 000 triangles au total**, pour **moins de 30 draw calls** : c'est le
nombre d'appels de rendu, pas le nombre de triangles, qui fait ramer un jeu.
D'où la fusion systématique des maillages — et des ombres limitées à 150 m
autour du joueur, seules réellement visibles.

### Contrôles (Étape 2 — opérationnels)

| Action | Touche |
|---|---|
| Accélérer / Reculer | `Z`/`W` ou `↑` — `S` ou `↓` |
| Tourner | `Q`/`A` ou `←` — `D` ou `→` |
| **Drift** (charge l'Éther) | Maintenir `Espace` |
| **Boost** (décharge l'Éther) | *Relâcher* `Espace` |
| **Esquive** (i-frames 0,4 s) | `Shift` ou clic droit |
| **Descente de tour** (Étape 3) | `R` — uniquement au sommet d'une tour synchronisée |
| **Caméra libre 360°** | Souris : regarde à gauche, à droite, au-dessus et derrière |
| **Recaler la caméra** derrière la voiture | `C` |
| Zoomer / dézoomer | Molette de la souris |

> 💡 **Conduite dans le monde ouvert** : les chemins cyan mènent aux tours ; monte la spirale pour révéler la carte. Les balises cyan de contrat démarrent une course contre-la-montre — deux chasseurs se lancent à tes trousses. `Shift` au moment de l'impact les annule.
>
> 💡 Les deux dispositions AZERTY et QWERTY sont acceptées : Unity associe `KeyCode` à la position physique de la touche, et le code teste les deux.
>
> 🎮 **Boucle de jeu** : drifter pour charger l'Éther → relâcher pour envoyer le boost → esquiver avec `Shift` pour traverser une attaque (0,4 s d'invincibilité, payée en endurance).

---

## 🗺️ Feuille de route (5 étapes)

- [x] **Étape 1 — Setup + Bootstrapper procédural** : Git, `.gitignore` Unity, arborescence, sol / véhicule / caméra / lumière générés par code, rendu low-poly.
- [x] **Étape 2 — Conduite arcade + drift + boost** : `CarController` (sphere car), `PlayerStats` (éther + endurance + i-frames), trails procéduraux, caméra à secousses.
- [x] **Étape 3 — Monde ouvert** : carte de 1,2 km en 3 zones (`WorldGenerator`), tours de synchronisation escaladables (`SyncTower`), contrats de livraison chronométrés avec chasseurs IA (`ContractManager`, `ChaserAI`), descente guidée (`DescentHelper`).
- [ ] **Étape 4 — Seigneurs Mécaniques** : IA de boss géant, patterns d'attaque, arène ouverte, dégâts par lames.
- [ ] **Étape 5 — UI + boucle de jeu** : HUD (vitesse, jauge, contrats), menus, déblocage d'améliorations permanentes (grappin, turbo, lames).

---

## 📐 Conventions de code (pour débutants)

- **Namespaces :** `AetherDrift.Core`, `AetherDrift.Player`, `AetherDrift.World`, `AetherDrift.Enemies`, `AetherDrift.UI` — un dossier = un namespace.
- **Commentaires en français** dans le code pour faciliter la lecture ; identifiants en anglais (convention C#).
- **Aucune dépendance externe** à l'Étape 1 : uniquement `UnityEngine` (et `UnityEditor` pour l'outil de menu, exclu du build par `#if UNITY_EDITOR`). Tout est en primitives + matériaux créés par code.
- **Ordre d'exécution :** `GameBootstrapper` a `[DefaultExecutionOrder(-100)]` pour générer le monde avant tout autre script.
- **Source de vérité unique :** le châssis visuel n'est déplacé que par `CarController.LateUpdate()`. Un seul script écrit un transform donné — deux scripts produiraient un rendu qui tremble.
- **Responsabilités séparées :** `CarController` lit la physique et applique les forces ; `PlayerStats` possède les ressources et les règles (i-frames, régénération). Le HUD et les boss lisent `PlayerStats` sans toucher au contrôleur.
- **Pièges C# à connaître :** `const` n'accepte qu'une valeur calculable à la compilation (impossible d'y mettre un champ `[SerializeField]`) ; `0.5` est un `double`, il faut écrire `0.5f` pour un `float` ; un `MeshCollider` non-convexe ne peut pas être un `Rigidbody` dynamique.
- **Pièges Unity à connaître :** ne jamais déplacer un objet pendant ▶ Play (tout est perdu à la sortie), stocker `GetComponent` dans un champ (`Awake`) plutôt que l'appeler dans `Update`, ne jamais éditer ni supprimer un fichier `.meta` à la main.
- **Archive de référence (Étape 1) :** branche `arena/04aef5b9-jeu`, commit `0db6aef` — `feat: project setup and procedural bootstrapper`.

---

## 📄 Licences

**Code du jeu :** projet personnel / pédagogique, tous droits réservés.

**Ressources tierces :**

| Ressource | Auteur | Licence | Attribution requise |
|---|---|---|---|
| Modèle de véhicule `muscle_blue` | Popyca Studio — [5 Free Low-Poly Toy Cars](https://github.com/Vuena/free-low-poly-toy-cars-cc0) | **CC0 1.0** (domaine public) | Non |

Le modèle est en domaine public : usage commercial, modification et
redistribution autorisés sans autorisation ni attribution. La dédicace
originale est conservée dans `Assets/_Project/Resources/Vehicles/LICENSE-CC0.txt`
par respect pour l'auteur, et pour que la provenance reste traçable.
Détails de la conversion FBX → OBJ : voir `Resources/Vehicles/README.md`.
