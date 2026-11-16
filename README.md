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

Cette étape initialise le projet et génère **toute la scène par code**, sans aucune scène `.unity` à créer à la main :

| Élément | Fichier | Rôle |
|---|---|---|
| **Bootstrapper** | `Assets/_Project/Scripts/Core/GameBootstrapper.cs` | Au `Awake()`, génère le sol, le véhicule, la caméra et la lumière via `GameObject.CreatePrimitive`. |
| **Caméra fluide** | `Assets/_Project/Scripts/Player/CameraFollow.cs` | Suit le véhicule en `LateUpdate()`, zoom arrière + FOV dynamique selon la vitesse. |
| **Véhicule (base)** | `Assets/_Project/Scripts/Player/PlayerVehicle.cs` | Sphère physique invisible (`Rigidbody` + `SphereCollider`) + châssis visuel en primitives avec lames latérales lumineuses. Conduite complète = **Étape 2**. |
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
        ├── Core/      → GameBootstrapper.cs (génération procédurale)
        ├── Player/    → CameraFollow.cs, PlayerVehicle.cs
        ├── World/     → (Étape 3 : terrain, ruines, props)
        ├── Enemies/   → (Étape 4 : Seigneurs Mécaniques / boss)
        └── UI/        → (Étape 5 : HUD, menus, jauge boost/drift)
```

---

## 🛠️ Comment ouvrir le projet (Unity)

> 📖 **Débutant complet ?** Suis le guide illustré pas-à-pas : **[docs/ETAPE1-GUIDE.md](docs/ETAPE1-GUIDE.md)** (installation Unity, création de la scène, lancement, personnalisation).

1. **Unity version recommandée :** 2022.3 LTS (ou Unity 6). Le code est compatible Built-in et URP (le Bootstrapper choisit automatiquement le shader `Universal Render Pipeline/Lit` si disponible, sinon `Standard`).
2. **Ouvre le dossier** du dépôt dans Unity Hub → *Add* → sélectionne la racine du repo (celle qui contient `Assets/`, `ProjectSettings/`, `Packages/`).
3. **Crée la scène en un clic :** menu **`Aether Drift ▸ Créer la scène de boot (Étape 1)`**.
   *(Alternative manuelle : `File → New Scene → Empty`, GameObject vide nommé `Bootstrapper`, glisser `GameBootstrapper.cs` dessus, sauvegarder sous `Assets/_Project/Scenes/Boot.unity`.)*
4. **Appuie sur ▶ Play.** Tout est généré automatiquement :
   - un grand sol sombre,
   - le véhicule runique au centre (sphère invisible + châssis + lames cyan lumineuses),
   - la caméra qui suit le véhicule,
   - une lumière directionnelle avec ombres.
5. **Sauvegarde la scène** (`Ctrl+S`) sous `Assets/_Project/Scenes/Boot.unity` pour la retrouver au prochain lancement.

> 💡 **Aucune scène n'est fournie dans le repo** : c'est volontaire. Le Bootstrapper est la source de vérité — la scène se régénère à chaque Play. Les fichiers `.meta` seront générés par Unity à la première ouverture (ils sont versionnés ensuite).

### Contrôles (prévus — Étape 2)

| Action | Touche (prévue) |
|---|---|
| Accélérer / Freiner | `Z / S` ou `W / S` (AZERTY) — `↑ / ↓` |
| Tourner | `Q / D` ou `A / D` — `← / →` |
| Drift + Boost (jauge) | `Espace` (maintenir) |
| Dash d'invulnérabilité | `Shift` |
| Caméra | suit automatiquement le véhicule |

> ⚠️ À l'Étape 1, le véhicule ne conduit pas encore : il est **statique** (Rigidbody en place, caméra qui le suit). La conduite arcade, le drift et la jauge d'endurance arrivent à l'Étape 2.

---

## 🗺️ Feuille de route (5 étapes)

- [x] **Étape 1 — Setup + Bootstrapper procédural** *(cette branche)* : Git, `.gitignore` Unity, arborescence, sol / véhicule / caméra / lumière générés par code.
- [ ] **Étape 2 — Conduite arcade + drift + boost** : `PlayerVehicle` complet (accélération, frein, direction, drift, jauge endurance, dash d'invulnérabilité, lames de drift).
- [ ] **Étape 3 — Monde ouvert** : vallée low-poly, ruines escaladables, props, contrats de livraison (waypoints + timer).
- [ ] **Étape 4 — Seigneurs Mécaniques** : IA de boss géant, patterns d'attaque, arène ouverte, dégâts par lames.
- [ ] **Étape 5 — UI + boucle de jeu** : HUD (vitesse, jauge, contrats), menus, déblocage d'améliorations permanentes (grappin, turbo, lames).

---

## 📐 Conventions de code (pour débutants)

- **Namespaces :** `AetherDrift.Core`, `AetherDrift.Player`, `AetherDrift.World`, `AetherDrift.Enemies`, `AetherDrift.UI` — un dossier = un namespace.
- **Commentaires en français** dans le code pour faciliter la lecture ; identifiants en anglais (convention C#).
- **Aucune dépendance externe** à l'Étape 1 : uniquement `UnityEngine`. Tout est en primitives + matériaux créés par code.
- **Ordre d'exécution :** `GameBootstrapper` a `[DefaultExecutionOrder(-100)]` pour générer le monde avant tout autre script.

---

## 📄 Licence

Projet personnel / pédagogique. Tous droits réservés.
