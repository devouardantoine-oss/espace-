# ESPACE — MVP de stratégie mobile en science-fiction

Prototype de jeu de stratégie mobile inspiré des grandes campagnes type *Total War*,
transposé dans un univers de science-fiction original (aucun élément sous licence tierce).

- **Moteur :** Unity 6 (`6000.0.x`) · Universal Render Pipeline
- **Langage :** C# — identifiants en anglais, documentation en français
- **Cibles :** Android · iOS
- **Vue :** 3D, caméra perspective inclinée (vue 3/4)

> **Statut : Phase 1 terminée** — fondations du projet. Aucun gameplay n'est encore implémenté.

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
├── Scenes/Bootstrap.unity        # scène principale : caméra, lumière, [GameBootstrap]
├── Settings/                     # assets URP (générés par le script de setup)
├── ScriptableObjects/            # instances de données éditables
│   └── GameConfig.asset
├── Scripts/
│   ├── Core/                     # → Espace.Core     (aucune dépendance sortante)
│   ├── Data/                     # → Espace.Data     (ScriptableObjects)
│   ├── Managers/                 # → Espace.Managers (composition de l'application)
│   ├── Gameplay/{Galaxy,Buildings,Units,Battle,AI}/   # Phases 2 → 7
│   ├── UI/                       # Phase 9
│   └── Editor/                   # → Espace.Editor   (outillage, exclu des builds)
└── Tests/EditMode/               # → Espace.Tests.EditMode
```

### Direction des dépendances

```
Espace.Core  ←  Espace.Data  ←  Espace.Managers
      ↖               ↖               ↖
       ╰───────── Espace.Editor / Espace.Tests.EditMode
```

Les *Assembly Definitions* rendent cette direction **vérifiée par le compilateur** :
`Espace.Core` ne peut pas référencer `Espace.Managers`, même par accident. Bénéfice
secondaire : modifier l'UI ne recompile pas le cœur du jeu.

> Les assemblies `Espace.Gameplay` et `Espace.UI` seront ajoutées avec leurs premiers
> scripts (Phase 2 et Phase 9). Créer un `.asmdef` sans code produirait une référence
> vide inutile.

### Briques du socle

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
35 tests couvrent `ServiceLocator`, `EventBus`, `GameStateMachine` et `ObjectPool`,
y compris le cas réentrant de la machine à états.

**Build** — `File → Build Settings` : Android et iOS doivent être sélectionnables,
avec `Bootstrap` en scène 0.

---

## 5. Feuille de route

| Phase | Contenu | État |
|---|---|---|
| 1 | Projet, architecture, Git, scène principale | ✅ terminée |
| 2 | Caméra, carte galactique, 8 planètes | à venir |
| 3 | Ressources (Crédits, Minerai), économie | à venir |
| 4 | Bâtiments (Centre de commandement, Mine, Caserne, Usine) | à venir |
| 5 | Armées (Infanterie, Commandos, Chars, Artillerie) | à venir |
| 6 | IA | à venir |
| 7 | Bataille terrestre | à venir |
| 8 | Sauvegarde JSON | à venir |
| 9 | Interface complète | à venir |
| 10 | Équilibrage | à venir |

Chaque phase est développée, testée et validée avant de passer à la suivante.

---

## 6. Conventions

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
