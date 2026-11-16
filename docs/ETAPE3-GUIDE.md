# Étape 3 — Le monde ouvert, guide pas-à-pas (débutant)

Ce guide explique **ce que tu vas voir**, **comment y jouer**, et **où régler
quoi** si tu veux changer le monde. Aucune connaissance de C# n'est nécessaire.

---

## 1. Ce qui apparaît quand tu appuies sur ▶

Tu apparais au centre d'une carte de **1 200 m × 1 200 m** (1,2 km de côté).

| Ce que tu vois | Où | À quoi ça sert |
|---|---|---|
| Piliers, arches, arcades effondrées | Partout autour de toi (rayon 240 m) | Décor des ruines : c'est ton terrain de jeu de départ |
| Grandes tours avec une rampe en spirale | À 150 m du centre, à 45°, 165° et 285° | Les **tours de synchronisation** (voir §3) |
| Routes grises bordées de petites bornes lumineuses cyan | En anneau à 300 m, plus quatre routes vers le centre | Repères de navigation : suivre une route mène toujours quelque part |
| Rampes de terre, de plus en plus grandes | Tout au nord (Z ≈ 450) | Zone de saut : prends de l'élan, décolle |
| Balises cyan (3 piliers lumineux en trépied) | Dispersées dans le monde | Les **contrats** (voir §4) |

> 🧭 **Repère simple** : les routes forment une croix au centre + un cercle
> autour. Si tu es perdu, rejoins une route et suis-la.

---

## 2. Les commandes

| Action | Touche |
|---|---|
| Accélérer / reculer | `Z`/`W` ou `↑` — `S` ou `↓` |
| Tourner | `Q`/`A` ou `←` — `D`/`→` |
| Drifter (charge l'Éther) | Maintenir `Espace` |
| Boost (décharge l'Éther) | *Relâcher* `Espace` |
| Esquive (0,4 s d'invincibilité) | `Shift` ou clic droit |
| **Redescendre d'une tour** | `R` (uniquement au sommet) |
| **Tourner la caméra** (360°) | Souris — `C` pour la remettre derrière la voiture |
| Zoomer / dézoomer | Molette |

---

## 3. Synchroniser une tour (façon *Assassin's Creed*)

1. **Trouve la rampe.** Elle fait le tour de la tour en montant (3,5 tours).
   Son entrée est au niveau du sol, côté opposé à la route la plus proche.
2. **Monte en roulant.** La pente est douce (12 à 14°) : pas besoin d'élan, il
   suffit d'accélérer. Un garde-corps t'empêche de tomber à l'extérieur.
3. **Arrive en haut.** Dès que tu touches la plateforme, la tour :
   - passe du **bleu** à l'**or**,
   - envoie trois anneaux lumineux vers le ciel,
   - provoque une secousse de caméra,
   - affiche dans la Console : `Progression : 1/3 tours synchronisées`.
4. **Redescends avec `R`.** La voiture glisse le long de la tour jusqu'au sol,
   puis tu récupères le contrôle. (Tu peux aussi redescendre la spirale à la
   main si tu préfères.)

> 💡 `R` ne fonctionne **que** si tu es au sommet : ailleurs, il ne fait rien.
> C'est volontaire, pour éviter de téléporter le joueur par accident.

---

## 4. Remplir un contrat (façon *GTA*)

1. **Traverse une balise cyan.** Le chronomètre démarre, la destination
   s'allume en **or**, et **deux chasseurs** apparaissent derrière toi.
2. **Roule vers l'or.** La distance restante et le temps sont affichés en haut
   à droite de l'écran.
3. **Évite les chasseurs.** Ils poursuivent, chargent quand ils sont alignés,
   puis freinent pour se replacer. **Ce freinage est ta fenêtre** : accélère
   ou drifte à ce moment-là.
4. **Esquive au dernier moment.** `Shift` donne 0,4 s d'invincibilité : un
   impact pendant cette fenêtre est annulé (`Percussion esquivée` dans la
   Console).
5. **Atteins la balise or.** Le contrat est rempli, les chasseurs disparaissent.

Si le temps s'écoule : le contrat échoue, il redevient disponible, et il faut
**sortir de la balise** avant de pouvoir le reprendre (pour éviter les
relances en boucle).

---

## 5. Régler le monde (sans coder)

Sélectionne l'objet **Bootstrapper** dans la scène, puis regarde l'Inspector.

### `GameBootstrapper`

| Champ | Effet |
|---|---|
| **Open World Enabled** | Décoche-le pour revenir à la petite arène de l'Étape 1 (utile si ça rame) |
| **World Half Size** | Demi-taille de la carte (600 = 1,2 km) |
| **Sync Tower Count** | Nombre de tours (3 par défaut) |
| **Contract Count** | Nombre de contrats (3 par défaut) |
| **Fog Density** | Épaisseur du brouillard (baisse-le pour voir plus loin) |

### `WorldGenerator` (objet `OpenWorld`)

| Champ | Effet |
|---|---|
| **Ruins Count** | Densité des ruines (320 par défaut) |
| **Ramps Count** | Nombre de rampes (34 par défaut) |
| **Clearance Radius** | Distance minimale entre un chemin et un obstacle (13 m) |
| **Seed** | Change la graine → monde différent, même taille |

> ⚠️ **Ne mets pas Clearance Radius sous 8 m** : le décor commencerait à
> pousser sur les routes, et la conduite deviendrait pénible.

---

## 6. Si quelque chose ne va pas

| Symptôme | Cause probable | Solution |
|---|---|---|
| Le jeu rame | Ombres trop loin, ou carte trop dense | Décoche **Open World Enabled**, ou baisse **Ruins Count** |
| Je tombe dans le vide | Les murs invisibles sont mal placés | Vérifie que **World Half Size** est bien 600 et que l'objet `Limites (invisibles)` existe dans la hiérarchie |
| La voiture bute sur une marche | Une rampe mal orientée | Signale le souci : le générateur place les rampes au hasard, une valeur de **Seed** différente suffit souvent |
| Les balises de contrat sont dans un mur | Génération dans le désordre | Supprime la scène et relance ▶ : l'ordre est garanti par le Bootstrapper |
| Rien ne se passe au ▶ | Le Bootstrapper n'est pas dans la scène | Le jeu le crée automatiquement ; sinon, menu **Aether Drift ▸ Créer la scène de boot** |

---

## 6 bis. La caméra (nouveau)

La caméra tourne librement autour de la voiture :

- **Souris** → tu regardes à gauche, à droite, derrière, et tu lèves ou baisses
  le point de vue (de la vue rasante à la vue plongeante).
- **`C`** → la caméra se remet instantanément dans le dos de la voiture.
- **Molette** → tu rapproches ou éloignes la caméra.
- Après **1,6 s** sans bouger la souris **et si tu roules**, elle revient
  d'elle-même derrière la voiture. À l'arrêt, elle reste où tu l'as laissée :
  c'est pratique pour admirer une tour de synchronisation.

Réglages, dans l'Inspector de l'objet `Main Camera` :

| Champ | Effet |
|---|---|
| **Yaw Sensitivity** | Vitesse de rotation horizontale (baisse-la si c'est trop nerveux) |
| **Pitch Sensitivity** | Vitesse de rotation verticale |
| **Invert Vertical** | Inverse le haut/bas de la souris |
| **Min / Max Pitch** | Limites de l'inclinaison (évite de passer sous la voiture) |
| **Recenter Delay** | Temps avant retour automatique (0 = jamais) |
| **Recenter Min Speed** | En dessous de cette vitesse, pas de recentrage (5 m/s) |
| **Avoid Obstacles** | Décoche si la caméra « saute » près des ruines |

> ⚠️ Si la souris ne fait rien : `Edit ▸ Project Settings ▸ Player ▸`
> **Active Input Handling** doit valoir **Both** (ou *Input Manager (Old)*).
> En mode *Input System Package (New)* seul, l'ancien `Input` de Unity est
> désactivé ; la Console affiche alors un avertissement explicite.

## 7. Ce que tu peux modifier sans risque

- Les **couleurs** (`Stone Color`, `Earth Color`, `Marker Color`) du `WorldGenerator`.
- La **hauteur des tours** : dans `GameBootstrapper`, la ligne
  `42f + i * 4f` donne 42, 46 et 50 m aux trois tours.
- Le **temps des contrats** : `minDeliveryDistance` / `maxDeliveryDistance` dans
  `ContractManager`, et la formule `15f + distance / 12f` (marge + trajet).
- La **vitesse des chasseurs** (`Cruise Speed`, `Charge Speed`) dans `ChaserAI`.

À éviter : toucher aux `MeshCollider` des zones fusionnées (le décor n'a plus
de collider individuel, tout passe par le maillage complet).
