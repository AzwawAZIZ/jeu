# Étape 1 — Guide pas-à-pas (débutant)

Ce guide suppose que tu n'as **jamais** ouvert Unity. Compte 15 minutes.
Aucun asset à télécharger : tout le monde est généré par code.

---

## 1. Installer Unity

1. Télécharge **Unity Hub** : https://unity.com/download
2. Dans le Hub → onglet **Installs** → **Install Editor** → choisis **2022.3 LTS** (ou *Unity 6 LTS*).
3. Coche au minimum le module **Windows Build Support** (ou *Mac Build Support*). Le reste n'est pas nécessaire pour l'Étape 1.
4. Attends la fin de l'installation (10–20 min).

> 💡 Le projet fonctionne aussi bien en **Built-in Render Pipeline** qu'en **URP**. Le code choisit automatiquement le bon shader.

---

## 2. Ouvrir le projet

1. Ouvre **Unity Hub** → **Projects** → **Add** → **Add project from disk**.
2. Sélectionne le **dossier du dépôt** (celui qui contient `Assets/`, `Packages/`, `ProjectSettings/`).
3. Clique sur le projet pour l'ouvrir. Unity va :
   - importer les scripts (1–3 min la première fois),
   - générer les fichiers `.meta` manquants,
   - compiler les 3 scripts C#.

✅ **Vérification :** dans la fenêtre **Console** (`Window → General → Console`), tu ne dois voir **aucune erreur rouge**. Des messages jaunes d'avertissement sont acceptables.

---

## 3. Créer la scène de boot (une seule fois)

Le dépôt ne contient volontairement **aucune scène** : la scène se régénère par code.

**Méthode automatique (recommandée) :**

1. Menu **`Aether Drift ▸ Créer la scène de boot (Étape 1)`**.
2. Une boîte de dialogue confirme : la scène `Assets/_Project/Scenes/Boot.unity` est créée,
   le GameObject `Bootstrapper` y est ajouté, et la scène est mise en première position dans les *Build Settings*.
3. Passe directement au **§4**.

**Méthode manuelle (si tu veux tout faire à la main) :**

1. Menu **File → New Scene** → choisis **Basic (Built-in)** ou **Empty**.
2. Menu **GameObject → Create Empty** → renomme-le `Bootstrapper` (touche `F2`).
3. Dans la fenêtre **Project**, ouvre `Assets/_Project/Scripts/Core/`.
4. **Glisse** `GameBootstrapper.cs` sur le GameObject `Bootstrapper` dans la **Hierarchy**.
   → Dans l'**Inspector**, tu dois voir le composant `Game Bootstrapper` avec ses champs (Sol, Véhicule, Caméra, Lumière).
5. Menu **File → Save As** → `Assets/_Project/Scenes/Boot.unity`.

> 🧹 Si tu as choisi une scène **Basic**, Unity y a mis une caméra et une lumière par défaut :
> le Bootstrapper les supprime automatiquement au Play pour éviter les doublons.
> Tu peux aussi simplement repartir d'une scène **Empty**.

---

## 4. Lancer le jeu

Appuie sur **▶ Play** (en haut au centre).

Tu dois voir apparaître :

| Élément | Ce que tu vois |
|---|---|
| **Sol** | Un grand plan sombre de 200 × 200 m, quadrillé de lignes fines cyan tous les 20 m. |
| **Véhicule** | Un châssis sombre allongé, un cockpit bombé, **2 lames cyan lumineuses** sur les côtés, 4 patins lumineux et un propulseur orange à l'arrière. |
| **Caméra** | Vue de 3/4 arrière, le véhicule au centre de l'écran. |
| **Lumière** | Soleil bas et chaud, ombres portées au sol. |
| **Ruines** | 10 piliers de pierre en cercle autour de la zone, certains brisés. |
| **Ambiance** | Brouillard gris-bleu léger (vallée brumeuse). |

Dans la **Console**, tu verras :

```
[AETHER DRIFT] Bootstrapper : scène générée procéduralement (Étape 1).
```

✅ **Tout va bien si :** aucun texte rouge dans la Console, et le véhicule est posé au sol (pas en l'air, pas à moitié enterré).

❌ **Si tu vois du rouge**, copie-colle le message complet dans le chat. Les causes les plus fréquentes :

| Message | Cause | Solution |
|---|---|---|
| `The type or namespace name 'AetherDrift' could not be found` | Unity n'a pas fini d'importer | Attends la fin de la barre de progression en bas à droite, puis `Assets → Refresh`. |
| Deux caméras visibles / `There are 2 audio listeners` | Une caméra existait déjà dans la scène | Le Bootstrapper nettoie au Play ; sinon repars d'une scène **Empty**. |
| `Shader 'Universal Render Pipeline/Lit' not found` (avertissement jaune) | Projet en Built-in | **Normal** : le code bascule tout seul sur `Standard`. |
| Le véhicule tombe à l'infini | Le sol a été supprimé ou le Play relancé deux fois | Sors du Play, vérifie que la scène ne contient que `Bootstrapper`. |

---

## 5. Comprendre ce que fait le code (4 fichiers, ~800 lignes)

```
Assets/_Project/
├── Scripts/
│   ├── Core/GameBootstrapper.cs   → crée le monde (sol, véhicule, caméra, lumière)
│   └── Player/
│       ├── PlayerVehicle.cs       → construit le visuel + configure la physique
│       └── CameraFollow.cs        → caméra fluide avec zoom selon la vitesse
└── Editor/BootSceneCreator.cs     → outil éditeur : menu « Aether Drift ▸ Créer la scène »
```

### `GameBootstrapper.cs` — le « chef d'orchestre »

- `[DefaultExecutionOrder(-100)]` : il s'exécute **avant** tous les autres scripts.
- Dans `Awake()`, il appelle 5 méthodes dans l'ordre :
  1. `CreateGround()` → plan 200 m + grille de repères,
  2. `CreatePlayerVehicle()` → GameObject taggé `Player` + `Rigidbody` + `PlayerVehicle`,
  3. `CreateCamera()` → caméra + `CameraFollow`,
  4. `CreateDirectionalLight()` → soleil + ombres douces,
  5. `CreateDecorativeRuins()` → 10 piliers en cercle (décor).
- `ConfigureAmbiance()` → brouillard + lumière ambiante.
- `CreateMaterial()` → **compatible Built-in / URP** : `Shader.Find("Universal Render Pipeline/Lit")` puis repli sur `Standard`.
  Tous les réglages sont exposés dans l'Inspector (taille du sol, couleurs, intensité…) : modifie-les en Play pour voir l'effet immédiatement.

### `PlayerVehicle.cs` — le bolide

- **Sphère physique invisible** (enfant) : `MeshRenderer.enabled = false`, `SphereCollider` conservé → c'est elle qui collisionne.
- **Châssis visuel** (enfant `VisualChassis`) : 12 primitives, **aucune** ne garde de collider (`col.enabled = false` puis `Destroy`).
- **Lames cyan** : matériau émissif (`_EMISSION` + `_EmissionColor`) → elles brilleront plus fort au drift (Étape 2).
- Physique prête pour la suite : `Interpolate`, `ContinuousDynamic`, gravité, `FreezeRotation`.
- Le véhicule est **statique** à l'Étape 1 : il tombe de 0,5 m et se pose sur le sol.

### `CameraFollow.cs` — la caméra

- Suit en `LateUpdate()` (après la physique → pas de tremblement).
- `Vector3.SmoothDamp` sur la position, `Quaternion.Slerp` sur la rotation.
- **Zoom dynamique** : distance 10 m → 18 m et FOV 60° → 78° selon la vitesse lue sur le `Rigidbody`.
- À l'Étape 1 la vitesse est nulle : la caméra reste stable. Dès que la conduite arrive (Étape 2), le zoom s'active tout seul, sans retoucher ce script.

---

## 6. Personnaliser en 30 secondes

| Envie | Où | Champ |
|---|---|---|
| Sol plus grand | `Bootstrapper` | `Ground Size` (ex. 500) |
| Sol plus clair | `Bootstrapper` | `Ground Color` |
| Caméra plus haute | `Main Camera` | `Offset` (y) |
| Véhicule plus nerveux (Étape 2) | `PlayerVehicle` | `Mass` (ex. 800) |
| Lames d'une autre couleur | `PlayerVehicle` | `Blade Color` (ex. magenta) |
| Soleil plus fort | `Directional Light (Soleil)` | `Intensity` |

---

## 7. Sauvegarder son travail sur Git

```bash
git status                 # voir les fichiers modifiés
git add .                  # préparer les changements
git commit -m "feat: ..."  # enregistrer
```

> Les dossiers `Library/`, `Temp/`, `Logs/` sont déjà ignorés : ton dépôt reste léger.
> Les fichiers `.meta` sont versionnés : indispensable pour que Unity retrouve les scripts.

---

## ✅ Checklist de fin d'Étape 1

- [ ] Unity ouvre le projet sans erreur de compilation.
- [ ] La Console affiche le message du Bootstrapper.
- [ ] Je vois le sol, le véhicule avec ses lames cyan, les ruines et les ombres.
- [ ] La caméra est derrière le véhicule et ne tremble pas.
- [ ] Aucune erreur rouge dans la Console.
- [ ] `git log` montre le commit `feat: project setup and procedural bootstrapper`.

**Prochaine étape (2) :** conduite arcade — accélération, direction, drift, jauge d'endurance/boost et dash d'invulnérabilité.

---

## 8. Bonnes pratiques Unity dès maintenant (les 5 erreurs classiques du débutant)

1. **Ne déplace jamais un objet en Play.** Tout changement fait pendant ▶ Play est perdu en sortant du Play. C'est la règle n°1 d'Unity.
2. **Un dossier = un namespace.** Ici : `Core/` → `AetherDrift.Core`, `Player/` → `AetherDrift.Player`. Garde cette convention aux Étapes 3–5.
3. **`Awake` vs `Start`** : `Awake` = avant tout le monde (mise en place), `Start` = au premier frame (dépendances prêtes). Le Bootstrapper est en `Awake` avec `[DefaultExecutionOrder(-100)]`.
4. **Jamais de `GetComponent` dans `Update`.** On le fait une seule fois dans `Awake`/`Start` et on stocke le résultat dans un champ (voir `cam` et `rb`).
5. **Ne touche pas aux `.meta`.** Ils contiennent les identifiants des assets. On les versionne, on ne les édite jamais à la main, on ne les supprime jamais.

---

## 9. Ce qui arrive à l'Étape 2 (pour te projeter)

`PlayerVehicle.cs` sera complété (le visuel et la physique ne changeront pas) avec :

- accélération / freinage / marche arrière (`Z`/`S` ou `W`/`S`),
- direction (`Q`/`D` ou `A`/`D`),
- **drift** (maintenir `Espace`) : le véhicule glisse, les lames s'illuminent, la trace au sol apparaît,
- **jauge d'endurance** partagée entre le drift et le **dash d'invulnérabilité** (`Shift`),
- sensation de vitesse (FOV déjà branché, particules, tremblement).

> La caméra de l'Étape 1 est **déjà prête** : elle lit la vitesse du `Rigidbody` et n'aura pas besoin d'être modifiée à l'Étape 2.
