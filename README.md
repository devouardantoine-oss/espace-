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

> **Statut : Phase 4 terminée** — carte galactique (100 systèmes), horloge de jeu (temps
> continu, pause, vitesses) et économie (production automatique, impôts, construction,
> investissement). Empires/IA, diplomatie, recherche, espionnage et armées ne sont pas
> encore implémentés.

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
│   └── GalaxyMap.unity           # scène jouable : galaxie + horloge + économie (Phases 2-4)
├── Settings/                     # assets URP (générés par le script de setup)
├── ScriptableObjects/            # instances de données éditables
│   ├── GameConfig.asset
│   ├── GameClockConfig.asset
│   ├── GalaxyConfig.asset
│   └── Buildings/                # 5 types de bâtiments (1 par ressource)
├── Scripts/
│   ├── Core/                     # → Espace.Core     (aucune dépendance sortante)
│   ├── Data/                     # → Espace.Data     (ScriptableObjects et types génériques)
│   ├── Managers/                 # → Espace.Managers (composition de l'application)
│   ├── Gameplay/                 # → Espace.Gameplay (référence Core + Data)
│   │   ├── Galaxy/               #     carte galactique, génération, caméra, sélection
│   │   └── Economy/              #     production, bâtiments, impôts, investissement
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
> circulaire) : les ScriptableObjects propres à un système de gameplay (ex. `GalaxyConfig`,
> `BuildingType`) vivent dans `Espace.Gameplay`, pas dans `Espace.Data`. Seuls les types
> véritablement transverses (`ResourceType`, `ResourceBundle`, `GameConfig`) restent dans
> `Espace.Data`.
>
> L'assembly `Espace.UI` sera ajoutée avec ses premiers scripts (Phase 11).

> **Un seul trésor pour l'instant :** `StarSystemState.OwnerId` existe depuis la Phase 2,
> mais jusqu'à la Phase 4 aucun système n'était possédé. `EconomyController` attribue
> maintenant au joueur (`EconomyService.PlayerOwnerId = 0`) le système le plus proche du
> centre de la galaxie, pour avoir un propriétaire concret à simuler. C'est une solution
> minimale, pas le cadre complet des empires : la Phase 5 généralisera `OwnerId` à plusieurs
> empires dotés d'une IA, sans avoir à retoucher l'API de `EconomyService`.

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

### Briques de l'horloge de jeu (Phase 3)

| Classe | Rôle | Choix technique |
|---|---|---|
| `GameDate` | date de jeu | calendrier simplifié 12 mois × 30 jours (360 j/an, sans année bissextile) — convention courante en grande stratégie, arithmétique de date triviale |
| `GameSpeed` | palier de vitesse | `Paused`, `Normal`, `Fast`, `Faster`, `Fastest` — 4 paliers actifs plutôt que les 5 de CK3, pour rester lisible sur petit écran |
| `GameClockSettings` | paramètres purs | structure C# immuable (même rôle que `GalaxyGenerationParameters` en Phase 2) : `GameClock` reste testable sans ScriptableObject |
| `IGameClock` / `GameClock` | horloge | temps continu accumulé (`deltaTime × multiplicateur`) avec seuil de jour ; `Tick` n'est **pas** sur l'interface publique — seul `GameBootstrap` fait avancer le temps, tout le reste ne fait que le lire ou le piloter |
| événements (`DayAdvancedEvent`, `MonthAdvancedEvent`, `YearAdvancedEvent`, `GameSpeedChangedEvent`) | notifications | un `DayAdvancedEvent` **par jour réellement franchi**, même si plusieurs jours s'écoulent dans une frame — l'économie (Phase 4) ne doit jamais sauter une production |
| `GameClockConfig` | réglages éditables | ScriptableObject ; convertit vers `GameClockSettings`, même pattern que `GalaxyConfig` |
| `GameClockDebugPanel` | contrôle temporaire | boutons tactiles Pause/Normal/Rapide/Très rapide/Maximum en IMGUI, pilotant le vrai `IGameClock` — outil de mise au point, pas l'écran Paramètres final (Phase 11) |

> **Plafond de rattrapage :** si l'application est relancée après une longue mise en veille
> (deltaTime extrême), `GameClock` plafonne l'avance à 30 jours par frame plutôt que de
> geler l'application ou de publier des centaines d'événements d'un coup — un vrai scénario
> mobile, pas une précaution théorique.

### Briques de l'économie (Phase 4)

| Classe | Rôle | Choix technique |
|---|---|---|
| `ResourceBundle` | quantité des 5 ressources | 5 champs `float` nommés plutôt qu'un tableau interne : un `readonly struct` contenant un tableau ne serait pas réellement immuable (copier la struct copierait la *référence* au tableau, pas son contenu) |
| `BuildingType` | définition d'un bâtiment | ScriptableObject (5 assets fournis, un par ressource) : ressource produite, coût en Credits, durée de construction, développement minimal requis |
| `BuildingInstance` | bâtiment construit/en construction | classe (pas struct) : possède un cycle de vie (`UnderConstruction` → `Completed`) muté par `EconomyService` au fil des jours |
| `IEconomyService` / `EconomyService` | trésor et actions économiques | s'abonne à `DayAdvancedEvent` (Phase 3) ; production quotidienne calculée depuis les stats déjà posées en Phase 2 (population, richesse, développement, stabilité, gisements) |
| `EconomyController` | composition dans la scène | résout ses dépendances (`IEventBus`, `IGameClock`, `GalaxyMap`) dans `Start`, pas `Awake` : Unity garantit que tous les `Awake` sont terminés avant le premier `Start`, ce qui évite toute course avec `GalaxyMapController` sans fixer d'ordre d'exécution explicite |
| `EconomyDebugPanel` | contrôle temporaire | trésor + impôts en bas à gauche, construction/investissement du système sélectionné en bas à droite — outil de mise au point, pas l'écran final (Phase 11) |

**Formule de production journalière** (par système possédé par le joueur, voir le commentaire
de `EconomyService.ComputeSystemProduction`) :

```
Credits    = Richesse   × 0.05 × Impôts  × (×2 si gisement de Credits)
Minerais   = Population × 0.01           × (×2 si gisement de Minerais)
Énergie    = Population × 0.008          × (×2 si gisement d'Énergie)
Nourriture = Population × 0.012          × (×2 si gisement de Nourriture)
Influence  = Développement × 0.4         × (×2 si gisement d'Influence)

  ... le tout × Stabilité (un système instable produit moins de tout)
  + la production des bâtiments achevés (mise à l'échelle par la stabilité, pas par le gisement)
```

Valeurs de départ raisonnables, explicitement destinées à être affinées en Phase 12
(équilibrage) — le principe (population → ressources physiques, richesse → Credits,
développement → Influence, gisement = bonus ×2, instabilité = pénalité globale) est ce qui
compte pour l'instant.

---

## 4. Tester la Phase 1

**Au lancement (Play sur `Bootstrap.unity`)** — la console doit afficher, sans aucune
erreur ni warning :

```
[Bootstrap] Configuration appliquee (cible : 60 FPS).
[Bootstrap] 4 services enregistres.
[FSM] Entree dans BootState
[FSM] Sortie de BootState
[FSM] Entree dans MainMenuState - le socle est operationnel.
```

**Tests unitaires** — `Window → General → Test Runner → EditMode → Run All`.
Voir §5 pour le compte total (155 tests, tous packages confondus).

**Build** — `File → Build Settings` : Android et iOS doivent être sélectionnables,
avec `Bootstrap` en scène 0.

---

## 5. Tester les Phases 2-4 — galaxie, horloge et économie

**Ouvrir `Assets/Scenes/GalaxyMap.unity` et appuyer sur Play.** La console doit afficher,
sans erreur ni warning :

```
[Bootstrap] Configuration appliquee (cible : 60 FPS).
[Bootstrap] 4 services enregistres.
[FSM] Entree dans BootState
[FSM] Sortie de BootState
[FSM] Entree dans MainMenuState - le socle est operationnel.
[GalaxyMap] Galaxie generee : 100 systemes, ~125 routes hyperspatiales.
[Economy] Systeme d'origine attribue au joueur : <nom du systeme>.
[Economy] Demarree avec 5 types de batiments disponibles.
```

Dans la fenêtre Game :
- **100 points colorés** (plus clairs = plus développés) répartis dans un disque, reliés par
  un réseau de fines lignes (routes hyperspatiales).
- **Glisser** (clic maintenu + déplacer, ou glisser au doigt) déplace la caméra ; **molette**
  (éditeur) ou **pincement à deux doigts** (mobile) zoome, avec des bornes qui empêchent de
  sortir de la galaxie ou de zoomer à l'infini.
- **Toucher un système** (tap bref, sans glisser) affiche son détail dans l'encart en haut à
  gauche : nom, population, richesse, développement, stabilité, propriétaire (« 0 » pour le
  système d'origine du joueur, « Independant » pour tous les autres — les empires IA arrivent
  en Phase 5), gisements, nombre de routes. Toucher le fond vide referme l'encart.
- **En haut à droite**, un second encart affiche la date courante (format `0001-01-02`) et
  la vitesse. Avec des réglages par défaut, un jour de jeu s'écoule toutes les 2 secondes
  réelles. Boutons : **Pause/Lecture**, **Normal**, **Rapide** (x2), **Très rapide** (x4),
  **Maximum** (x8).
- **En bas à gauche**, le trésor du joueur (5 ressources) et le taux d'imposition courant
  (25% par défaut), avec des boutons **-10%/+10%**. Les 5 valeurs doivent augmenter chaque
  jour de jeu écoulé (visible en accélérant la vitesse).
- **Touchez le système d'origine du joueur** (le seul avec propriétaire « 0 ») : un troisième
  encart apparaît en bas à droite avec un bouton **Investir** (augmente le développement,
  coût croissant) et un bouton par type de bâtiment (Extracteur de Minerai, Centrale
  Énergétique, Complexe Agricole, Place de Marché, Centre Culturel). Un bâtiment déjà
  construit affiche « (construit) » et devient inactif ; sa production doit apparaître dans
  le trésor une fois sa durée de construction écoulée.

> Ces trois encarts sont des outils de mise au point temporaires (IMGUI), pas les écrans
> finaux (Phase 11) — voir les commentaires de `GalaxyMapController`, `GameClockDebugPanel`
> et `EconomyDebugPanel`.

**Tests unitaires** (inclus dans le Run All du Test Runner, 155 au total) :
`GalaxyGeneratorTests`, `GalaxyMapTests`, `HyperlaneLinkTests`, `StarSystemNameGeneratorTests`
(Phase 2) ; `GameDateTests`, `GameClockSettingsTests`, `GameClockTests` (Phase 3) ;
`ResourceBundleTests` (arithmétique de ressources), `EconomyServiceTests` (Phase 4 — formule
de production vérifiée valeur par valeur, effet des gisements et de la stabilité, cycle
complet construction → achèvement → production, impôts, investissement, tous les cas
d'erreur des actions joueur).

**Points à vérifier en priorité sur appareil réel** — la partie la plus délicate à garantir
sans pouvoir ouvrir l'éditeur ici :
- le geste de pincement (`GalaxyCameraController`, API `EnhancedTouch`) et la distinction
  tap/glisser (`GalaxySelectionController`) ;
- que les boutons des trois panneaux IMGUI répondent bien au tactile (traduit automatiquement
  par Unity sur Android/iOS, mais un point à confirmer sur appareil).

La logique de génération de galaxie, celle de l'horloge/calendrier, et la formule de
production économique (y compris le cycle construction → achèvement) ont chacune été
recoupées indépendamment par un script Python qui reproduit l'algorithme : voir les
commentaires de `GalaxyGenerator`, `GameClock` et `EconomyService` pour le détail.

---

## 6. Feuille de route

| Phase | Contenu | État |
|---|---|---|
| 1 | Socle technique : services, événements, machine à états, configuration | ✅ terminée |
| 2 | Carte galactique : 100 systèmes, génération procédurale, caméra tactile, sélection | ✅ terminée |
| 3 | Horloge de jeu : temps continu, pause, vitesses | ✅ terminée |
| 4 | Économie : production, bâtiments, impôts, investissement | ✅ terminée |
| 5 | Empires et IA de base (personnalités, colonisation) | à venir |
| 6 | Armées et résolution automatique des combats | à venir |
| 7 | Diplomatie (alliances, traités, embargos, ultimatums...) | à venir |
| 8 | Recherche (arbre technologique, 7 domaines) | à venir |
| 9 | Espionnage (agents, sabotage, vol de technologie) | à venir |
| 10 | Sauvegarde JSON automatique | à venir |
| 11 | Interface complète (menu, écrans de gestion, HUD) | à venir |
| 12 | Équilibrage | à venir |

Chaque phase est développée, testée et validée avant de passer à la suivante. Un seul
système complexe à la fois (consigne du brief) : la Phase 4 n'a touché ni la diplomatie, ni
la recherche, ni les armées — l'économie ne connaît qu'un seul propriétaire (le joueur), en
attendant que la Phase 5 généralise `OwnerId` à des empires IA.

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
