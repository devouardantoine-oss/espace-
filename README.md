# ESPACE — Grande stratégie galactique mobile

Prototype de jeu mobile de grande stratégie 2D (inspiré d'Europa Universalis, Supremacy 1914,
Age of History II), dans un univers de science-fiction original — aucun élément sous licence
tierce. Le joueur dirige un empire galactique par l'économie, la diplomatie, la recherche,
l'espionnage et la guerre. **Aucune bataille n'est simulée en temps réel** : les conflits sont
résolus automatiquement à partir des statistiques (puissance militaire, technologie, moral,
ravitaillement, terrain, commandement).

- **Moteur :** Unity 6 (`6000.0.x`) · Universal Render Pipeline
- **Langage :** C# — identifiants en anglais, documentation en français
- **Cibles :** Android · iOS
- **Vue :** 2D, caméra orthographique
- **Temps :** hybride temps réel / tour — horloge continue avec pause et vitesses (façon
  *Crusader Kings*), simplifiée pour des sessions mobiles courtes (Phase 3)

> **Statut : Phase 2 terminée** — carte galactique (100 systèmes). Économie, diplomatie,
> recherche, espionnage et armées ne sont pas encore implémentés.

> **Note d'historique :** le projet a démarré sur un concept différent (stratégie temps réel
> façon *Total War*, batailles 3D). La Phase 1 (socle technique : services, événements,
> machine à états, configuration) reste entièrement valide et a été conservée telle quelle —
> elle est agnostique du gameplay. Le concept a été redéfini avant tout code de gameplay.

---

## 1. Ouvrir le projet

```bash
git clone <url-du-depot>
cd espace-
git lfs install   # les binaires (textures, modèles, sons) passeront par LFS
```

1. **Unity Hub → Add → Add project from disk**, sélectionner le dossier cloné.
2. Ouvrir avec **Unity 6.0.x**. Le Package Manager résout URP, Input System et
   Addressables au premier lancement (quelques minutes).
3. Lancer **`Tools → Espace → Setup Project`** (voir §2).
4. Ouvrir `Assets/Scenes/Bootstrap.unity` et appuyer sur **Play**.

### En cas d'erreur de résolution de package

`Packages/manifest.json` épingle des versions précises. Si Unity signale un paquet
introuvable, ouvrez **Window → Package Manager**, sélectionnez le paquet concerné et
installez la version proposée par votre éditeur : le code du projet n'en dépend pas.

---

## 2. Le script de configuration

`Tools → Espace → Setup Project` (`Assets/Scripts/Editor/ProjectSetup.cs`) applique en
une fois tous les réglages du projet :

| Réglage | Valeur appliquée |
|---|---|
| Render pipeline | crée `Assets/Settings/URP-Mobile.asset` et l'assigne (global + tous les niveaux de qualité) |
| Rendu mobile | HDR off, MSAA 2×, textures depth/opaque off, distance d'ombre 60 |
| Espace colorimétrique | Linear |
| Active Input Handling | Input System Package (New) |
| Build Settings | `Bootstrap.unity` en scène 0 |
| Android | IL2CPP, ARM64, min SDK 24 |
| iOS | IL2CPP, cible minimale 13.0 |

**Pourquoi un script plutôt que des `ProjectSettings/*.asset` versionnés ?** Ces fichiers
YAML dépendent de la version de sérialisation exacte de l'éditeur ; un champ obsolète
suffit à empêcher l'ouverture du projet. Les API officielles donnent un résultat valide
quelle que soit la version d'Unity 6 installée.

> Le passage à l'Input System **impose un redémarrage de l'éditeur**. Acceptez la
> proposition d'Unity, ou fermez et rouvrez le projet.

L'opération est idempotente : la relancer ne crée aucun doublon.

---

## 3. Architecture

```
Assets/
├── Scenes/
│   ├── Bootstrap.unity           # scène de démarrage : caméra, lumière, [GameBootstrap]
│   └── GalaxyMap.unity           # scène jouable Phase 2 : carte galactique
├── Settings/                     # assets URP (générés par le script de setup)
├── ScriptableObjects/            # instances de données éditables
│   ├── GameConfig.asset
│   └── GalaxyConfig.asset
├── Scripts/
│   ├── Core/                     # → Espace.Core     (aucune dépendance sortante)
│   ├── Data/                     # → Espace.Data     (ScriptableObjects et types génériques)
│   ├── Managers/                 # → Espace.Managers (composition de l'application)
│   ├── Gameplay/                 # → Espace.Gameplay (référence Core + Data)
│   │   └── Galaxy/               #     carte galactique, génération, caméra, sélection
│   ├── UI/                       # Phase 11
│   └── Editor/                   # → Espace.Editor   (outillage, exclu des builds)
└── Tests/EditMode/               # → Espace.Tests.EditMode
```

### Direction des dépendances

```
Espace.Core  ←  Espace.Data  ←  Espace.Gameplay
      ↖               ↖               ↖
       ╰──── Espace.Managers, Espace.Editor, Espace.Tests.EditMode
```

Les *Assembly Definitions* rendent cette direction **vérifiée par le compilateur** :
`Espace.Core` ne peut pas référencer `Espace.Gameplay`, même par accident. Bénéfice
secondaire : modifier l'UI ne recompile pas le cœur du jeu.

> `Espace.Data` ne référence jamais `Espace.Gameplay` (cela créerait une dépendance
> circulaire) : les ScriptableObjects propres à un système de gameplay (ex. `GalaxyConfig`)
> vivent dans `Espace.Gameplay`, pas dans `Espace.Data`. Seuls les types véritablement
> transverses (`ResourceType`, `GameConfig`) restent dans `Espace.Data`.
>
> L'assembly `Espace.UI` sera ajoutée avec ses premiers scripts (Phase 11).

### Briques du socle (Phase 1)

| Classe | Rôle | Choix technique |
|---|---|---|
| `ServiceLocator` | registre des services | remplace les singletons `Instance` : chaque service est enregistré **sous son interface**, donc remplaçable et testable (SOLID/DIP) |
| `IGameService` | contrat `Initialize` / `Shutdown` | l'initialisation se fait après l'enregistrement de tous les services → aucune contrainte d'ordre |
| `EventBus` / `IEventBus` | messagerie typée | `Dictionary<Type, Delegate>` multicast → **zéro allocation** à la publication ; les systèmes ne se connaissent pas |
| `IGameEvent` | marqueur d'événement | à implémenter sur des `readonly struct` : pas de boxing, pas de GC |
| `GameStateMachine` | flux global | gère la **réentrance** : un état peut demander la transition suivante depuis son `Enter()` (cas `BootState → MainMenuState`) |
| `SceneLoaderService` | chargement de scène | via `AsyncOperation.completed`, donc sans coroutine ni MonoBehaviour hôte ; passera à Addressables derrière la même interface |
| `ObjectPool<T>` | recyclage d'objets | C# pur (`where T : class`) : utilisable avec ou sans GameObject, et testable hors Play Mode |
| `GameLog` | journalisation | `[Conditional]` → appels **et arguments** supprimés en build release : aucune allocation de chaîne en production |
| `GameBootstrap` | composition | seul MonoBehaviour de la scène, `[DefaultExecutionOrder(-1000)]`, unique endroit qui connaît les types concrets |
| `GameConfig` | réglages globaux | ScriptableObject : éditable sans recompiler, versionnable en texte |

### Briques de la carte galactique (Phase 2)

| Classe | Rôle | Choix technique |
|---|---|---|
| `StarSystemId`, `HyperlaneLink` | identifiants forts | structures immuables comparables par valeur, évitent de confondre un id de système avec un `int` quelconque |
| `StarSystemState` | données d'un système | nom, position, population, richesse, développement, stabilité, propriétaire, gisements — pas encore de simulation (Phase 4/5) |
| `GalaxyMap` | graphe de la galaxie | accès O(1) par identifiant et par voisinage ; valide ses données d'entrée à la construction |
| `GalaxyGenerator` | génération procédurale | **déterministe** (`System.Random` dédié, pas `UnityEngine.Random`) : même graine = même galaxie, donc testable |
| `StarSystemNameGenerator` | noms de systèmes | composition de syllabes originales, unicité garantie dans une même génération |
| `GalaxyConfig` | réglages de génération | ScriptableObject ; convertit vers `GalaxyGenerationParameters`, une structure C# pure testable sans instancier d'asset |
| `RuntimeSpriteFactory` | sprite des systèmes | disque généré par code (pas de texture importée à faire vérifier sans éditeur) ; **une seule instance partagée** par tous les marqueurs pour permettre le batching |
| `GalaxyLinkRenderer` | rendu des routes | toutes les routes hyperspatiales fusionnées en **un seul maillage** → un draw call quel que soit leur nombre, plutôt qu'un `LineRenderer` par route |
| `GalaxyCameraController` | caméra tactile | glisser (pan) + pincer (zoom) via `EnhancedTouch`, plus souris/molette pour tester dans l'éditeur ; bornes calculées depuis le rayon de la galaxie |
| `GalaxySelectionController` | sélection tactile | distingue un tap d'un glisser par seuils de durée/déplacement ; publie `SystemSelectedEvent` / `SystemDeselectedEvent` sur l'`IEventBus` |
| `GalaxyMapController` | orchestrateur de scène | construit toute la carte **par code** plutôt que par références d'inspecteur — réduit au minimum le YAML de scène à écrire à la main |

---

## 4. Tester la Phase 1

**Au lancement (Play sur `Bootstrap.unity`)** — la console doit afficher, sans aucune
erreur ni warning :

```
[Bootstrap] Configuration appliquee (cible : 60 FPS).
[Bootstrap] 3 services enregistres.
[FSM] Entree dans BootState
[FSM] Sortie de BootState
[FSM] Entree dans MainMenuState - le socle est operationnel.
```

**Tests unitaires** — `Window → General → Test Runner → EditMode → Run All`.
Voir §5 pour le compte total (73 tests, tous packages confondus).

**Build** — `File → Build Settings` : Android et iOS doivent être sélectionnables,
avec `Bootstrap` en scène 0.

---

## 5. Tester la Phase 2 — carte galactique

**Ouvrir `Assets/Scenes/GalaxyMap.unity` et appuyer sur Play.** La console doit afficher,
sans erreur ni warning :

```
[Bootstrap] Configuration appliquee (cible : 60 FPS).
[Bootstrap] 3 services enregistres.
[FSM] Entree dans BootState
[FSM] Sortie de BootState
[FSM] Entree dans MainMenuState - le socle est operationnel.
[GalaxyMap] Galaxie generee : 100 systemes, ~125 routes hyperspatiales.
```

Dans la fenêtre Game :
- **100 points colorés** (plus clairs = plus développés) répartis dans un disque, reliés par
  un réseau de fines lignes (routes hyperspatiales).
- **Glisser** (clic maintenu + déplacer, ou glisser au doigt) déplace la caméra ; **molette**
  (éditeur) ou **pincement à deux doigts** (mobile) zoome, avec des bornes qui empêchent de
  sortir de la galaxie ou de zoomer à l'infini.
- **Toucher un système** (tap bref, sans glisser) affiche son détail dans l'encart en haut à
  gauche : nom, population, richesse, développement, stabilité, propriétaire (« Independant »
  pour tous en Phase 2, les empires arrivent en Phase 5), gisements, nombre de routes.
  Toucher le fond vide referme l'encart.

> Ce panneau en haut à gauche est un outil de mise au point temporaire (IMGUI), pas l'écran
> « Gestion des systèmes » prévu en Phase 11 — voir le commentaire de `GalaxyMapController`.

**Tests unitaires propres à la galaxie** (inclus dans le Run All du Test Runner) :
`GalaxyGeneratorTests` (déterminisme, connexité totale, respect des distances, plages de
statistiques), `GalaxyMapTests` (validation des données, requêtes de voisinage),
`HyperlaneLinkTests` (égalité non ordonnée A↔B), `StarSystemNameGeneratorTests`
(déterminisme et unicité des noms).

**Point à vérifier en priorité sur appareil réel** — c'est la partie la plus délicate à
garantir sans pouvoir ouvrir l'éditeur ici : le geste de pincement (`GalaxyCameraController`,
API `EnhancedTouch`) et la distinction tap/glisser (`GalaxySelectionController`). La logique
de génération, elle, a été recoupée indépendamment par un script Python qui reproduit
l'algorithme (déterminisme, connexité sur 10 graines, absence de doublons) : voir le
commentaire de `GalaxyGenerator` pour le détail de l'algorithme.

---

## 6. Feuille de route

| Phase | Contenu | État |
|---|---|---|
| 1 | Socle technique : services, événements, machine à états, configuration | ✅ terminée |
| 2 | Carte galactique : 100 systèmes, génération procédurale, caméra tactile, sélection | ✅ terminée |
| 3 | Horloge de jeu : temps continu, pause, vitesses | à venir |
| 4 | Économie : Crédits, Minerais, Énergie, Nourriture, Influence | à venir |
| 5 | Empires et IA de base (personnalités, colonisation) | à venir |
| 6 | Armées et résolution automatique des combats | à venir |
| 7 | Diplomatie (alliances, traités, embargos, ultimatums...) | à venir |
| 8 | Recherche (arbre technologique, 7 domaines) | à venir |
| 9 | Espionnage (agents, sabotage, vol de technologie) | à venir |
| 10 | Sauvegarde JSON automatique | à venir |
| 11 | Interface complète (menu, écrans de gestion, HUD) | à venir |
| 12 | Équilibrage | à venir |

Chaque phase est développée, testée et validée avant de passer à la suivante. Un seul
système complexe à la fois (consigne du brief) : la Phase 2 n'a touché ni l'économie, ni le
temps, ni aucun autre système.

---

## 7. Conventions

- **Données dans des assets, jamais en dur** : tout contenu (factions, unités, bâtiments)
  passe par des ScriptableObjects.
- **Aucune duplication** : une brique réutilisable va dans `Espace.Core`.
- **Communication par événements** : deux systèmes ne se référencent pas directement,
  ils passent par l'`IEventBus`.
- **Toujours se désabonner** dans `Shutdown` / `OnDestroy`.
- **Optimisation mobile** : pooling plutôt que `Instantiate`/`Destroy`, peu de draw calls,
  pas d'allocation dans les boucles par frame.

### Fusion Git des scènes

Les fichiers `.unity`, `.prefab` et `.asset` sont configurés pour *UnityYAMLMerge*.
À configurer une fois par poste :

```bash
git config merge.unityyamlmerge.driver \
  "'<chemin-unity>/Tools/UnityYAMLMerge' merge -p %O %B %A %A"
```
