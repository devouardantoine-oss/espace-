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

> **Statut : Phase 20 terminée — flottes, fiche de système et offensives.** Carte galactique (100 systèmes),
> horloge de jeu, économie, 6 empires (1 joueur + 5 IA dont chacune **gère l'intégralité de son
> territoire** et s'étend au-delà de ses voisins immédiats, avec un rayon d'expansion propre à
> sa personnalité), armées (sept types d'unités dont quatre classes de vaisseaux —
> Chasseurs, Frégate, Croiseur, Cuirassé —, flottes nommées et commandées chacune par un Amiral
> aux bonus/malus propres, plafonnées à 10 unités, nombre de flottes en campagne simultanée lié
> à la recherche en Logistique, recrutement, résolution automatique des combats, une
> colonisation stratégique où l'Infanterie est indispensable — pour s'installer sur un système
> libre comme pour occuper un système conquis —, et des **déplacements vers n'importe quelle
> destination de la galaxie**, l'itinéraire étant calculé le long des routes hyperspatiales et
> la durée dépendant de la distance parcourue, avec des **rencontres spatiales** en cours de
> route où le joueur choisit lui-même l'issue — combattre, se replier, négocier, commercer,
> pirater ou passer son chemin selon le statut diplomatique), diplomatie (guerre/paix/alliances/pactes de
> non-agression, opinion, traités commerciaux, embargos, ultimatums, échanges de ressources et
> de territoires), recherche (7 domaines, 3 paliers chacun, bonus sur la production, le combat,
> la vitesse des flottes et les gains d'opinion), espionnage (cinq missions déterministes selon
> un rapport de puissance), une sauvegarde JSON automatique (la partie reprend exactement où
> elle en était après une fermeture ou une mise en arrière-plan, sur un seul fichier local), une
> interface complète (menu principal, barre d'état permanente, panneau de système contextuel,
> fenêtre de gestion à onglets — dont un onglet Flottes — et menu pause), une carte galactique
> immersive (fond spatial procédural, systèmes stylés, **zones d'influence continues découpées
> en cellules de contrôle, aux frontières soulignées uniquement au contact d'un autre empire, et
> nom de faction posé au cœur de chaque territoire**, noms/détails affichés selon le niveau de
> zoom), et un écran de choix en début de partie : le
> joueur choisit librement sa faction parmi les 6 disponibles, puis son système de départ parmi
> les emplacements que l'algorithme de placement proposerait — ce n'est plus toujours la même
> faction sur le même système le plus proche du centre. La génération de la galaxie (et son
> fond) est déterministe (graine fixe) pour que la même galaxie réapparaisse d'une session à
> l'autre.
>
> **Refonte V1 en cours (inspirée de Star Wars dans ses mécaniques, avec des noms 100%
> originaux — voir la feuille de route §6) :** les Phases 12 à 18 remplacent l'ancienne
> Phase 12 « Équilibrage » et couvrent une refonte étendue demandée après les premiers essais
> du jeu — carte immersive (Phase 12), choix de faction/système de départ (Phase 13), refonte
> des flottes (Phase 14), amiraux (Phase 15), colonisation stratégique (Phase 16), déplacement
> longue distance avec rencontres spatiales (Phase 17) et IA dynamique ajustée à toutes ces
> nouvelles règles (Phase 18, ci-dessus) : **la refonte est terminée**. Un seul système complexe
> à la fois, de la Phase 1 à la Phase 18.

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
| Build Settings | `Bootstrap.unity` en scène 0, `GalaxyMap.unity` en scène 1 (indispensable : le jeu la charge par son nom, une scène absente de cette liste n'existe pas dans une application compilée) |
| Orientation | paysage uniquement (Android et iOS) : la fenêtre de gestion fait 660 unités de large, en portrait l'échelle de l'interface devrait se brider pour la faire tenir et les boutons redeviendraient trop petits pour un doigt |
| Android | IL2CPP, ARM64, min SDK 26 (Unity 6 refuse toute valeur inférieure) |
| iOS | IL2CPP, cible minimale 13.0 |

**Pourquoi un script plutôt que des `ProjectSettings/*.asset` versionnés ?** Ces fichiers
YAML dépendent de la version de sérialisation exacte de l'éditeur ; un champ obsolète
suffit à empêcher l'ouverture du projet. Les API officielles donnent un résultat valide
quelle que soit la version d'Unity 6 installée.

> Le passage à l'Input System **impose un redémarrage de l'éditeur**. Acceptez la
> proposition d'Unity, ou fermez et rouvrez le projet.
>
> **Ce réglage est vérifié après écriture.** `AssetDatabase.SaveAssets` ne couvre que le dossier
> `Assets/` : `ProjectSettings.asset` n'est réécrit sur le disque qu'à la fermeture de l'éditeur
> ou sur `File → Save Project`. Sans cet appel explicite, la valeur restait en mémoire, semblait
> appliquée, puis disparaissait — et la compilation Android échouait sur
> « *Active Input Handling is set to Both* » après cent secondes, alors que le script venait
> d'annoncer sa réussite. Le script relit désormais la valeur et journalise une erreur explicite
> si elle n'a pas pris.
>
> Le script journalise aussi, à la fin, les quatre réglages Android relus (backend,
> architectures, SDK minimal, orientation) : ce sont ceux dont l'erreur ne se manifeste
> qu'après un build complet, ou une fois l'application installée sur le téléphone.

L'opération est idempotente : la relancer ne crée aucun doublon.

---

## 3. Architecture

```
Assets/
├── Scenes/
│   ├── Bootstrap.unity           # scène de démarrage : caméra, lumière, [GameBootstrap]
│   └── GalaxyMap.unity           # scène jouable : galaxie + horloge + économie + empires + armées + diplomatie + recherche + espionnage + sauvegarde (Phases 2-10)
├── Settings/                     # assets URP (générés par le script de setup)
├── ScriptableObjects/            # instances de données éditables
│   ├── GameConfig.asset
│   ├── GameClockConfig.asset
│   ├── GalaxyConfig.asset        # graine fixe depuis la Phase 10 (voir §3, Briques de la sauvegarde)
│   ├── Buildings/                # 5 types de bâtiments (1 par ressource)
│   ├── Empires/                  # 6 empires : le joueur + 1 par personnalité IA
│   ├── Units/                    # 7 types d'unités (Infanterie, Blindés, Forces spéciales, Chasseurs, Frégate, Croiseur, Cuirassé — Phase 14)
│   └── Research/                 # 21 paliers de recherche (3 x 7 domaines)
├── Scripts/
│   ├── Core/                     # → Espace.Core     (aucune dépendance sortante)
│   ├── Data/                     # → Espace.Data     (ScriptableObjects et types génériques)
│   ├── Managers/                 # → Espace.Managers (composition de l'application)
│   ├── Gameplay/                 # → Espace.Gameplay (référence Core + Data)
│   │   ├── Galaxy/               #     carte galactique, génération, caméra, sélection, itinéraires hyperspatiaux
│   │   ├── Economy/              #     production, bâtiments, impôts, investissement
│   │   ├── Empires/              #     identité, personnalités, territoire, décisions IA autonomes
│   │   ├── Military/             #     unités, flottes, amiraux, combat automatique, colonisation, déplacement, rencontres
│   │   ├── Diplomacy/            #     statut guerre/paix/alliance, opinion, propositions
│   │   ├── Research/             #     domaines, paliers, points, bonus par domaine
│   │   ├── Espionage/            #     missions déterministes, puissance/contre-espionnage
│   │   └── Save/                 #     capture/restauration JSON de l'état mutable
│   ├── UI/                       # → Espace.UI       (HUD, fenêtre de gestion, menus — Phase 11)
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
> `BuildingType`, `EmpireDefinition`) vivent dans `Espace.Gameplay`, pas dans `Espace.Data`.
> Seuls les types véritablement transverses (`ResourceType`, `ResourceBundle`, `GameConfig`)
> restent dans `Espace.Data`.
>
> L'assembly `Espace.UI` (Phase 11) référence `Espace.Gameplay` mais jamais l'inverse : l'UI
> lit et pilote les services de gameplay, aucun service de gameplay ne connaît l'UI.

> **Empires vs. personnalités : donnée contre comportement.** `EmpireDefinition` (nom,
> couleur, personnalité, joueur ou non) est un `ScriptableObject` — du contenu qu'un game
> designer ajuste sans toucher au code, même pattern que `BuildingType`/`GalaxyConfig`.
> `EmpirePersonalityProfile`, à l'inverse, est une table statique **dans le code** : une
> personnalité est un comportement de décision, pas un nombre à éditer dans l'inspecteur.
> Distinction volontaire entre les deux, détaillée dans le commentaire de la classe.

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
| `GameClockDebugPanel` *(retiré en Phase 11)* | contrôle temporaire | boutons tactiles Pause/Normal/Rapide/Très rapide/Maximum en IMGUI — remplacé par la barre `HudController`, qui pilote le même `IGameClock` |

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
| `EconomyDebugPanel` *(retiré en Phase 11)* | contrôle temporaire | trésor + impôts en bas à gauche, construction/investissement du système sélectionné en bas à droite — remplacé par `HudController` (trésor/impôts) et `SystemInfoPanelController` (construction/investissement) |

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

### Briques des empires et de l'IA (Phase 5)

| Classe | Rôle | Choix technique |
|---|---|---|
| `Empire` | identité d'un empire | immuable, **sans territoire stocké** : « quels systèmes possède cet empire » reste dérivé à la volée de `StarSystemState.OwnerId` — un choix qui s'est révélé payant dès que la Phase 6 a rendu la colonisation possible, sans rien avoir à changer à `Empire` |
| `EmpirePersonality` / `EmpireDefinition` | contenu | 5 personnalités (Pacifique, Expansionniste, Commerçante, Militaire, Opportuniste) ; `EmpireDefinition` est un ScriptableObject — 6 assets fournis (le joueur + une IA par personnalité) |
| `EmpireFactory` | attribution des identifiants | trouve la définition marquée joueur (`EconomyService.PlayerOwnerId = 0`), attribue 1..5 aux IA — fonction pure, testable sans scène |
| `EmpirePlacement` | systèmes d'origine | *farthest-point sampling* déterministe : le 1er (joueur) est le plus proche du centre, chaque suivant maximise sa distance minimale aux origines déjà choisies — disperse les 6 empires sans recourir à l'aléatoire |
| `EmpireRegistry` | registre passif | même rôle que `GalaxyMap` pour les systèmes : accès O(1) par identifiant, aucune simulation |
| `EmpirePersonalityProfile` | comportement | table **dans le code**, pas un asset (voir l'encart plus haut) : taux d'imposition préféré, ordre de priorité de construction par ressource, marge de prudence avant investissement — l'Opportuniste déroge à l'ordre fixe et choisit le moins cher disponible |
| `AIDecisionMaker` | décision IA | fonction statique testable sans `ServiceLocator` : impôts réaffirmés, puis **une seule** action par appel (une construction, sinon un investissement) — jamais les deux, jamais plusieurs bâtiments d'un coup |
| `EmpireController` / `AIController` | orchestration | fils minces ; `AIController` résout `EmpireRegistry`/`IEconomyService` **paresseusement dans son gestionnaire d'événement** plutôt que dans `Start`, pour éviter toute course avec `EmpireController.Start` (deux `Start` sans ordre garanti entre eux) |
| `EmpireDebugPanel` *(retiré en Phase 11)* | contrôle temporaire | liste les 6 empires (nom, personnalité, systèmes, Credits) — remplacé par l'onglet Empires de `ManagementWindowController` |

**`EconomyService` généralisé à plusieurs trésors.** Depuis la Phase 4, la classe était déjà
écrite pour retrouver le propriétaire d'un système plutôt que de coder « le joueur » en dur.
La Phase 5 exploite exactement cela : un dictionnaire par empire remplace le trésor unique,
`TryStartConstruction`/`TryInvestInDevelopment` fonctionnent sans modification pour n'importe
quel empire, et `Treasury`/`TaxRate`/`SetTaxRate(rate)` restent des raccourcis vers le joueur
— `EconomyDebugPanel` (Phase 4) continue de marcher **sans une seule ligne changée**, la
meilleure preuve que la généralisation est correcte.

> **Colonisation : reportée à la Phase 5, réalisée en Phase 6.** Sans flottes, coloniser
> aurait été un mécanisme abstrait de plus à réécrire une fois les flottes disponibles — la
> Phase 5 s'est donc limitée à ce qui ne dépendait d'aucun système futur (gestion économique
> autonome). Les flottes existent maintenant : voir la table ci-dessous.

### Briques des armées et du combat (Phase 6)

| Classe | Rôle | Choix technique |
|---|---|---|
| `UnitType` / `UnitTypeDefinition` | contenu | 7 types depuis la Phase 14 (Infanterie, Blindés, Forces spéciales, Chasseurs, Frégate, Croiseur, Cuirassé — remplace l'ancienne « Flotte spatiale » générique), ScriptableObject — puissance, vitesse, coûts, durée de recrutement, entretien, développement minimal requis, description de rôle |
| `UnitBundle` | quantité d'unités | même pattern que `ResourceBundle` (Phase 4) : 7 champs `int` nommés (depuis la Phase 14, 4 à l'origine) plutôt qu'un tableau, pour la même raison d'immuabilité réelle |
| `Fleet` | groupe d'unités | stationnée (garnison d'un système) ou en déplacement ; **au plus une flotte stationnée par (système, propriétaire)** — toute arrivée fusionne avec la garnison existante, ce qui évite à la résolution de combat de devoir combiner plusieurs flottes du même camp |
| `CombatResolver` | résolution de bataille | **déterministe, sans hasard** : la puissance de chaque camp (quantité × puissance du catalogue, modulée par moral/commandement/terrain) décide du vainqueur ; la fraction de pertes de chaque camp est proportionnelle à la puissance adverse relative au total — testable sans stub de générateur aléatoire |
| `IMilitaryService` / `MilitaryService` | armées de tous les empires | même architecture que `EconomyService` : recrutement en file (mirroring `BuildingInstance`), entretien journalier prélevé via `IEconomyService.TrySpend` (nouvelle méthode générique, réutilisée aussi par la construction/l'investissement pour éviter de dupliquer la logique de dépense) |
| `MilitaryDecisionMaker` | décision militaire IA | même séparation que `AIDecisionMaker` : recrutement jusqu'à la garnison cible, puis colonisation d'un voisin libre, puis — seulement pour les personnalités qui s'y autorisent — une attaque ; **une seule action par appel**, toujours au moins 2 unités gardées à domicile |
| `MilitaryController` | orchestration | seul composant Phase 6 à dépendre d'un autre `Start()` non garanti (`EmpireRegistry`, `IEconomyService`) : initialisation différée à `Update` plutôt qu'à un événement, faute d'événement naturel à attendre pour un service qui doit exister avant que d'autres ne le cherchent |
| `MilitaryDebugPanel` *(retiré en Phase 11)* | contrôle temporaire | recrutement et envoi de la garnison entière vers un voisin — remplacé par la section Armée de `SystemInfoPanelController` |

**Agressivité de l'IA — décision produit assumée.** Sans état de guerre/paix (Phase 7), une
attaque IA n'a pas de justification diplomatique ; le choix a été fait malgré tout de
l'activer dès cette phase pour les personnalités qui s'y prêtent (voir
`EmpirePersonalityProfileData.AggressionThreshold`) : Militariste attaque dès qu'elle a un
léger avantage (×1.1), Opportuniste seulement un net avantage (×1.6), Expansionniste
quasiment jamais (×2.5, préfère coloniser), Pacifiste et Commerçante jamais (`null`). Ce
comportement est probablement à retravailler une fois la Phase 7 introduira un véritable état
de guerre.

**Formule de combat** (voir le commentaire de `CombatResolver.Resolve`) :

```
PuissanceAttaquant = Σ(quantité × puissance du catalogue) × MoralOrigine × Commandement
PuissanceDefenseur = Σ(quantité × puissance du catalogue) × MoralSystème × (1 + Développement × 0.1) × Commandement

  Vainqueur = le camp de plus grande puissance (égalité stricte → défenseur)
  FractionPertesAttaquant = PuissanceDéfenseur / (PuissanceAttaquant + PuissanceDéfenseur)
  FractionPertesDéfenseur = PuissanceAttaquant / (PuissanceAttaquant + PuissanceDéfenseur)
```

« Moral » approximé par la stabilité du système (celui d'origine pour l'attaquant, celui
attaqué pour le défenseur) ; « terrain » par le niveau de développement du système défendu
(fortifications) ; « commandement » par la personnalité de l'empire (seul le Militariste a un
bonus, ×1.15) **et, depuis la Phase 8, par le bonus de recherche Armement de chaque empire**
(voir plus bas). « Ravitaillement » reste implicitement favorable tant que les déplacements
sont limités aux voisins directs (pas de calcul d'itinéraire multi-sauts en v1) ; le domaine
de recherche Logistique accélère désormais ces déplacements plutôt que d'agir sur le combat
lui-même. « Technologie » n'est donc plus un facteur neutre depuis la Phase 8 : il se
décompose entre les bonus des domaines Armement (puissance) et Logistique (vitesse).

**En cas de défaite, l'attaquant survivant se replie** vers son système d'origine plutôt que
d'être systématiquement anéanti : plus lisible à observer, et une défaite reste réversible
plutôt que définitivement punitive pour une IA qui aurait mal évalué ses chances.

### Briques de la diplomatie (Phase 7)

| Classe | Rôle | Choix technique |
|---|---|---|
| `DiplomaticStatus` | statut symétrique | `Peace` / `War` / `Alliance` / `NonAggressionPact`, un seul par paire d'empires — traité commercial et embargo restent des relations **indépendantes** (voir plus bas), une vraie diplomatie les superpose plutôt que de les fondre dans un seul statut |
| `ProposalType` / `DiplomaticProposal` | proposition | 7 types (Alliance, Pacte de non-agression, Traité commercial, Traité de paix, Échange de ressources, Échange de territoires, Ultimatum) ; une seule classe de proposition avec une charge utile optionnelle (ressources/systèmes) plutôt qu'une hiérarchie polymorphe — volontairement simple pour le périmètre du MVP |
| `IDiplomacyService` / `DiplomacyService` | diplomatie de tous les empires | même architecture que `MilitaryService`/`EconomyService` ; statut stocké par paire non ordonnée, **opinion stockée par paire dirigée** (l'opinion de A envers B peut différer de celle de B envers A) |
| `ProposalEvaluator` | acceptation d'une proposition | fonction pure, même esprit que `CombatResolver` : une IA cible accepte selon l'opinion qu'elle a du proposeur (pactes), le rapport de puissance (paix, ultimatum), ou l'équité de l'échange (ressources) |
| `DiplomacyDecisionMaker` | décision diplomatique IA | même séparation que `MilitaryDecisionMaker` : propose la paix si le rapport de force devient défavorable, sinon déclare la guerre à un voisin écrasé (personnalités agressives uniquement), sinon propose un pacte de non-agression (ou une alliance) à un voisin apprécié ; **une seule action par appel** |
| `DiplomacyController` | orchestration | s'initialise **avant** `MilitaryController` (qui dépend désormais de `IDiplomacyService`) sans jamais dépendre en retour de `IMilitaryService` au constructeur — `DiplomacyService` le résout paresseusement via `ServiceLocator` pour éviter un cycle d'attente mutuelle entre les deux contrôleurs |
| `DiplomacyDebugPanel` *(retiré en Phase 11)* | contrôle temporaire | statut/opinion envers chaque IA avec boutons d'action (guerre, pacte, alliance, paix, rupture), et les propositions reçues en attente (accepter/refuser) ; échanges de ressources/territoires et ultimatums non exposés en bouton (charge utile numérique peu adaptée à l'IMGUI tactile) mais entièrement implémentés et testés au niveau du service — remplacé par l'onglet Diplomatie de `ManagementWindowController` |

**Résolution des propositions : instantanée pour l'IA, en attente pour le joueur.** Une
proposition dont la cible est une IA est évaluée et résolue au moment même où elle est
soumise (`ProposalEvaluator`, appelé par `DiplomacyService`) — pas de file d'attente pour des
décisions qui n'ont pas besoin d'attendre une saisie humaine. Une proposition qui cible le
joueur est mise en attente et publiée via `ProposalReceivedEvent`, jusqu'à ce que
`TryRespondToProposal` soit appelée (bouton de l'onglet Diplomatie, `ManagementWindowController`).

**Le combat conditionné à la guerre.** `MilitaryService.TryMoveFleet` refuse désormais tout
déplacement vers un système possédé par un autre empire tant qu'un état de
`DiplomaticStatus.War` n'a pas été déclaré entre les deux — avant cette phase, n'importe quel
empire pouvait attaquer n'importe quel voisin sans justification diplomatique (limitation du
combat automatique de la Phase 6, explicitement signalée à l'époque). Le choix de *qui*
attaquer (seuil d'agressivité, rapport de puissance) est passé de `MilitaryDecisionMaker` à
`DiplomacyDecisionMaker`, appelé juste avant dans `AIController` (ordre **Économie → Diplomatie
→ Armée**) : une guerre déclarée ce mois-ci peut donc être exploitée par l'armée ce même mois.
La colonisation d'un système non possédé reste, elle, entièrement libre.

**Un ultimatum refusé déclare automatiquement la guerre.** `Ultimatum` est la seule proposition
à effet asymétrique en cas de refus : la cible qui refuse de payer le tribut demandé s'expose à
une déclaration de guerre immédiate et automatique du proposeur — pas de round supplémentaire
de négociation, cohérent avec le sens réel du mot.

**Opinion et revenu commercial dérivent chaque mois.** Sur `MonthAdvancedEvent`, chaque opinion
déjà enregistrée dérive vers une valeur cible dépendant du statut courant (guerre : très
négative, alliance : très positive, pacte : positive, paix simple : neutre) — pas de dérive
brutale, un point par mois (`Mathf.MoveTowards`). Un traité commercial actif crédite un petit
revenu passif aux deux partenaires chaque mois, via le nouveau `IEconomyService.Grant`
(contrepartie inconditionnelle de `TrySpend`, introduite pour tout gain hors production
journalière : échanges de ressources, tribut d'ultimatum, revenu commercial).

### Briques de la recherche (Phase 8)

| Classe | Rôle | Choix technique |
|---|---|---|
| `ResearchDomain` | contenu | 7 domaines du brief (Économie, Industrie, Armement, Énergie, Diplomatie, Espionnage, Logistique), chacun lié à un système précis qu'il améliore — sauf Espionnage, banqué pour la Phase 9 |
| `TechnologyDefinition` | contenu | ScriptableObject, même pattern que `BuildingType`/`UnitTypeDefinition` : nom, domaine, palier (1 à 3), coût en points de recherche, bonus apporté ; 21 assets au total (3 paliers × 7 domaines) |
| `IResearchService` / `ResearchService` | recherche de tous les empires | même architecture que `EconomyService`/`MilitaryService` ; génère des points chaque jour (population et développement des systèmes possédés, même forme que la production d'Influence) crédités au **domaine actif** choisi par l'empire — un seul à la fois, changer de domaine ne fait perdre aucune progression déjà acquise |
| `ResearchDecisionMaker` | décision de recherche IA | même séparation que les autres : si le domaine actif est encore en cours, ne change rien ; sinon, choisit le premier domaine non maximal dans l'ordre de préférence de la personnalité (`EmpirePersonalityProfileData.ResearchPriority`) |
| `ResearchController` | orchestration | seul contrôleur de Phase 7-8 à s'initialiser dans `Start` plutôt qu'`Update` : aucune dépendance à un autre `Start` de la scène (juste `GalaxyMap` et `IEventBus`, disponibles dès l'`Awake`) |
| `ResearchDebugPanel` *(retiré en Phase 11)* | contrôle temporaire | domaine actif, palier et bonus courants des 7 domaines, bouton pour rediriger le focus — remplacé par l'onglet Recherche de `ManagementWindowController` |

**Bonus appliqués sans dépendance de construction, par résolution paresseuse.** `EconomyService`,
`MilitaryService` et `DiplomacyService` résolvent `IResearchService` via `ServiceLocator` au
moment où ils en ont besoin (même précédent que `DiplomacyService` résolvant
`IMilitaryService` en Phase 7) plutôt que de le recevoir au constructeur — sans quoi
`ResearchController` et les autres contrôleurs devraient s'attendre mutuellement. Le bonus
vaut 0 (aucun effet) tant que la recherche n'est pas disponible ou que rien n'a encore été
recherché : **tous les tests des phases précédentes restent valides sans aucune modification**,
la meilleure preuve que cette intégration est correctement rétrocompatible.

**Où va chaque domaine :**

```
Économie   → +bonus% sur la production de Credits (EconomyService)
Industrie  → +bonus% sur la production de Minerais (EconomyService)
Énergie    → +bonus% sur la production d'Énergie (EconomyService)
Armement   → +bonus% sur la puissance de combat, attaquant et défenseur (MilitaryService)
Logistique → +bonus% sur la vitesse des flottes, donc des trajets plus courts (MilitaryService)
Diplomatie → +bonus% sur les gains d'opinion d'une proposition acceptée, jamais sur les
             pénalités (DiplomacyService) — la recherche rend plus convaincant, elle
             n'atténue pas la colère qu'on suscite
Espionnage → +bonus% sur la puissance et le contre-espionnage (EspionageService, Phase 9)
```

Nourriture et Influence n'ont volontairement aucun domaine associé : le brief n'en compte que
7, et forcer une correspondance aurait dilué le sens de chacun.

### Briques de l'espionnage (Phase 9)

| Classe | Rôle | Choix technique |
|---|---|---|
| `EspionageMissionType` | contenu | 5 missions du brief (vol de technologie, sabotage, découverte d'armées, influence de gouvernement, incitation à la révolte) |
| `IEspionageService` / `EspionageService` | espionnage de tous les empires | **déterministe, sans hasard**, même philosophie que `CombatResolver` : une mission réussit si et seulement si la puissance d'espionnage du proposeur dépasse strictement le contre-espionnage de la cible (égalité stricte → échec) ; aucune adjacence requise (l'espionnage est distant, à la différence des flottes) |
| `EspionageDecisionMaker` | décision d'espionnage IA | même séparation que les autres : seuil de puissance et mission préférée propres à chaque personnalité, une seule mission réussie par appel |
| `EspionageController` | orchestration | comme `ResearchController`, s'initialise dans `Start` sans dépendance à un autre contrôleur — `EspionageService` résout lui-même, paresseusement, l'économie, l'armée, la diplomatie et la recherche au moment où une mission en a besoin |
| `EspionageDebugPanel` *(retiré en Phase 11)* | contrôle temporaire | puissance d'espionnage du joueur, un bouton par mission pour chaque IA, dernier résultat de découverte d'armées — remplacé par l'onglet Espionnage de `ManagementWindowController` |

**Le risque vient de la découverte, pas du hasard.** Une mission réussie est invisible pour la
cible ; une mission ratée est toujours découverte et inflige une pénalité d'opinion au
proposeur (`IDiplomacyService.ApplyOpinionShift`, nouvelle méthode réservée à ce genre d'effet
qui n'entre dans aucune mécanique diplomatique existante). Le coût d'une mission (Credits) est
payé qu'elle réussisse ou non — lancer une mission perdue d'avance a donc un vrai prix.

**Chaque mission, un effet distinct :**

```
Vol de technologie      → copie instantanément (IResearchService.GrantTier, sans coût de
                           points) le domaine où la cible a la plus grande avance sur le
                           proposeur — échoue si aucun domaine n'est en avance
Sabotage                 → réduit d'un niveau le développement du système ciblé
Incitation à la révolte  → réduit fortement la stabilité du système ciblé (bornée à 0)
Influence de gouvernement → améliore secrètement l'opinion de la cible envers le proposeur
Découverte d'armées      → révèle la garnison réelle d'un système (IMilitaryService),
                           purement informatif pour le joueur — l'IA voit déjà tout
```

**Pourquoi chaque personnalité a sa mission de prédilection.** Le Pacifiste n'espionne jamais
(seuil nul, cohérent avec son refus de toute confrontation même déguisée) ; la Commerçante vole
des technologies ; le Militariste découvre les armées adverses avant de frapper ; l'Expansionniste
sabote pour affaiblir avant d'envahir ; l'Opportuniste, seuil le plus bas du groupe, influence
les gouvernements — le moyen le plus discret.

### Briques de la sauvegarde (Phase 10)

| Classe | Rôle | Choix technique |
|---|---|---|
| `GameSaveData` | contenu | arbre de classes `[Serializable]` **à plat**, compatible `JsonUtility` (qui ne sérialise ni dictionnaires, ni `Nullable`, ni références de `ScriptableObject`) — construit et lu uniquement par `SaveService`, jamais par les services eux-mêmes |
| `ISaveService` / `SaveService` | sauvegarde de toute la partie | capture l'état via les interfaces publiques déjà existantes de chaque service, écrit/lit un fichier JSON unique (`Application.persistentDataPath`) ; ne sauvegarde que l'état **mutable** (propriétaire, trésor, garnisons, relations, progression) — jamais le contenu régénérable (galaxie, roster d'empires) |
| `SaveController` | orchestration | le plus grand nombre de dépendances de tous les contrôleurs (tous les services de gameplay) ; comme rien ne dépend de lui en retour, il peut se permettre d'attendre patiemment (sondage `Update`, comme `MilitaryController`) que tout le reste soit prêt |
| `SaveDebugPanel` *(retiré en Phase 11)* | contrôle temporaire | état du fichier, boutons Sauvegarder maintenant / Recharger — remplacé par l'onglet Sauvegarde de `ManagementWindowController` (et par « Sauvegarder maintenant »/« Recharger » du menu pause) |

**Une galaxie enfin déterministe.** Jusqu'à la Phase 9, `GalaxyConfig.seed` valait `0`, tirant
une galaxie différente à chaque lancement (positions, noms, gisements, routes) — un choix
délibéré pour explorer des layouts variés en développement, mais incompatible avec une
sauvegarde qui référence des systèmes par simple identifiant numérique. La graine est
désormais fixée : la galaxie régénérée à chaque lancement est **strictement identique**,
donc les identifiants de système sauvegardés retrouvent toujours le même contexte visuel.

**Restaurer sans perturber : des méthodes `Restore*` dédiées, silencieuses.** Charger une
sauvegarde ne doit déclencher ni coût, ni vérification, ni notification — contrairement aux
méthodes de jeu normales (`TryDeclareWar`, `TrySetActiveDomain`...) qui publient des
événements et appliquent des règles. `IEconomyService.RestoreCompletedBuilding`,
`IMilitaryService.RestoreGarrison`, `IDiplomacyService.RestoreRelations`/`RestoreOpinion`/
`RestoreEmbargo` et `IResearchService.RestoreProgress`/`RestoreActiveDomain` écrivent
directement l'état, sans lever d'événement (seule exception pragmatique : la restauration du
trésor réutilise `Grant`, déjà sans risque sur une économie fraîchement initialisée à zéro,
plutôt que d'ajouter une cinquième méthode dédiée pour ce seul cas).

**Sauvegarde automatique, jamais de plantage sur un fichier illisible.** Une sauvegarde a lieu
chaque mois de jeu (`MonthAdvancedEvent`), et à chaque mise en arrière-plan ou fermeture de
l'application (`OnApplicationPause`/`OnApplicationQuit`) — le scénario le plus courant sur
mobile, où l'application est bien plus souvent suspendue que fermée proprement. Un fichier
absent, corrompu ou d'une version incompatible ne fait jamais planter le jeu : `TryLoadAndApply`
retourne simplement un échec explicite, journalisé, et la partie continue sur son état courant.

**Limitations v1, assumées et documentées plutôt que traitées comme des bugs :** les flottes en
transit, les commandes de recrutement en cours et les propositions diplomatiques en attente ne
sont pas sauvegardées (seules les garnisons déjà stationnées le sont). La fenêtre de risque
reste faible — sauvegarde automatique mensuelle, trajets de quelques jours, propositions
résolues quasi instantanément — à revisiter en Phase 12 si nécessaire.

### Briques de l'interface (Phase 11)

| Classe | Rôle | Choix technique |
|---|---|---|
| `UITheme` | palette et styles partagés | couleurs, `GUIStyle` et textures 1×1 mis en cache, construits paresseusement (jamais en initialiseur statique — `GUI.skin` n'est valide que dans `OnGUI`) ; évite que chaque écran invente ses propres couleurs |
| `HudFormatter` | mise en forme textuelle | seule logique **pure** de cette phase (dates, montants abrégés en k/M, pourcentages) — testable en EditMode et recoupée en Python, contrairement au reste (agencement visuel non testable sans éditeur) |
| `HudController` | barre supérieure, toujours visible | date/vitesse et trésor/impôts du joueur — fusionne `GameClockDebugPanel` et l'encart trésor d'`EconomyDebugPanel` (Phases 3/4) ; bascule les deux autres fenêtres via `GetComponent` (même GameObject `[UI]`) |
| `SystemInfoPanelController` | panneau du système sélectionné | fiche d'identité + actions (construire, investir, recruter, déplacer la garnison) si le système appartient au joueur — fusionne l'ancien encart de sélection de `GalaxyMapController`, l'encart d'actions d'`EconomyDebugPanel` et la totalité de `MilitaryDebugPanel` (Phases 2/4/6) |
| `ManagementWindowController` | fenêtre à onglets | Empires / Diplomatie / Recherche / Espionnage / Sauvegarde — remplace cinq panneaux distincts (Phases 5/7/8/9/10) par une seule fenêtre, ouverte/fermée par le bouton « Gestion » ; pas d'onglet Économie dédié (déjà couvert par `HudController` et `SystemInfoPanelController`) |
| `PauseMenuController` | menu pause | Reprendre / Sauvegarder / Recharger / Menu principal / Quitter ; met l'horloge en pause à l'ouverture et la reprend à la fermeture, **seulement** si c'est lui qui l'a mise en pause |
| `MainMenuController` | menu principal | Nouvelle partie / Continuer (actif seulement si une sauvegarde existe) / Quitter — placé directement dans la scène `Bootstrap`, donne enfin un écran réel à `MainMenuState` (qui restait un état muet depuis la Phase 1) |
| `SaveFileLocator` | chemin de la sauvegarde | extrait de `SaveService`/`SaveController` : le menu principal doit savoir si une sauvegarde existe et pouvoir la supprimer **avant** qu'aucun service de `GalaxyMap` (donc `ISaveService`) n'existe |

> **Pourquoi encore de l'IMGUI (`OnGUI`), pas UI Toolkit ni uGUI ?** Ce projet est développé
> sans accès à l'éditeur Unity. UI Toolkit (UXML/USS) et uGUI (Canvas/RectTransform) exigent
> tous deux des assets ou des scènes calibrés visuellement — une erreur ne se révèle qu'à
> l'ouverture dans l'éditeur, jamais à la compilation ni dans un test. IMGUI reste la seule
> approche entièrement exprimable en C# pur, vérifiable par la seule lecture du code et les
> tests EditMode. Les panneaux de diagnostic des Phases 2 à 10 le présentaient comme
> temporaire ; cette phase le garde mais l'élève au rang d'interface définitive, avec un thème
> partagé (`UITheme`) plutôt que des `GUI.Box` par défaut, quatre écrans consolidés plutôt que
> neuf encarts empilés, et un vrai menu principal.

> **Flux de scènes enfin réel :** `ISceneLoader`/`SceneLoaderService` existaient depuis la
> Phase 1 mais n'étaient encore jamais appelés — `GalaxyMap.unity` s'ouvrait jusqu'ici en
> Play directement, sans passer par `Bootstrap.unity`. Cette phase les met enfin en service
> (« Nouvelle partie »/« Continuer » vers `GalaxyMap`, « Menu principal » en retour vers
> `Bootstrap`) et ajoute donc pour la première fois `ProjectSettings/EditorBuildSettings.asset`
> (les deux scènes, `Bootstrap` en premier). Corollaire découvert à cette occasion :
> `GalaxyMapController` enregistrait `GalaxyMap` dans le `ServiceLocator` sans jamais s'en
> désinscrire à sa destruction (aucun aller-retour de scène n'existait encore pour le
> révéler) — corrigé au passage, sinon un « Nouvelle partie » après un retour au menu aurait
> câblé toute la nouvelle partie sur l'ancienne galaxie. `IGameClock.ResetToStart` est ajoutée
> pour la même raison : l'horloge vit dans `GameBootstrap` (`DontDestroyOnLoad`), donc une
> « Nouvelle partie » doit explicitement lui redemander de repartir de la date de début plutôt
> que de laisser filer celle de la partie précédente.

### Briques de la carte galactique immersive (Phase 12)

| Classe | Rôle | Choix technique |
|---|---|---|
| `StarSystemVisualProfile` | style visuel déterministe | fonction statique pure : teinte (parmi une palette de 8 couleurs curatées), présence d'un anneau (~30%), nombre de lunes (0-2), facteur de taille — dérivés d'un hachage de bits (variante de la finalisation MurmurHash3 32 bits) de l'identifiant du système et de la graine, jamais de `UnityEngine.Random` ; testable en EditMode et recoupé en Python |
| `GalaxyBackgroundFactory` | fond spatial | texture procédurale unique (étoiles éparses par hachage par pixel, nébuleuses par bruit de Perlin modulé par des taches radiales), déterministe depuis la même graine que la galaxie — même technique que `RuntimeSpriteFactory` (pas de texture importée) |
| `RuntimeSpriteFactory` (étendu) | sprites anneau/lune | `GetRingSprite()` (disque avec un trou, contour adouci des deux côtés) et `GetMoonSprite()` (réutilise `GetCircleSprite()`, une lune n'étant qu'un petit disque) |
| `StarSystemMarker` (étendu) | style et légère animation | applique `StarSystemVisualProfile` (teinte modulée par le développement, taille, anneau et lunes en enfants du marqueur), rotation lente de l'anneau — purement cosmétique, aucun impact sur la simulation |
| `TerritoryOverlayController` | halo de territoire par empire | un halo par système, mis à jour par **sondage** (`Update`, pas d'abonnement à un événement précis) : au moins deux événements distincts peuvent changer un propriétaire (`SystemColonizedEvent`, `BattleResolvedEvent`) et rien ne garantit qu'il n'y en aura pas d'autres — comparer le `OwnerId` courant à une valeur mise en cache reste correct quelle que soit la cause du changement, pour un coût négligeable (une centaine de comparaisons d'entiers par frame) ; légère pulsation d'alpha |
| `SystemLabelController` | noms/détails selon le zoom | `TextMesh` (composant intégré au moteur, pas TextMeshPro — aucun asset à importer) par système, visibilité et contenu basculés par seuils **relatifs** du zoom courant (`GalaxyCameraController.MinOrthographicSize`/`MaxOrthographicSize`, nouvellement exposés) : éloigné = rien, moyen = nom, proche = nom + population/développement |
| `SystemInfoPanelController` (corrigé) | bouton Investir | désactivé et remplacé par « Développement maximal atteint » à développement 5/5, au lieu de rester cliquable pour échouer silencieusement côté service (`EconomyService.TryInvestInDevelopment` bloquait déjà correctement le paiement — seul l'état du bouton était en cause) |

> **Terminologie « système » vs « planète » :** vérifiée sur l'ensemble du code et de
> l'interface — aucune trace de « planète » n'existait déjà (`StarSystemState`/« système »
> partout depuis la Phase 2). Rien à renommer ; la structure reste prête à accueillir
> plusieurs planètes par système plus tard (voir la remarque suivante).
>
> **Prêt pour plusieurs planètes par système, sans réécriture :** `StarSystemVisualProfile`,
> le fond et les halos de territoire raisonnent tous en termes de « système », jamais de
> « planète unique » — un système pourra un jour exposer une liste de planètes/lunes/colonies
> sans que cette couche visuelle ait à changer de forme.

### Briques du choix de faction et de système de départ (Phase 13)

| Classe | Rôle | Choix technique |
|---|---|---|
| `PendingGameSetup` | choix du joueur, transporté entre scènes | petite classe immuable (faction choisie, index d'emplacement de départ) enregistrée dans le `ServiceLocator` juste avant `ISceneLoader.LoadScene` — celui-ci n'est vidé qu'à la toute première `Awake` de `GameBootstrap`, donc reste lisible dans `GalaxyMap` sans aucun nouveau mécanisme de transport inter-scènes ; consommée et désenregistrée une seule fois par `EmpireController` |
| `FactionPickerController` | écran à deux étapes (faction, puis système) | même pattern de coordination que la Phase 11 (`HudController`/`ManagementWindowController` via `GetComponent` sur le même GameObject) : vit sur `[UI]` à côté de `MainMenuController`, qui se tait tant qu'il est ouvert ; régénère une galaxie jetable (même graine que la vraie, donc strictement identique) uniquement pour lister les 6 emplacements candidats, puis la laisse de côté |
| `EmpireFactory.CreateEmpires` (étendu) | n'importe quelle faction jouable | nouveau paramètre optionnel `playerDefinitionOverride` (signature additive, aucune régression sur les appels existants) : permet d'incarner n'importe laquelle des 6 définitions plutôt que systématiquement celle marquée `IsPlayerControlled`, qui reste le repli par défaut |
| `EmpirePlacement.AssignHomeSystems` (nouveau) | système de départ choisi, pas imposé | fonction pure : réordonne les emplacements candidats pour placer celui choisi par le joueur en tête (le joueur est toujours `empires[0]`) ; `null` ou un index hors bornes reproduit exactement le comportement d'avant cette phase (identité) — aucune régression pour « Continuer », qui ne passe jamais par l'écran de choix |

> **Pourquoi le joueur pouvait-il jusque-là seulement incarner une faction fixe, toujours sur
> le système le plus proche du centre ?** `EmpireFactory`/`EmpirePlacement` n'avaient jamais eu
> besoin de faire autrement : un seul joueur, un seul roster, une seule disposition possible.
> Cette phase n'a rien réécrit de leur logique — elle leur ajoute la capacité de recevoir un
> choix, sans changer leur comportement par défaut.

### Briques de la refonte des flottes (Phase 14)

| Classe | Rôle | Choix technique |
|---|---|---|
| `UnitType` (étendu) | 7 types d'unités | `SpaceFleet` (générique) remplacé par 4 classes de vaisseaux différenciées : Chasseurs, Frégate, Croiseur, Cuirassé — la seule fois où la règle « ne jamais réordonner/réutiliser une valeur » de l'enum est enfreinte (projet en cours, aucune sauvegarde à préserver ; voir `GameSaveData.Version`) |
| `UnitBundle` (étendu) | 7 champs nommés | même pattern qu'avant (Phase 6), 3 champs de plus ; `Get`/`Of`/`Scale`/opérateurs/`ToString` étendus en conséquence |
| `UnitTypeDefinition` (étendu) | description de rôle | nouveau champ `roleDescription`, affiché sous chaque bouton de recrutement (`SystemInfoPanelController`) — livre le rôle des vaisseaux du brief (Frégate transporte l'infanterie, Cuirassé transporte blindés/infanterie/chasseurs et frappe le plus fort, Chasseurs pour le combat spatial, Infanterie indispensable pour coloniser/envahir, Blindés pour le combat terrestre) **comme texte informatif seulement** — aucun verrouillage mécanique encore (dépend de la refonte de la colonisation, Phase 16) ; le Croiseur, sans rôle défini dans le brief, reçoit un rôle générique de vaisseau de combat polyvalent (choix de jugement documenté ici) |
| `Fleet` (étendu) | nom de flotte | nouveau champ `Name`, attribué automatiquement à la création (`"Flotte {Id}"`) — commandant (Amiral) ajouté en Phase 15 |
| `MilitaryService.TryRecruitUnits` (étendu) | plafond de 10 unités par flotte | vérifié en amont du recrutement (garnison actuelle + commandes déjà en attente + quantité demandée), pas à sa complétion — évite de dépenser des ressources pour un recrutement voué à être refusé |
| `MilitaryService.TryMoveFleet` (étendu) | plafond de flottes en campagne, lié à la Logistique | `1 + IResearchService.GetCompletedTierCount(empireId, ResearchDomain.Logistics)` flottes en `FleetStatus.Moving` simultanément par empire (pas le nombre total de flottes : les garnisons de chaque système possédé restent illimitées, sans quoi la colonisation serait bridée par la technologie) |
| `IMilitaryService.GetFleetsForEmpire` (nouveau) | liste des flottes d'un empire | toutes les flottes d'un empire à travers la galaxie, stationnées ou en déplacement — alimente le nouvel onglet Flottes |
| `MilitaryDecisionMaker.SplitAttackForce` (généralisé) | réserve/attaque sur 7 types | réécrit avec `UnitTypes.All` (au lieu d'une chaîne à 4 champs codés en dur) : comportement inchangé (Infanterie réservée en priorité), généralisé aux nouveaux types |
| `ManagementWindowController` (étendu) | onglet Flottes | nom, position, composition résumée et puissance estimée de chaque flotte du joueur — même style à onglets que les 5 autres |
| `GameSaveData.Version` 1→2 / `GarrisonSaveData` (étendu) | sauvegarde | `SpaceFleet` remplacé par les 4 nouveaux champs de vaisseaux, plus `FleetName` |

> **Pourquoi un plafond de flottes « en déplacement » plutôt qu'un plafond du nombre total de
> flottes ?** Le brief dit « Tech1 → 1 flotte, Tech2 → 2, etc. » Compter *tous* les objets
> `Fleet` d'un empire (donc chaque garnison de chaque système possédé) bloquerait la
> colonisation dès qu'un empire posséderait plus de systèmes que son palier technologique —
> contraire à l'esprit du brief, qui vise le nombre de flottes qu'on peut mener en campagne à la
> fois. Le plafond ne s'applique donc qu'aux flottes activement en transit.
>
> **Limitation v1 documentée :** les fusions de flottes à l'arrivée (renfort d'une garnison déjà
> pleine) peuvent dépasser le plafond de 10 unités sans être bloquées — même esprit que
> l'absence de calcul d'itinéraire multi-sauts (Phase 6) : un cas marginal qui n'exigeait pas de
> logique de perte/refus supplémentaire cette phase.

### Briques des amiraux (Phase 15)

| Classe | Rôle | Choix technique |
|---|---|---|
| `Admiral` (nouveau) | commandant d'une flotte | `readonly struct` généré par hachage déterministe (même technique que `StarSystemVisualProfile`, Phase 12 : variante de la finalisation MurmurHash3 32 bits, aucun `System.Random`/`UnityEngine.Random`) — mélange `(fleetId, ownerId)` plutôt que la graine de galaxie (non exposée jusqu'à `MilitaryService`), pour éviter que la toute première flotte de chaque empire de chaque partie obtienne le même Amiral ; nom (deux listes de mots 100% originaux, indexées par hachage) et trois bonus/malus (attaque, vitesse, défense, plage -15%/+20%) |
| `Admiral.Compute` — un malus garanti | compromis plutôt que pige libre | une pige totalement indépendante par statistique donnerait ~1 Amiral sur 5 sans aucun défaut ; une pige supplémentaire désigne laquelle des trois statistiques est la faible, cohérent avec l'exemple du brief (+attaque/+vitesse **mais** -défense) |
| `Fleet` (étendu) | commandant assigné à la création | nouvelle propriété `Admiral`, générée une seule fois (jamais recalculée après coup — `Fleet.Id` est réémis séquentiellement à chaque chargement de sauvegarde, donc un Amiral re-dérivé changerait silencieusement au rechargement) ; les deux constructeurs de la Phase 14 fusionnent en un seul avec `name`/`admiral` optionnels |
| `MilitaryService` (étendu) | bonus/malus branchés sur le combat et le déplacement | attaque dans `ComputeAttackerModifier`, défense dans `ComputeDefenderModifier` (accepte désormais la flotte défenseuse, potentiellement absente), vitesse dans `ComputeArrivalDate` — `CombatResolver` reste inchangé, déjà générique sur un simple facteur multiplicatif par camp |
| `GameSaveData.Version` 2→3 / `GarrisonSaveData` (étendu) | sauvegarde | 4 nouveaux champs (nom + 3 bonus de l'Amiral) ; une sauvegarde antérieure (Version < 3) ne les contient pas — `SaveService.Apply` génère alors un nouvel Amiral plutôt que d'en restaurer un « tout à zéro » à partir des champs absents |
| `SystemInfoPanelController` / `ManagementWindowController` (étendus) | affichage | nom et trois bonus de l'Amiral affichés sous la garnison du panneau système et sous chaque ligne de l'onglet Flottes |

### Briques de la colonisation stratégique (Phase 16)

| Classe | Rôle | Choix technique |
|---|---|---|
| `ColonizationRules` (nouveau) | coût de colonisation | fonctions statiques pures (même esprit que `CombatResolver`/`EmpirePlacement`) : `RequiredInfantry = 1 + population/1500 + (développement+1)/2` (1 à 6 unités sur les plages générées) et `InfantryLost = Clamp(⌈requis × (1.25 − stabilité)⌉, 1, requis)` — population et développement pilotent l'exigence (« plus développé = plus d'infanterie »), la stabilité pilote les pertes |
| Proxy « défenses » | interprétation du brief | aucun champ de défense n'existe sur `StarSystemState`, et un système libre n'a par construction aucune garnison : le **niveau de développement** sert de mesure de ce qu'il faut surmonter — exactement le rôle qu'il joue déjà comme bonus de terrain défensif dans la formule de combat |
| Échelle des pertes | 1..6 unités, pas 5..150 soldats | le brief raisonne en soldats ; le jeu compte en unités avec un plafond de 10 par flotte (Phase 14). L'exigence reste donc toujours réalisable en une seule flotte, aucun système n'est incolonisable |
| `MilitaryService.TryMoveFleet` (étendu) | refus en amont | un départ vers un système libre est refusé si la flotte n'embarque pas l'Infanterie requise — même philosophie que les plafonds de la Phase 14 : ne jamais laisser partir un voyage voué à l'échec. Sûr parce que les statistiques d'un système libre ne changent jamais (rien ne les fait évoluer tant qu'il n'a pas de propriétaire) |
| `MilitaryService.ResolveColonization` (nouveau) | installation | consomme `InfantryLost` fantassins ; **les colons ne se dissolvent plus systématiquement** (comportement d'avant cette phase) : le reste de la flotte devient la garnison de la nouvelle colonie. Une flotte qui n'embarquait que le strict nécessaire disparaît toujours entièrement |
| Verrou d'invasion dans `ResolveBattle` | point 9 du brief | gagner la bataille ne suffit plus : sans Infanterie survivante, la garnison adverse est bien détruite mais le système reste à son propriétaire et les vainqueurs repartent. Une frappe de Chasseurs seuls reste donc une tactique valable (rôle « combat spatial »), simplement sans conquête. Corrige au passage un défaut préexistant : une victoire à la Pyrrhus sans survivant créait une garnison vide fantôme |
| `MilitaryDecisionMaker` — trois corrections IA | indissociables du verrou | (1) l'IA vise le voisin libre **le moins exigeant** au lieu du premier trouvé (sinon elle bloque indéfiniment sur un système trop peuplé) ; (2) sa cible de garnison monte à `2 + exigence du voisin` quand un système libre est à portée — sans quoi le Pacifiste (cible 2) ne pourrait *jamais* coloniser — et elle recrute de l'Infanterie en priorité tant qu'elle n'en a pas assez, ce qui règle le cas du Militariste qui ne recrutait que l'unité la plus puissante et n'aurait donc jamais eu un seul fantassin ; (3) `SplitAttackForce` ne réserve plus la *dernière* Infanterie à domicile, sinon toute force d'attaque serait incapable d'occuper ce qu'elle conquiert |
| `SystemColonizedEvent` (étendu) | rapport | nouveau champ `InfantryLost`. Rayon d'impact quasi nul : un seul émetteur, un seul abonné (test) — `TerritoryOverlayController` sonde `OwnerId` sans s'abonner |

> **Aucun changement de sauvegarde, de scène ni d'interface `IMilitaryService`** : la phase
> n'introduit aucun état persistant, et les trois doublures de test d'`IMilitaryService`
> restent intactes.

### Briques du déplacement longue distance et des rencontres spatiales (Phase 17)

| Classe | Rôle | Choix technique |
|---|---|---|
| `HyperlanePathfinder` (nouveau) | itinéraire le long des routes hyperspatiales | Dijkstra statique pur en O(V²) **pondéré par la distance euclidienne** entre systèmes consécutifs, pas par le nombre de sauts — la durée dépend de la distance (brief), donc le bon itinéraire est le plus court en distance. Sur 100 systèmes, O(V²) est instantané et évite un tas binaire dont l'ordre d'égalité serait une source de non-déterminisme ; départage explicite sur `StarSystemId.Value` à l'extraction **et** à la relaxation. Aucun pathfinding n'existait dans le projet : la galaxie est en revanche garantie connexe (arbre couvrant minimal, `GalaxyGenerator`), donc un chemin physique existe toujours |
| Prédicat de traversabilité | **intermédiaires seulement** | `isIntermediateTraversable` ne s'applique jamais aux extrémités : un système ennemi en guerre est une destination légale mais pas un point de passage légal. L'appliquer aux extrémités casserait toute attaque — c'est l'erreur la plus facile à commettre ici, elle a son propre test |
| Points de passage | libres ou à soi, et **jamais colonisés** | seule la destination finale déclenche une résolution d'arrivée (colonisation / renfort / bataille). Sans cette règle, `ResolveArrival` coloniserait le premier système libre traversé par n'importe quel trajet. Un point de passage n'est qu'un point de navigation — on le survole sans s'y arrêter |
| Traversabilité = contrainte de **planification** | jamais revérifiée en vol | revérifier à chaque étape ouvrirait toute une classe de cas limites (recalcul d'itinéraire, halte en route, repli en cascade) sans contrepartie de jeu. Limitation v1 documentée, du même registre que celles déjà assumées dans `MilitaryService` |
| `Fleet` (étendu) | état d'itinéraire | `Route`/`RouteIndex`/`DepartureDate`/`JourneyStartDate`, propriétés dérivées `DestinationSystemId`, `CurrentLegFrom`/`CurrentLegTo`, `IsOnFinalLeg` ; `BeginJourney`/`AdvanceToNextLeg` remplacent `BeginMove`. **`OriginSystemId` et `CurrentSystemId` sont figés pour tout le voyage** : `ComputeAttackerModifier` lit le moral du système d'origine et `RetreatToOrigin` y renvoie la flotte — les déplacer d'étape en étape ferait varier silencieusement la puissance de combat selon le dernier système survolé, et replierait la flotte d'un seul saut au lieu de la ramener chez elle |
| Durée | **un seul arrondi** sur la distance cumulée | l'étape *k* arrive à `départ + Max(k, ⌈distanceCumulée[k] / vitesse⌉)`. Arrondir par étape ferait payer 15 arrondis et 15 planchers d'un jour à un trajet de 15 sauts ; un arrondi unique donne un total exactement égal à `⌈distanceTotale / vitesse⌉` tout en restant monotone étape par étape |
| Revalidation de la destination à l'arrivée | trou créé par cette phase | la Phase 16 justifie ses raccourcis par « les statistiques d'un système libre ne changent jamais » — vrai sur 3 jours, faux sur 30. Si le propriétaire a changé pendant le vol et qu'aucune guerre n'est déclarée, la flotte se replie au lieu de résoudre : sans cela, une destination colonisée en cours de route enverrait la flotte en bataille contre un empire avec qui on est en paix, contournant entièrement le verrou de guerre de la Phase 7 |
| Plafond de flottes simultanées : base 1 → **2** | jouabilité | avec des trajets de 20 à 40 jours, un joueur sans recherche en Logistique serait privé de *tout* mouvement — y compris une colonisation voisine — pendant plus d'un mois de jeu, ce qui contredit frontalement la promesse du point 12. Le prédicat passe aussi de `Status == Moving` à `Status != Stationed`, sinon laisser une rencontre en attente deviendrait un moyen de lancer une flotte supplémentaire gratuitement |
| Détection de rencontre | **au début d'une étape**, pas quotidiennement | balayage des autres flottes en vol dont l'étape courante emprunte la même paire de systèmes (non ordonnée). Aucun faux négatif : de deux flottes partageant un tronçon sur des fenêtres qui se chevauchent, la seconde à démarrer voit toujours la première — donc ni balayage quotidien, ni registre « déjà rencontré ». Au plus **une** rencontre par début d'étape (première par `Fleet.Id` croissant) : deux rencontres simultanées mutileraient la même composition deux fois. Les flottes d'un même propriétaire s'ignorent |
| Mode `Scan`/`Suppress` sur le début d'étape | anti-boucle | toute étape entamée *à cause* d'une rencontre ou d'une bataille (repli, retraite) supprime le balayage — sinon un repli repart sur la même paire de systèmes et re-rencontre immédiatement la flotte qu'il vient de fuir |
| La détection **ne mute rien** | robustesse | elle passe les deux flottes en `AwaitingEncounter`, gèle leurs jours de trajet restants, empile une rencontre et publie un événement. La résolution se fait **hors du tick**, en fin de `OnDayAdvanced` et en fin de `TryMoveFleet` — la rencontre naît sinon à l'intérieur d'un gestionnaire de `DayAdvancedEvent`, au milieu de la copie défensive de `CompleteArrivals`. Les rencontres IA-contre-IA se résolvent immédiatement dans le drainage ; celles impliquant le joueur restent en file, une seule exposée à la fois |
| La mise en pause est **cosmétique, jamais un mécanisme** | contrainte de l'horloge | `MilitaryService` ne touche jamais l'horloge ; c'est `EncounterWindowController` qui met en pause à l'ouverture et reprend à la fermeture, seulement si c'est elle qui a mis en pause (convention exacte de `PauseMenuController`). `GameClock.AdvanceDays` publie `DayAdvancedEvent` **en boucle** sur N jours dans la même frame et ne teste `IsPaused` qu'à l'entrée de `Tick` : mettre en pause depuis un gestionnaire n'arrêterait pas les jours restants, et la barre du HUD permet de toute façon de relancer le temps. La correction repose donc sur le **gel explicite** des jours de trajet restants, réémis à la résolution |
| `EncounterRules` (nouveau) | issues et choix IA | fonctions statiques pures. Issues selon le statut diplomatique : guerre → Combattre / Se replier / Négocier ; alliance ou pacte → Négocier / Commercer / Passer son chemin ; paix → Négocier / Commercer / Piraterie / Passer son chemin. Le choix de l'IA est une **formule pure sur les seuils de personnalité existants, sans hachage** : `Fleet.Id` est réémis à chaque chargement (c'est précisément pourquoi la Phase 15 persiste l'Amiral), un tirage haché dessus serait « déterministe » sans être reproductible. `AggressionThreshold` (`null` = ne combat ni ne pirate jamais), `PeacePowerRatioThreshold` et le traité commercial suffisent à décider — zéro nouvelle constante de personnalité |
| Attaquant d'un combat de rencontre | celui qui a choisi « Combattre » | `CombatResolver` donne l'égalité au défenseur : en espace profond il n'y a pas de terrain, donc le rôle doit venir du sens de l'action et non d'un identifiant. Aucun bonus de terrain n'est appliqué |
| « Poursuite » abandonnée | arbitrage documenté | seule option exigeant qu'une flotte abandonne son itinéraire pour re-cibler un objet **mobile** sans position fixe. Combat / repli / négociation couvrent l'esprit du point 13 en guerre ; côté paix, « croisement » et « poursuite de route » désignent tous deux « il ne se passe rien » — c'est l'option « Passer son chemin » |
| `GameSaveData.Version` 3→4 / `FleetInTransitSaveData` (nouveau) | **les flottes en vol sont désormais sauvegardées** | leur disparition au rechargement était une limitation acceptable avec des trajets de 1 à 3 jours ; elle devient un bug très visible avec des trajets de plusieurs semaines. Itinéraire, étape, dates, nom, Amiral et composition sont persistés — **aucun identifiant de flotte** : rien nulle part n'en référence un, et les flottes restaurées en reçoivent un neuf, exactement le précédent de la Phase 15 |
| `ClearFleetsInTransit()` **avant** la boucle de restauration | garde-fou | `SaveService.Apply` ne vide jamais rien (`RestoreGarrison` est un get-or-create, donc il écrase). Une restauration qui *ajoute* dupliquerait chaque flotte en vol à chaque « Recharger » depuis le menu pause |
| Les rencontres en attente **ne sont pas sauvegardées** | simplification assumée | la sauvegarde ne peut pas être refusée (`SaveController` sauvegarde sur mise en arrière-plan et sur quitter, et l'autosauvegarde mensuelle part dans la même frame que le jour qui a créé la rencontre). Au rechargement, les flottes reprennent leur étape en vol sans en *démarrer* une : aucun balayage ne se déclenche, la rencontre est oubliée. Le jeu étant entièrement déterministe avec un seul fichier de sauvegarde, « recharger pour retenter » n'existe pas — la seule variable était le choix du joueur, qu'il refera. Supprime toute une surface de sérialisation pour une phrase de documentation |
| `IEncounterService` **séparée** de `IMilitaryService`, enregistrée sur la même instance | limitation de la casse | le `ServiceLocator` est indexé par type, un même objet peut donc s'enregistrer sous deux clés. `IMilitaryService` ne gagne que les 3 membres de sauvegarde, soit 3 lignes par doublure de test au lieu de 5, dans les 3 fichiers qui en hébergent une |
| `SystemInfoPanelController` (étendu) | ciblage | « Déplacer une flotte » arme un mode de ciblage ; la sélection suivante est la destination (annulée par un clic dans le vide). L'état armé vit **hors** de `_selectedSystemId` et est consommé **avant** sa réécriture, et la flotte source est revalidée au moment du clic |

### Briques de l'IA dynamique (Phase 18)

| Classe | Rôle | Choix technique |
|---|---|---|
| `EmpireHoldings` (nouveau) | ce qu'un empire possède | fonctions statiques pures (même esprit que `EmpirePlacement`/`ColonizationRules`) : `OwnedSystems`, `Capital`, `TotalPower`, `NeighboringEmpires`, `FirstBorderSystemOf`. **Remplace les cinq copies privées de `FindPrimarySystem`** — chacun des cinq decision makers embarquait la sienne, renvoyant le *premier* système de `map.Systems` appartenant à l'empire et raisonnant uniquement dessus. Sans conséquence tant qu'un empire n'avait qu'un système ; depuis que la colonisation fonctionne réellement (Phase 16), cela voulait dire des colonies jamais développées, jamais garnisonnées, et une IA qui cessait définitivement de s'étendre une fois les voisins directs de sa capitale pris |
| `Capital` = le système **le plus développé** | départage sur le plus petit identifiant | une colonie naît au développement 0 : elle ne peut donc jamais déloger la capitale par accident — ce que « le premier système trouvé » faisait dès qu'une colonie tombait à un indice plus faible que la vraie capitale, l'IA se mettant alors à gérer un caillou vide en laissant son centre à l'abandon. Le critère suit malgré tout l'empire si sa capitale historique est conquise |
| `FleetRouting.IsTraversableWaypoint` | source **unique** de la règle de traversée | utilisée à la fois par `MilitaryService.TryPlanRoute`, qui calcule l'itinéraire réel, et par le parcours de l'IA. Les laisser diverger ferait choisir à l'IA des destinations que le service refuserait ensuite : elle gaspillerait son mois, et pire, elle aurait déjà détaché une flotte pour rien |
| `FleetRouting.HopDistances` (nouveau) | **un seul parcours en largeur multi-source par empire et par mois** | le point CPU de la phase. Interroger le pathfinder pour chaque cible possible ferait, sur 100 systèmes et 5 IA, cinq cents Dijkstra O(V²) par mois de jeu. Un parcours en largeur depuis *tous* les systèmes possédés donne en **un** passage O(V+E) la distance en sauts **et** le système possédé le plus proche pour chaque cible. L'IA a besoin de comparer des candidats, pas de connaître l'itinéraire exact — `TryMoveFleet` calculera de toute façon le vrai chemin pondéré par la distance au moment du départ |
| Système non traversable : **atteint mais jamais dépassé** | cohérence avec la Phase 17 | un système ennemi entre dans le résultat (on peut vouloir l'attaquer) mais ses propres voisins ne sont pas explorés à travers lui. C'est la règle « le prédicat ne s'applique pas aux extrémités » transposée au parcours en largeur — sans elle, l'IA planifierait des trajets à travers le territoire d'un tiers, que `TryPlanRoute` refuse |
| `ExpansionPlanner` (nouveau) | choix de cible **validé avant tout détachement** | **le correctif le plus important de la phase.** `TryDetachFleet` n'a pas d'inverse : le code d'avant détachait puis appelait `TryMoveFleet`, et si le déplacement échouait la flotte détachée restait **orpheline et stationnée** à côté de la garnison, qui se fragmentait un peu plus chaque mois. Défaut préexistant, quasi inoffensif tant que tout était adjacent et vérifié d'avance — mais avec des cibles à plusieurs sauts et un plafond de deux flottes, l'échec devient le cas courant. Le plafond est donc consulté (`CanDeployAnotherFleet`) et la composition validée **avant** de détacher quoi que ce soit |
| Cible de colonisation | exigence en Infanterie la plus basse, puis le moins de sauts, puis l'identifiant | trois critères **totalement ordonnés** : aucune égalité ne subsiste, donc aucun tirage n'est nécessaire pour départager — et le résultat ne dépend pas de l'ordre d'itération du dictionnaire. L'exigence passe avant la distance parce qu'un système trop peuplé reste hors de portée pendant des dizaines de mois, alors qu'un système abordable trois sauts plus loin est colonisable tout de suite : c'est l'extension à toute la galaxie de la correction de la Phase 16 |
| Cible offensive | le système ennemi **le moins défendu** atteignable, mêmes départages | l'IA cesse de se casser indéfiniment les dents sur le premier voisin de sa liste quand un système sans garnison est à portée. Contrairement à la colonisation, une cible déjà visée n'est pas exclue : deux vagues successives sur le même système ennemi sont une concentration de force légitime |
| Une cible de colonisation déjà visée est **exclue** | plafond de deux flottes | viser deux fois le même système libre gaspillerait la moitié de la capacité d'expansion de l'empire : la seconde flotte arriverait sur un système devenu le sien et se contenterait de renforcer la garnison de la première. Ce sont ses **propres** flottes que l'IA consulte — aucune information qu'elle ne possède pas légitimement |
| Recrutement sur le système **le plus loin de sa cible** | pas simplement « le moins défendu » | viser le système le plus faible en valeur absolue produit un blocage : il atteint sa petite cible de colonie, le recrutement s'arrête, et le système d'où devrait partir la prochaine vague de colons — dont la cible est relevée par l'exigence de sa cible de colonisation — n'est jamais renforcé, l'empire cessant alors toute activité. Comparer des **écarts à la cible** fait converger *chaque* système vers la sienne. C'est aussi, gratuitement, le comportement défensif de la phase : un système vidé par un départ de flotte ou décimé par une bataille affiche le plus gros déficit et passe en tête |
| **Aucune réaction à une flotte ennemie en approche** | limitation v1 documentée | il faudrait scanner les flottes de tous les rivaux, c'est-à-dire donner à l'IA une **omniscience** que le joueur n'a pas — alors que le projet a justement un système d'espionnage pour que l'information se mérite. Le rééquilibrage par le déficit couvre l'essentiel du besoin sans tricher |
| Cible de garnison : pleine sur la capitale, **moitié (au moins 2) sur les colonies** | entretien | appliquer la cible pleine partout multiplierait l'entretien par le nombre de systèmes : un Militariste à cinq systèmes viserait 40 unités, soit jusqu'à 200 crédits par jour, et se ruinerait. Le plafonnement ramène ce pire cas à 24. La capitale reste le bastion, les colonies n'ont qu'une garnison de tenue |
| `DevelopsCapitalFirst` (nouveau champ de personnalité) | rattrapage ou bastion | `GetInvestmentCost = (niveau + 1) × 200`, donc développer un système en retard est **toujours** moins cher que pousser plus haut celui qui est déjà en tête : le rattrapage est aussi la stratégie efficace, et c'est le défaut. Seul le Militariste fait exception — ses Cuirassés exigent un `MinimumDevelopmentLevel` de 4, cinq systèmes médiocres ne lui en donneraient jamais un seul. Corollaire noté dans le code : **aucun repli sur un autre système n'est nécessaire**, puisque l'investissement sur le système le moins développé est par construction le moins cher de l'empire — s'il n'est pas finançable, aucun autre ne l'est |
| `ExpansionRange` (nouveau champ de personnalité, en sauts) | Pacifiste 2, Commerçante 3, Militariste et Opportuniste 4, Expansionniste 6 | la différence de comportement la plus visible de la phase : l'Expansionniste essaime loin, le Pacifiste ne quitte pas ses abords immédiats. Borne aussi le parcours en largeur, donc le coût CPU |
| « Une seule action par appel » **conservée**, mais le système est choisi | rythme de dépense | passer à « une action par système » multiplierait le rythme de dépense par le nombre de systèmes et viderait le trésor d'un empire étendu. Choisir dynamiquement la bonne cible garde le rythme constant, la lisibilité de l'observation, et corrige quand même la famine des colonies |
| `DiplomacyDecisionMaker` compare des **puissances d'empire** | plus des garnisons de système | avec plusieurs systèmes, comparer deux garnisons isolées ne mesure plus rien, et un rival ne bordant que les colonies n'existait tout simplement pas aux yeux de la diplomatie — ni pacte, ni guerre, ni paix possible avec lui. Effet de bord voulu : les écarts deviennent plus marqués, donc les déclarations de guerre et les demandes de paix plus tranchées. Sur une partie où chaque empire n'a qu'un système, `TotalPower` vaut exactement l'ancienne valeur, et les seuils réglés avant cette phase gardent leur sens |
| Déclaration de guerre laissée à l'**adjacence** (élargie à tous les systèmes possédés) | garantit une guerre exploitable | un empire limitrophe est toujours joignable : l'itinéraire vers un voisin direct ne compte aucune étape intermédiaire et échappe donc au filtre de traversabilité. Aucune vérification d'itinéraire supplémentaire n'est nécessaire, et l'IA ne peut pas se retrouver dans une guerre qu'elle n'a aucun moyen de mener |
| `EspionageService` et l'onglet Espionnage visent eux aussi la **capitale** | cohérence | le « système de référence » du contre-espionnage était le premier trouvé dans l'ordre de la carte — souvent une colonie vide dont le contre-espionnage quasi nul rendait toutes les missions triviales. Le service, l'affichage et le choix de l'IA doivent désigner le même système, sinon l'IA raisonne sur une difficulté qui n'est pas celle qu'elle rencontre |

> **Aucun état IA persistant, aucun changement de sauvegarde** (`GameSaveData.Version` reste à
> 4) : toutes les décisions sont recalculées chaque mois à partir de la carte et des services.
> Une IA sans mémoire ne peut ni désynchroniser une sauvegarde, ni s'obstiner sur un objectif
> devenu caduc. `IMilitaryService` ne gagne qu'un membre, `CanDeployAnotherFleet`.

### Briques des territoires et des frontières (Phase 19)

Les halos de la Phase 12 posaient un disque coloré par système. Deux systèmes voisins ne se
rejoignaient jamais : l'œil lisait des taches, jamais un territoire, et encore moins une ligne
de front. Cette phase les remplace par des **zones d'influence continues**.

| Classe | Rôle | Choix technique |
|---|---|---|
| `TerritoryPartition` (nouveau) | découpe la galaxie en cellules de contrôle | **découpage par demi-plans (Sutherland-Hodgman), pas un algorithme de Voronoï dédié.** Le résultat est identique, mais l'implémentation tient en une boucle de rognage triviale à relire et à tester, là où Fortune demande une structure de front de mer et une file de priorité dont la moindre erreur de tolérance numérique produit une cellule silencieusement fausse. Classe statique pure, sans dépendance à Unity au-delà de `Vector2` — même esprit que `GalaxyGenerator` |
| Chaque arête retient **qui se trouve en face** | `TerritoryCellVertex.NeighbourIndex` | c'est ce qui distingue une frontière contestée d'une façade sur le vide, et donc tout le concept de la phase. L'information est connue gratuitement au moment du découpage — le demi-plan qui crée l'arête *est* celui du voisin. La retrouver après coup coûterait une recherche géométrique par arête, avec les problèmes de tolérance que cela suppose |
| Sortie anticipée **exacte**, pas une heuristique | voisins triés par distance | la bissectrice entre un site et un voisin passe à `distance / 2` du site. Si cette valeur dépasse le rayon du sommet le plus éloigné de la cellule courante, elle ne peut plus la couper — et les voisins suivants encore moins. Écarter les sites lointains ne dégrade donc jamais le résultat, contrairement à un plafond « les 24 plus proches » qui serait faux dans un amas |
| `TerritoryMeshBuilder` (nouveau) | deux maillages combinés, **deux draw calls** | même raisonnement que `GalaxyLinkRenderer` pour les routes. Les cent halos de la Phase 12 coûtaient cent `SpriteRenderer` mis à jour à chaque frame ; ici rien n'est reconstruit tant qu'aucun système ne change de maître |
| Le concept tient dans les **couleurs de sommets**, sans shader | dégradé + frontières | le remplissage s'estompe vers le centre de chaque cellule et s'affirme au bord ; seules les arêtes bordant un *autre* empire reçoivent un halo puis un trait vif. Une frontière contestée brille, une façade sur le vide s'évanouit — la carte désigne d'elle-même où se joue la tension, sans que le joueur ait à ouvrir un écran |
| Chaque cellule dessine **sa propre moitié** de frontière | décalée vers l'intérieur | une frontière entre deux empires porte les deux couleurs, une de chaque côté : chacun lit la sienne. C'est aussi ce qui évite d'avoir à dédupliquer les arêtes partagées, donc à comparer des positions en virgule flottante |
| `TerritoryLevelOfDetail` (nouveau) | l'échelle de lecture | **plus on s'éloigne, plus l'information devient politique ; plus on s'approche, plus elle devient locale.** Les territoires s'affirment au zoom éloigné (opacité 0,30) et s'effacent au zoom rapproché (0,08) : des zones pleines à intensité constante rendraient un système illisible sous sa propre couleur au moment où le joueur doit justement en lire les statistiques. Seuils exprimés en **fraction de zoom**, comme `SystemLabelController`, pour rester corrects quelle que soit la taille de galaxie configurée |
| Largeur de frontière **par palier**, pas continue | 0,22 / 0,16 / 0,11 / 0,05 | une frontière doit garder une épaisseur à peu près constante *à l'écran*, sinon elle est en dents de scie au zoom arrière et devient un ruban au zoom avant. La suivre en continu obligerait à reconstruire le maillage à chaque frame de pincement ; l'indexer sur le palier limite les reconstructions à trois au maximum pour un pincement complet, pour un écart imperceptible à l'intérieur d'un palier |
| L'**opacité** ne reconstruit rien | teinte du matériau | les couleurs de sommets ne stockent qu'une intensité *relative* ; l'opacité globale est portée par `_Color`, donc la faire varier à chaque frame ne coûte rien. Seule la largeur des frontières touche à la géométrie |
| `TerritoryOverlayController` (réécrit) | assemble le tout | deux sources de reconstruction **délibérément dissociées** : le remplissage ne dépend que des propriétaires, les frontières en plus du palier de zoom. Un changement de zoom ne reconstruit donc jamais le remplissage. Détection du changement de propriétaire toujours **par sondage**, pour la raison donnée en Phase 12 |
| `FactionLabelController` (nouveau) | nom de l'empire au cœur de son territoire | une carte politique se lit d'abord par ses noms de régions : sans eux, six couleurs restent six couleurs. Opacité suivant exactement la courbe **inverse** de celle des noms de système, si bien que les deux familles de libellés se relaient au lieu de se superposer. Ancrage au **centre de gravité pondéré par l'aire** des cellules — un empire tenant une grappe dense et un système lointain verrait sinon son nom dériver vers le vide |
| Taille de libellé constante **à l'écran** | échelle déduite de `orthographicSize` | un texte posé dans le monde grandit avec le zoom et finit par barrer l'écran. Un nom de région est une annotation de carte, pas un objet de la scène |
| `BuiltinFontLoader` (nouveau) | police intégrée, une seule fois | extrait de `SystemLabelController`, qui portait seul la parade au retrait d'`Arial.ttf` par Unity 6. Deux contrôleurs affichent maintenant du texte dans la scène : dupliquer ce piège de version reviendrait à devoir le corriger deux fois |
| `Sprites/Default` en tête des nuanciers candidats | vertex color + transparence | c'est déjà celui qu'utilisent tous les `SpriteRenderer` de la scène : son comportement est vérifié dans ce projet précis. `UI/Default` ferme la marche car il force un test de profondeur permissif hors d'un Canvas, ce qui ferait passer les zones devant les systèmes. Les propriétés qu'un `SpriteRenderer` alimente normalement (`_RendererColor`, `_Flip`) sont posées explicitement, un `MeshRenderer` ne le faisant pas |

> **Aucun changement de sauvegarde** (`GameSaveData.Version` reste à 4) : tout est dérivé de
> `StarSystemState.OwnerId` et des positions des systèmes, déjà en mémoire. Le découpage ne
> dépend que des positions, qui ne bougent jamais — il est calculé une fois à la génération de
> la carte et n'est plus jamais retouché ; seules les couleurs suivent les conquêtes.
>
> **Coûts mesurés** (100 systèmes, hors appareil) : découpage 3,5 ms **une fois par partie** ;
> reconstruction du remplissage 0,04 ms et des frontières 0,12 ms, uniquement sur changement de
> propriétaire ou de palier de zoom. Les tampons de construction sont réutilisés, donc aucune
> allocation n'est faite après la première reconstruction — ce qui compte : un pic de
> ramasse-miettes surviendrait sinon exactement pendant une bataille ou une colonisation.
>
> **`SystemLabelController` n'est pas modifié** par cette phase (hors extraction de la police) :
> les noms de systèmes gardent le comportement de la Phase 12.

### Briques des flottes sur la carte (Phase 20, étape 1)

Jusqu'ici, une flotte en voyage n'existait que dans les données : rien ne la dessinait, et la
sélection ne connaissait que les systèmes. Une galaxie où les armées se déplacent sans que rien
ne bouge à l'écran est une galaxie morte.

| Classe | Rôle | Choix technique |
|---|---|---|
| `FleetPresentation` (nouveau) | position et cap d'une flotte en vol | fonction **pure**, séparée du rendu (même esprit que `TerritoryPartition` ou `ColonizationRules`) : vérifiable en EditMode sans scène ni caméra. C'est ce qui permet de garantir la propriété qui compte — **un vaisseau ne sort jamais de son tronçon** |
| Avancement **toujours borné** | rencontres spatiales | une flotte immobilisée par une rencontre voit la date courante dépasser sa date d'arrivée, puisque son trajet est gelé mais pas le calendrier. Sans la borne, son vaisseau continuerait au-delà de sa destination, sur une carte où plus rien ne le ramènerait. Il s'arrête donc dessus — approximation assumée, `Fleet` ne conservant pas la position exacte du gel |
| **`IGameClock` n'est pas touché** | fluidité obtenue au rendu | le calendrier avance par jours entiers, donc la position calculée saute d'un cran par jour. Exposer une fraction de jour aurait obligé à modifier les **sept doublures de test** qui implémentent `IGameClock`, pour un besoin purement cosmétique. `FleetMarker` amortit donc la position vers sa cible (lissage exponentiel indépendant de la fréquence d'affichage) : même résultat visuel, zéro risque sur le modèle |
| `FleetMarker` (nouveau) | la vue d'un vaisseau | **volontairement ignorante de `Fleet`** : ne connaît qu'un identifiant entier et les valeurs à afficher. C'est ce qui permet à `GalaxySelectionController` — qui vit dans `Galaxy` — de le reconnaître au toucher sans que `Galaxy` ait à dépendre de `Military`. La dépendance ne va que dans un sens |
| Taille constante **à l'écran** | vaisseau et cible tactile | un vaisseau dimensionné en unités monde serait un point invisible au zoom arrière et un objet géant au zoom avant. Le rayon du collisionneur suit la même échelle : la cible tactile ne change jamais de taille sous le doigt |
| La **taille** du vaisseau porte la force | pas de texte sur la carte | l'échelle interpole entre 1 et 10 unités embarquées (le plafond par flotte). On lit la puissance d'une flotte sans ajouter un seul libellé à une carte qui en porte déjà beaucoup |
| `RuntimeSpriteFactory.GetShipSprite` | silhouette procédurale | pointe de flèche échancrée à l'arrière, **orientée vers +X** : une rotation de Z égale au cap suffit à l'orienter, sans décalage à compenser. Anti-aliasing par **sur-échantillonnage** (4×4) plutôt que par distance au contour : la forme est concave, une distance signée demanderait de traiter chaque arête séparément |
| `FleetTrailRenderer` (nouveau) | trajectoires | un seul maillage combiné, reconstruit à chaque frame **sans détection de changement** : il y a au plus une poignée de flottes en vol et quelques dizaines de quads, une signature à comparer coûterait plus de code que le calcul. Le tronçon déjà parcouru s'efface derrière le vaisseau |
| **Seules les flottes du joueur laissent une trajectoire** | le renseignement se mérite | les vaisseaux ennemis sont visibles — une galaxie où rien ne bouge est morte — mais leur *destination* est un renseignement. Le projet a un système d'espionnage précisément pour cela, et la Phase 18 refuse déjà à l'IA toute omniscience que le joueur n'a pas : la réciproque doit tenir |
| `GalaxySelectionController` (étendu) | vaisseaux prioritaires sur les systèmes | un vaisseau passant au-dessus d'un système couvre les deux collisionneurs. `OverlapPoint` n'en renvoie qu'un, choisi arbitrairement : la surcharge à tableau (qui n'alloue pas, contrairement à `OverlapPointAll`) les récupère tous et arbitre explicitement. Sans cela, toucher une flotte au-dessus d'un système donnerait un résultat différent d'une frame à l'autre |
| Sélections **exclusives** | un seul panneau contextuel | sélectionner un vaisseau désélectionne le système, et inversement. Deux panneaux ouverts se disputeraient le bas de l'écran |
| Marqueurs **mis en commun** (`ObjectPool`) | pas d'instanciation en jeu | les départs et arrivées sont fréquents à vitesse maximale ; instancier et détruire un GameObject à chaque fois produirait exactement le pic de ramasse-miettes que les conventions du projet interdisent |

> **Seules les flottes en voyage sont dessinées.** Une flotte stationnée *est* la garnison de son
> système : lui donner un vaisseau ajouterait une centaine d'icônes immobiles sur une carte qui
> affiche déjà ses systèmes et ses territoires. Le panneau de système reste l'endroit où l'on
> consulte une garnison.
>
> **Aucun changement de sauvegarde** (`GameSaveData.Version` reste à 4), aucune règle de jeu
> modifiée : cette étape est entièrement de la présentation. La création de flotte, la
> colonisation et le combat restent ceux des Phases 14 à 18.

### Briques de la fiche de système (Phase 20, étape 2)

L'ancienne fiche mesurait **420 unités de haut sur un écran qui en offre environ 315** en 20:9 :
elle était structurellement plus grande que l'écran. Ses sept boutons de recrutement alignés
horizontalement demandaient plus de 700 unités de large pour une zone qui en offrait 344. Les
deux barres de défilement n'étaient pas un défaut de réglage, mais la conséquence directe de ces
deux dépassements.

| Décision | Pourquoi |
|---|---|
| **En-tête permanent + trois onglets en bas** | avec sept champs d'identité et treize actions, aucune disposition ne fait tenir l'ensemble dans 596 × 125 sans passer les cibles tactiles sous le seuil utilisable. Les onglets sont le seul découpage qui laisse chaque section respirer **et** qui accueille une mécanique future sans redécouper la fenêtre : ce sera un onglet de plus. L'identité, elle, ne bascule jamais |
| Onglets **en bas**, pas en haut | c'est la zone du pouce en paysage. Un onglet en haut d'un écran de 6 pouces tenu à deux mains demande de changer de prise |
| Disposition en **`Rect` calculés**, pas en `GUILayout` imbriqué | IMGUI ne signale pas un dépassement, il le rogne silencieusement. Calculer chaque rectangle rend le débordement impossible par construction plutôt que de l'espérer |
| **Hauteurs proportionnelles**, pas en dur | la fiche a été vérifiée par le calcul en 16:9, 19,5:9, 20:9, 21:9 et 22:9, avec et sans message de retour. Les valeurs fixes cassaient dans trois de ces dix cas : bouton d'investissement laissant 14 unités aux bâtiments, boutons d'armée à 25 unités de haut, aperçu débordant de 13 |
| Les lignes d'information **cèdent la place aux actions** | sur un écran très allongé, ce sont les détails de garnison qui disparaissent, jamais les boutons. `WriteLine` n'écrit une ligne que si elle tient encore |
| Le composeur de flotte prend **toute la fiche** | réparti dans les 125 unités du corps, ses boutons « − / + » tombaient à 22 unités de haut, moitié moins que le seuil utilisable. Une action de saisie mérite l'écran entier |
| Valeur **entre** les deux boutons du composeur | empilée au-dessus, elle chevauchait les boutons dès que la tuile descendait sous 62 unités, c'est-à-dire sur tout écran plus allongé que du 19,5:9 |
| Recrutement en **4 colonnes × 2 rangées** | c'était le débordement horizontal : sept tuiles alignées ne tenaient pas. Quatre colonnes laissent environ 94 unités par tuile |
| Le détail de colonisation **n'est plus dupliqué** dans l'Aperçu | il occupe son propre onglet, accompagné des flottes capables de s'en charger. Le doublon faisait déborder l'Aperçu de treize unités dès qu'un message s'affichait |

**Ce que la fiche sait faire de plus :**

| Fonction | Détail |
|---|---|
| **Créer une flotte** | le joueur choisit quelles unités embarquent, jusqu'au plafond de 10. Le reste tient la garnison — l'ancien bouton envoyait *toute* la garnison et laissait systématiquement le système sans défense. S'appuie sur `TryDetachFleet`, qui existait depuis la Phase 14 sans qu'aucune interface ne l'expose |
| **Coloniser explicitement** | l'onglet d'un système libre liste les flottes du joueur et affiche, pour chacune, ce qui lui manque. L'ordre reste un `TryMoveFleet` : le service vérifie l'exigence d'Infanterie **au départ** depuis la Phase 16 et retire les unités à l'arrivée. Dupliquer cette règle pour un bouton dédié la ferait diverger à la première retouche d'équilibrage |
| **Ordre par flotte, plus par système** | `_awaitingDestinationFleetId` remplace `_moveOriginSystemId`. Depuis qu'un système peut héberger sa garnison *et* des flottes détachées, désigner l'origine ne suffit plus à désigner la flotte : l'ancien code déplaçait systématiquement la garnison, même après un détachement |
| **Plafond de flottes en campagne affiché** | déduit de `GetFleetsForEmpire` et `CanDeployAnotherFleet`, sans ajouter de membre à `IMilitaryService` — **cinq doublures de test l'implémentent**, et l'étendre les aurait toutes cassées pour un affichage |

> **Aucun changement de sauvegarde ni de règle de jeu.** Toutes les actions passent par les
> services existants ; seule l'interface change.

### Briques de l'offensive (Phase 20, étape 3)

| Décision | Pourquoi |
|---|---|
| **Aucune « probabilité de victoire » n'est affichée** | parce qu'il n'y en a pas. `CombatResolver` est **entièrement déterministe** — `attackerWon = attackerPower > defenderPower`, sans le moindre tirage. Annoncer « 68 % de victoire » aurait été une invention pure. La fiche annonce donc ce qui va réellement se produire : l'issue, les pertes, et si le système changera de mains. C'est à la fois plus honnête et strictement plus utile qu'un pourcentage |
| **Une bataille par arrivée, pas une bataille combinée** | c'est ce que fait le jeu : `MilitaryService` résout chaque arrivée indépendamment. Trois flottes arrivant à trois dates livrent trois batailles successives et se font battre en détail. `OffensivePlanner` simule les vagues **dans leur ordre d'arrivée**, ce qui rend cette vérité visible au lieu de la cacher derrière un total flatteur — et apprend au joueur à concentrer ses forces |
| `FleetTravel` **extrait** de `MilitaryService` | la planification annonce au joueur la durée que le service appliquera. Deux implémentations de la même formule divergeraient à la première retouche d'équilibrage, et l'interface se mettrait à promettre des dates que le jeu ne tiendrait pas. Le service délègue désormais ; il n'y a qu'une formule |
| Le tri des vagues est un **ordre total** (délai puis identifiant) | deux flottes arrivant le même jour sont toujours simulées dans le même ordre : la prévision affichée ne change pas d'une frame à l'autre |
| Itinéraires **mis en cache par cible** | chaque candidate coûte un Dijkstra sur cent systèmes, et `OnGUI` est appelé au moins deux fois par frame. Les recalculer à chaque appel ferait chuter la fluidité dès l'ouverture de l'onglet |
| Le même prédicat de traversée que le service | `FleetRouting.IsTraversableWaypoint` : une flotte annoncée comme atteignant la cible doit réellement pouvoir partir |
| **Prévision, pas certitude** | la garnison peut être renforcée avant l'arrivée, et le commandement comme la recherche de l'adversaire ne sont pas connus du joueur — c'est le rôle de l'espionnage. La fiche l'écrit : « prévision à effectifs constants » |
| Les échecs de lancement sont **rapportés individuellement** | le plafond de flottes en campagne peut refuser une partie de l'offensive ; le joueur doit savoir combien sont parties et pourquoi les autres ne l'ont pas fait |
| Onglet **Opérations** dans la fenêtre de gestion | l'onglet Flottes reste l'inventaire complet ; celui-ci est le tableau de bord : uniquement ce qui bouge, avec son état, plus un fil des derniers dénouements. Une bataille se résout en un instant — sans cette trace, une offensive résolue pendant que le joueur regardait ailleurs ne laisserait rien à consulter |
| Les batailles **entre tiers** n'entrent pas dans le fil | les lister reviendrait à offrir au joueur un renseignement qu'il n'a pas payé |

> **Vérification par simulation.** La propriété qui fonde la phase est vérifiée en exécutant le
> planificateur : la même force de 8 unités contre une garnison de 5 Croiseurs **prend le
> système en groupé** (4 pertes) et **est repoussée en échelonné** (6 pertes). Une frappe sans
> Infanterie détruit la garnison sans prendre le système. Le résultat ne dépend pas de l'ordre
> dans lequel les flottes sont cochées.
>
> **Aucun changement de sauvegarde, aucune règle de jeu modifiée** : la planification ne fait
> qu'anticiper et enchaîner des `TryMoveFleet`.

### Corrections d'ergonomie après les premiers essais sur téléphone (Phase 20)

Quatre défauts relevés sur l'appareil, invisibles dans l'éditeur :

| Défaut | Cause | Correction |
|---|---|---|
| **Toucher un onglet fermait la fiche** | IMGUI dessine par-dessus la scène mais **ne consomme pas** les entrées du nouvel Input System. `GalaxySelectionController` lisait le pointeur directement, ne rencontrait aucun collisionneur sous le doigt, et en concluait « le joueur a touché le vide ». | `UiScreenRegions` (dans `Espace.Core`, seul espace de noms que l'interface *et* le gameplay référencent) : chaque panneau déclare son rectangle, la sélection l'interroge avant d'agir |
| **Les noms de systèmes grossissaient au zoom et se chevauchaient** | `TextMesh` dimensionné en unités monde. Au zoom rapproché un seul nom barrait le tiers de l'écran ; aucun arbitrage n'existait entre voisins. | Échelle recalculée d'après la taille orthographique, et **placement du plus important au moins important** : un libellé qui empiéterait sur un déjà placé est omis. Perdre un nom vaut mieux qu'en rendre deux illisibles |
| **Libellés tronqués dans la fiche** | Les styles ne fixaient **aucune taille de police** : ils héritaient de la police par défaut, plus grande que les rectangles calculés. IMGUI déborde sans rien signaler. | Tailles explicites dans `UITheme` (`CaptionFontSize` à `TitleFontSize`) et hauteurs de rectangle **dérivées** d'elles via `UITheme.LineHeight`, plus jamais devinées. Deux styles compacts sans retour à la ligne — dans un rectangle d'une ligne, un mot renvoyé à la ligne suivante disparaît purement et simplement |
| **Barre d'état tronquée, « Gestion » et « Menu » hors écran** | `GUILayout` à largeurs fixes demandait **~1330 unités** sur un écran qui en garantit 700. Le débordement est silencieux. | Disposition en rectangles calculés : les commandes sont **ancrées aux deux bords** et le trésor occupe ce qui reste, en n'affichant que les colonnes qui tiennent. Ce qui disparaît en premier est de l'information, jamais une commande |

> **Le trésor s'adapte** de 2 colonnes (700 unités de large) à 5 (923 et au-delà) ; « Gestion » et
> « Menu » restent visibles dans tous les cas. Vérifié par le calcul sur cinq largeurs.
>
> **La fiche a été revérifiée** en 16:9, 19,5:9, 20:9, 21:9 et 22:9, avec et sans message de
> retour : aucun débordement, aucun chevauchement, cibles tactiles au-dessus du seuil.

### Briques de la musique d'ambiance (Phase 21)

Une playlist unique, jouée dès le lancement, dans un ordre fixe et en boucle, du menu
principal jusqu'à la fin de la partie.

| Brique | Rôle |
|---|---|
| `MusicSettings` (`Espace.Core`) | Durées de fondu, intervalle de silence, volume par défaut. Structure pure, **valeurs assainies dans le constructeur** : elles viennent d'un champ d'éditeur, donc de l'extérieur du code |
| `MusicPlaylistCursor` (`Espace.Core`) | Machine à états de l'enchaînement : `Silent → FadingIn → Playing → FadingOut`. **Ne lit aucun son** — il dit seulement quel morceau doit jouer et à quel volume. C'est ce qui rend les fondus vérifiables en EditMode |
| `IMusicService` / `MusicService` (`Espace.Core`) | Possède l'`AudioSource`, traduit les décisions du curseur en appels moteur, garde le volume dans `PlayerPrefs` |
| `MusicLibrary` (`Espace.Core`) | Découverte automatique des fichiers de `Assets/Resources/Music`, triés par nom (comparaison **ordinale**, pas culturelle) |
| `MusicPlaylist` (`Espace.Data`) | Asset **optionnel** : ordre explicite et durées sur mesure, pour qui ne veut pas de l'ordre alphabétique |
| `AudioImportSetup` (`Espace.Editor`) | `Tools → Espace → Configure Audio Import` : force `Streaming` + `Vorbis` sur tous les fichiers audio |

**Ajouter une musique se résume à déposer un fichier** dans `Assets/Resources/Music`, puis à
lancer `Tools → Espace → Configure Audio Import`. Préfixer les fichiers (`01_`, `02_`…) fixe
l'ordre de lecture. Voir `Assets/Resources/Music/LISEZ-MOI.txt`.

Quatre décisions méritent d'être expliquées :

- **Une seule `AudioSource`, pas deux.** Un vrai fondu enchaîné — deux morceaux qui se
  superposent — demanderait deux sources et le double de mémoire de décodage. L'enchaînement
  retenu (fondu de sortie, silence de 3 à 8 s, fondu d'entrée) ne superpose jamais deux
  morceaux : une source suffit.
- **`Streaming`, jamais `Decompress On Load`.** C'est le réglage d'import par défaut d'Unity et
  c'est le plus coûteux qui soit ici : un morceau de trois minutes se retrouve décompressé en
  mémoire, soit environ **30 Mo de PCM pour 3 Mo de fichier**. Quatre morceaux suffisent à faire
  fermer l'application par Android. En streaming, le coût est un tampon de quelques centaines de
  kilo-octets, quelle que soit la durée.
- **Le volume est une préférence d'appareil (`PlayerPrefs`), pas un état de partie.** Le mettre
  dans la sauvegarde imposerait au joueur, en rechargeant, le réglage sonore d'un autre moment —
  voire d'un autre téléphone. Aucun format de sauvegarde n'a donc changé.
- **Rythmée en `unscaledDeltaTime`.** La musique ne suit ni la vitesse ×4 ni la pause : elle
  accompagne le joueur, elle ne simule rien. `GameBootstrap` relaie en plus `OnApplicationPause`,
  sans quoi revenir dans le jeu après quelques minutes reprendrait la playlist deux morceaux plus
  loin.

> **Le jeu fonctionne sans aucun fichier audio** : le service se déclare silencieux en
> *Info* (pas en avertissement — un projet sans musique est un état normal) et le bloc
> « Musique » du menu pause n'apparaît pas.

### Briques de l'espionnage gradué et de l'IA stratégique (Phase 22, P6 et P7)

| Modèle | Rôle |
|---|---|
| `EspionageResolution` | Chance de réussite issue d'un rapport de forces, quatre issues, mise en influence à dimensionner |
| `EmpireAssessment` | Photographie de la situation d'un empire et posture qui en découle |

**L'ancienne règle d'espionnage** tenait en une comparaison : `attaque > défense`, où la défense
valait `10 × stabilité`. Comme la stabilité est toujours inférieure à 1, **l'attaquant gagnait
toujours** à recherche égale. Désormais :

- **La chance vient d'un rapport de forces**, pas d'une comparaison — deux camps à égalité sont à
  50 % quelle que soit leur puissance absolue.
- **Quatre issues, pas deux** : réussite discrète, réussite attribuée, échec discret, échec
  exposé. Réussir et se faire voir sont deux questions distinctes.
- **La mise en influence a des rendements décroissants** (racine carrée) : un empire riche ne
  s'achète pas la certitude, sinon on revient au système binaire.
- **La vigilance monte à chaque tentative** : frapper deux fois au même endroit devient plus dur,
  sans qu'aucun délai arbitraire soit imposé.

**L'IA** réaffirmait chaque mois un taux d'imposition fixe issu de la personnalité — `0,20` pour
un pacifiste, `0,35` pour un militariste — qu'elle soit en faillite ou opulente. Le taux découle
maintenant de la **situation**, tempérée par le caractère à un tiers. L'ordre de priorité des
postures est volontaire : **survivre, puis se défendre, puis grandir, puis frapper**. Un empire au
bord de la faillite ne part plus en guerre même s'il est le plus fort.

> **`EmpireAssessment` ne lit que ce que le joueur voit** sur son interface. Une IA mieux informée
> serait une triche, et une triche n'apprend rien au joueur sur le jeu.

> **`EspionageResolution` est branché dans `EspionageService`.** Le point de passage unique des
> cinq missions (`TryAttempt`) porte toute la nouvelle résolution : **aucune des cinq méthodes
> publiques ni l'interface n'ont changé**, donc aucun appelant n'a été retouché.
>
> **L'influence est la mise, sans nouveau paramètre.** L'opération engage l'influence disponible
> à hauteur de 0,6 par crédit dépensé. La conséquence est systémique et personne n'a eu à
> l'écrire : l'influence paie *aussi* l'administration de l'empire (P4), donc **un empire étalé
> n'a plus les moyens de comploter**. Elle tombe du partage d'une même ressource.
>
> **La vigilance monte à chaque tentative** et retombe de 0,15 par mois : une cible harcelée
> devient dure à reprendre, mais jamais définitivement intouchable.
>
> **Un échec ne coûte plus systématiquement de l'opinion** — seulement s'il est attribué. Une
> réussite attribuée, elle, en coûte désormais.

> **Les quatre postures sont opérationnelles.** `AIDecisionMaker` reçoit désormais
> `IMilitaryService` — que `AIController` détenait déjà pour les décisions militaires, donc
> aucune interface n'a été élargie. Le rapport de forces se lit sur `EmpireHoldings.TotalPower`
> et `NeighboringEmpires`, deux fonctions qui existaient depuis la Phase 5.
>
> **Le rapport se calcule sur les seuls voisins immédiats**, pas sur la galaxie entière : un
> empire lointain trois fois plus puissant ne menace personne tant qu'aucune frontière ne le
> sépare de vous. C'est aussi ce que le joueur perçoit en regardant sa carte — l'IA ne sait rien
> de plus que lui. Sans voisin, le rapport vaut 1 : ni menace, ni proie, l'empire se juge alors
> sur sa seule situation intérieure.
>
> Le rapport est **plafonné à 5** : au-delà, « je domine très largement » et « j'écrase »
> appellent la même décision, et diviser par une puissance quasi nulle ne produirait que des
> nombres absurdes.

#### Une seule photographie pour les cinq preneurs de décision (P8)

`EmpireAssessment` annonçait une photographie « calculée une fois par mois et partagée par tous
ses preneurs de décision ». Elle ne l'était pas : le calcul vivait au fond d'`AIDecisionMaker`,
donc **seul le module économique en bénéficiait**. Recherche, espionnage, diplomatie et armée
décidaient chacun dans leur coin, sans savoir si l'empire était au bord de la faillite.

`EmpireAssessmentFactory.Assess` sort le calcul, `AIController` l'appelle **une fois par empire et
par mois** et passe la structure aux cinq modules. Ce n'est pas qu'une économie de CPU : si chacun
recalculait de son côté, deux modules pourraient lire des valeurs différentes le même mois —
l'économie relâchant les impôts pendant que l'armée se prépare à la guerre.

| Module | Ce que la posture change | Ce qui ne change pas |
|---|---|---|
| Économie | *(inchangé depuis P7)* le taux d'imposition découle de la situation | La personnalité la décale d'un tiers |
| Recherche | Une urgence promeut un domaine devant la liste de personnalité : `Consolidating` → Économie, `Defending` → Armement, `Aggressive` → Logistique | `Expanding` (le cas courant) laisse décider la personnalité seule |
| Espionnage | `Consolidating` → aucune opération ; posture militaire → voisins uniquement ; `Defending` → Découvrir les armées, `Aggressive` → Inciter à la révolte | Une personnalité pacifiste n'espionne jamais, quelle que soit la posture |
| Diplomatie | Pas de déclaration de guerre en `Consolidating` ni `Defending` ; en `Consolidating`, sortie de toutes les guerres, y compris gagnées | `Expanding` et `Aggressive` conservent exactement l'ancien comportement, seuils compris |
| Armée | `Consolidating` → aucune dépense ; `Defending` → cible pleine sur les colonies, ni colonisation ni offensive | `Expanding` et `Aggressive` conservent exactement l'ancien comportement |

> **Aucun bonus n'a été ajouté à l'IA.** Chaque inflexion est soit une règle existante qu'elle
> suspend (le `ColonyGarrisonDivisor` en posture défensive — et elle en paie alors le plein prix
> en entretien mensuel), soit une action qu'elle s'interdit. Une IA qui tient sa frontière le fait
> avec le même budget et les mêmes coûts que le joueur.
>
> **Deux associations méritent leur raison plutôt qu'un tableau.** `Aggressive` → *Inciter à la
> révolte* : la révolte fait chuter la stabilité du système visé, et le contre-espionnage se
> calcule **à partir de cette stabilité** — affaiblir une cible la rend mécaniquement plus
> pénétrable ensuite, sans qu'aucun bonus n'ait été ajouté pour l'obtenir. `Defending` →
> *Armement* pendant que `Aggressive` → *Logistique* : les deux postures militaires ne doivent pas
> chercher la même chose, sinon la distinction entre se défendre et attaquer ne se lit nulle part.
>
> **L'interruption de recherche est gratuite**, et c'est ce qui la rend acceptable :
> `ResearchService` conserve les points domaine par domaine, donc abandonner l'Économie pour
> l'Armement ne perd rien — une posture qui oscillerait ferait perdre du temps, jamais du travail.

---

### Briques des trois freins (Phase 22, P3 à P5)

L'audit relevait que le jeu n'avait **que des moteurs** — population, production, conquête, plus
de production — et aucun frein. Une faction en avance ne pouvait plus être rattrapée.

| Modèle | Frein posé |
|---|---|
| `SubsistenceModel` | La nourriture et l'énergie sont enfin **consommées**. Famine → déclin de population ; panne → production des bâtiments réduite au prorata |
| `AdministrationModel` | Chaque système au-delà de trois coûte de l'**influence**, et le coût par système croît avec la taille |
| `FleetUpkeepModel` | L'entretien impayé provoque une **attrition progressive**, et coûte désormais crédits *et* minerais |

- **Deux contraintes de nature différente**, volontairement. La famine est lente et structurelle,
  elle punit l'expansion sans consolidation. La panne d'énergie est immédiate et réversible, elle
  se corrige en construisant une centrale.
- **Les stocks comptent** : la consommation est prélevée sur le trésor, donc un empire prévoyant
  traverse une mauvaise passe sur ses réserves. C'est ce qui distingue une contrainte d'une
  punition — elle se prépare.
- **Le déficit d'influence se paie en stabilité, pas en interdiction.** Rien n'est bloqué : les
  provinces se tiennent moins bien, ce qui réduit la production, ce qui rend la flotte impayable.
  La spirale est lente et lisible, et l'on en sort en consolidant.
- **L'attrition est graduelle.** Un mois d'impayé coûte 12 % de la flotte, deux ans en laissent
  moins d'un dixième. Une désertion instantanée transformerait une erreur de trésorerie en
  défaite définitive.

**Simulation du coût d'administration** (systèmes de développement 3) :

```
 systèmes   influence due   produite   solde
       10             166        360    +194
       20             550        720    +170
       30           1 027      1 080     +53
       40           1 571      1 440    −131  ← déficit
```

> **Un calibrage faux, rattrapé par la simulation.** Le premier réglage du coût par système
> (1,6) donnait **210 d'influence de charge pour 1 440 produites** à quarante systèmes : le frein
> ne freinait rien du tout. Aucun test ne l'aurait vu — ils vérifiaient la *forme* de la courbe,
> qui était correcte, pas son *échelle*. Le coefficient est désormais calé sur la production
> réelle d'influence, et l'explication du calcul vit à côté de la constante.

> **Période de grâce de trois mois** sur l'attrition après le chargement d'une sauvegarde : une
> partie d'avant cette phase contient des flottes constituées sans que l'entretien ait jamais
> mordu, et les faire fondre au premier mois la rendrait injouable.

---

### Briques de l'économie vivante (Phase 22, P1 et P2)

Issu d'un audit complet des mécaniques. Le constat de départ : **`Population`, `Wealth` et
`Stability` n'avaient aucun point d'écriture en cours de partie.** Fixées à la génération, relues
par la sauvegarde, jamais modifiées — alors qu'elles sont les entrées de la production.
L'économie d'un empire était une **constante** qui ne bougeait que par conquête.

Second constat : **`taxRate` multipliait les crédits linéairement et n'était lu nulle part
ailleurs.** L'optimum était 100 %, toujours. Une décision dont la réponse est constante n'est pas
une décision.

| Modèle | Rôle |
|---|---|
| `PopulationModel` | Croissance logistique plafonnée par le développement. Décline en cas de famine ou de surpeuplement |
| `WealthModel` | La richesse est ce que l'impôt n'a pas pris. Plafonnée par population et développement, érodée chaque mois |
| `TaxationModel` | Évasion fiscale au-delà de 35 % : le taux **perçu** décroche du taux affiché, puis diminue |
| `StabilityModel` | Convergence vers une cible méritée. Une révolte devient un creux dont on se relève |

Ce sont quatre **fonctions pures**, sans `MonoBehaviour` ni état — même découpage que
`OffensivePlanner`, `FleetTravel` et `WorldProfile`. `EconomyService` les appelle sur
`MonthAdvanced` ; la production reste journalière.

**Résultats de simulation** (100 ans, empire développé, hors Unity) :

```
 taux   crédits cumulés   richesse finale   stabilité
  25 %          274 584               660        0,85
  40 %          365 015  ← optimum     568        0,79
  70 %          133 177               200        0,59
 100 %            6 245                62        0,40
```

- **L'optimum est intérieur** (40 %) et **se déplace avec la stabilité** : le joueur ne peut pas
  apprendre un chiffre une fois pour toutes.
- **Taxer à 100 % rapporte 2 % de l'optimum.** La stratégie dominante a disparu.
- **Le rattrapage fonctionne** : un empire parti 5 fois plus pauvre mais bien géré finit 2 fois
  plus riche qu'un leader qui surtaxe — sans qu'aucun bonus n'ait été donné à personne.

> **En dessous de 35 %, la formule historique est inchangée** — choix délibéré : le contenu
> existant n'a pas à être rééquilibré, et les tests de production qui utilisent le taux par
> défaut de 25 % restent valides. Un seul ancien test a dû changer, celui qui affirmait que
> l'impôt à 100 % maximisait le revenu.

> **Un test écrit de travers, corrigé.** `Population_GrowthSlowsAsItFillsUp` comparait
> l'accroissement **absolu** à 10 % et à 90 % de remplissage. Or celui d'une logistique est
> symétrique autour de la moitié : les deux valeurs sont égales par construction, et le test
> échouait sur un comportement parfaitement correct. C'est le taux **relatif** qui décroît.

---

### Briques de la sélection du monde d'origine « orbite » (Phase 21.4)

La chaîne est bouclée : **menu → civilisation → monde → jeu**. La planète tourne en grand à
droite, le dossier se réécrit à gauche à chaque changement de candidat.

| Brique | Rôle |
|---|---|
| `WorldProfile` (`Espace.Gameplay.Galaxy`) | Traduit un `StarSystemState` en portrait comparable : type de monde, trois notes de ressources, difficulté, secteur |
| `FactionPickerController` (étape monde réécrite) | Dossier, navigation entre candidats, planète accrochée à la caméra |
| `PlanetVisual` (posé en 21.1) | Enfin utilisé : c'est cet écran qui le justifiait |

- **Rien n'est inventé.** Le générateur produisait déjà des systèmes très différents — gisements,
  développement, nombre de voisins — mais l'ancien écran n'en montrait que le nom. Le joueur
  choisissait au hasard, non par négligence mais faute d'avoir de quoi choisir autrement.
- **La carte d'aperçu est conservée**, alors que la Phase 13 n'en gardait que les noms : le
  dossier a besoin des gisements et du voisinage pour dresser un portrait.
- **La planète est accrochée à la caméra**, pas posée dans le monde : le fond du menu dérive
  lentement, et une planète en coordonnées absolues sortirait du cadre.

> **Deux défauts trouvés par les tests et le calcul.** Le type de monde reposait d'abord sur des
> seuils absolus — mais le développement relève les trois notes à la fois, si bien qu'un monde
> très développé ne descendait jamais sous le seuil « peu de nourriture » et ne pouvait plus être
> désertique, quels que soient ses gisements. Le type suit désormais le profil *relatif*. Côté
> mise en page, le dossier réclamait 209 unités de haut pour 158 disponibles sur un téléphone
> dense : nom et rang partagent maintenant une ligne, les quatre faits tiennent sur une seule, et
> le nombre de voisins s'efface quand la place manque. 136 unités nécessaires, vérifié sur quatre
> formats.

---

### Briques du choix de civilisation « prise de contrôle » (Phase 21.3)

Les six empires du roster ont désormais un corps. **Aucun n'a été remplacé** : les noms et les
couleurs existants promettaient déjà quelque chose — « Essaim de Kethra » était déjà une espèce
insectoïde — et l'étape s'est contentée de tenir la promesse.

| Espèce | Civilisation | Registre |
|---|---|---|
| Humanoïdes | Fédération de l'Aube | Démocratie de colons, aucune spécialité marquée |
| Chitineux | Essaim de Kethra | Ruche à conscience répartie, expansion maximale |
| Lithoïdes | Bastion de Drathmoor | Caste guerrière de silicate, défense supérieure |
| Céphalopodes | Ligue Marchande d'Oskar | Consortium de familles, économie supérieure |
| Sylvoïdes | Sanctuaire de Vharin | Symbiose végétale, recherche accélérée |
| Synthétiques | Cartel des Confins | Unités affranchies, espionnage supérieur |

| Brique | Rôle |
|---|---|
| `EmblemShape` + `FactionEmblemFactory` (`Espace.Gameplay.Empires`) | Six emblèmes tracés au trait, en textures blanches teintées à l'affichage |
| `EmpireDefinition` (enrichi) | Espèce, devise, description, emblème, quatre axes de doctrine, atout, contrepartie. **Aucun champ existant retiré** |
| `FactionPickerController` (étape faction réécrite) | Onglets, bascule de palette, emblème en filigrane, doctrine, validation |

- **La palette entière bascule avec l'onglet** — fond, traits, barres, bouton, emblème. C'est ce
  basculement, et non un portrait, qui produit la sensation de changer de civilisation. Il a
  l'avantage décisif d'être dérivable d'une seule couleur, donc de valoir aussi pour la septième
  faction que personne n'a encore écrite.
- **Les emblèmes sont au trait, pas en aplats.** Un emblème en surfaces pleines demanderait un
  graphiste pour ne pas paraître pauvre ; au trait, il appartient au registre du plan technique,
  où la simplicité se lit comme un parti pris.
- **Les quatre axes de doctrine sont purement descriptifs.** Aucun service ne les lit : la façon
  dont une personnalité décide reste dans `EmpirePersonalityProfile`, du code et non de la
  donnée. Les brancher sur la simulation serait un changement d'équilibrage déguisé en habillage.

> **Un rognage évité par le calcul.** Les intitulés « ATOUT » et « CONTREPARTIE » au-dessus de
> chaque ligne coûtaient trente unités de hauteur, et sur un écran de 286 unités la contrepartie
> se retrouvait coupée. Un signe `+` / `−` coloré porte la même information pour douze unités de
> large. Colonne vérifiée sur quatre formats : 143 unités nécessaires, 156 disponibles au pire cas.

---

### Briques du menu principal « galaxie vivante » (Phase 21.2)

Le menu montre désormais **la carte du jeu**, survolée lentement par la caméra, avec le menu en
panneau holographique sur le tiers gauche.

| Brique | Rôle |
|---|---|
| `MenuBackdropController` (`Espace.Gameplay.Galaxy`) | Engendre la galaxie d'aperçu, pose fond stellaire, systèmes, routes et capitales, et fait dériver la caméra entre des points d'intérêt |
| `MainMenuController` (`Espace.UI`, réécrit) | Le panneau : titre, quatre commandes, apparition en cascade, secteur observé |

- **La vraie carte, pas une image d'accroche.** Le fond sort du même `GalaxyGenerator` et de la
  même graine que la partie qui va commencer : le joueur regarde la galaxie où il jouera
  réellement. C'est aussi la raison qui a fait retenir ce concept — tout ce qui s'affiche
  existait déjà et était déjà testé.
- **Aucun service, aucune simulation.** La galaxie d'aperçu est un objet jetable, exactement
  comme celui que `FactionPickerController` régénère depuis la Phase 13. Le menu ne fait avancer
  aucune horloge et ne pilote aucun empire.
- **Des halos de capitale, pas des territoires.** Une galaxie qui vient d'être engendrée n'a
  aucun propriétaire : dessiner des zones d'influence reviendrait à inventer un état de partie.
  Les emplacements de départ, eux, sortent du même `EmpirePlacement` que la partie utilisera —
  les six couleurs apparaissent donc sans qu'on ait rien fabriqué.
- **La caméra est reconfigurée au runtime**, pas dans le fichier de scène : la scène `Bootstrap`
  porte une caméra en perspective héritée de la Phase 1, et son état d'origine est restauré à la
  destruction du décor.

> **Un défaut de mise en page trouvé avant Unity.** Le panneau ancre ses commandes en bas, mais
> sur un téléphone très dense l'échelle est bridée par la largeur et l'écran ne fait plus que
> **286 unités de haut** : l'en-tête complet et quatre commandes n'y tenaient pas, et la première
> commande remontait par-dessus le filet de séparation. La hauteur des boutons s'adapte
> désormais, et sur les écrans les plus courts c'est **le sous-titre qui disparaît, jamais une
> commande** — la règle déjà retenue pour la barre d'état en Phase 20. Vérifié par le calcul sur
> six formats, de 16:9 à 22:9 et jusqu'à 700 ppp.

---

### Briques du socle visuel des écrans d'ouverture (Phase 21.1)

Refonte validée : **menu « galaxie vivante »**, **sélection de monde « orbite »**, **choix de
civilisation « prise de contrôle »**. Cette étape ne change rien à l'écran — elle pose les
briques dont les trois écrans dépendent.

| Brique | Rôle |
|---|---|
| `UiEasing` (`Espace.UI`) | Amortissement exponentiel et courbes d'apparition. **Aucune animation en `Lerp` par frame** : la durée dépendrait alors de la cadence, et une transition prendrait deux fois plus de temps à 30 qu'à 60 images par seconde |
| `FactionPalette` (`Espace.UI`) | **Une couleur en entrée, six en sortie.** C'est ce qui rend tenable « ajouter une faction sans refaire l'interface » : une nouvelle civilisation ne demande de renseigner que `EmpireDefinition.Color` |
| `UiTextures` (`Espace.UI`) | Dégradés, cadres à équerres, halos radiaux, générés et mis en cache. Le cache n'est pas une optimisation : IMGUI redessine chaque frame, et les `Texture2D` sont des objets natifs que le ramasse-miettes ne libère pas |
| `PlanetKind` + `PlanetTextureFactory` (`Espace.Gameplay.Galaxy`) | Surface équirectangulaire par bruit fractal : continents, océans, calottes. Six types de monde, purement visuels — **aucune règle de simulation n'en dépend** |
| `PlanetVisual` (`Espace.Gameplay.Galaxy`) | La planète en rotation : sphère texturée, halo derrière, ombre devant. Trois appels de rendu, **aucune lumière, aucun nuanceur maison** |

Trois décisions méritent d'être expliquées :

- **La palette est dérivée en teinte-saturation-valeur, pas en RVB.** Assombrir une couleur en
  multipliant ses trois canaux la désature aussi, et un bleu sombre obtenu ainsi vire au gris.
  En TSV, la teinte de la faction survit jusque dans le fond d'écran le plus sombre — c'est
  précisément ce qui doit donner l'impression de changer de monde.
- **Le terminateur est une ombre peinte, pas une vraie lumière.** Éclairer la sphère exigerait
  un matériau *Lit* et une lumière directionnelle dans une scène qui n'en contient aucune, donc
  un rendu à la merci de la configuration du pipeline — pour un résultat identique. Ici l'ombre
  reste fixe pendant que la planète tourne dessous, ce qui est exactement le comportement
  voulu : c'est l'étoile qui ne bouge pas.
- **La longitude parcourt un cercle dans le plan du bruit**, elle n'est pas une abscisse. Après
  un tour complet l'échantillon retombe sur son point de départ : la carte reboucle sans le
  moindre raccord, sans avoir à fondre les deux bords l'un dans l'autre.

> **Le niveau des mers est déduit du terrain, pas fixé d'avance.** Le premier jet donnait à
> chaque type de monde une altitude de référence — mais la part d'océan qu'elle produit dépend de
> la distribution exacte du bruit, et celle-ci diffère d'une implémentation de Perlin à l'autre.
> Le premier passage du Test Runner dans Unity l'a montré net : un monde réglé « aride » s'y
> retrouvait couvert à **28 % d'eau** là où la calibration hors-ligne en annonçait 10. Chaque type
> déclare désormais la **part de sa surface** qu'il veut immergée, et le niveau correspondant est
> calculé sur le relief réellement engendré (par histogramme, sans tri). La part obtenue est celle
> demandée, quelle que soit la source de bruit — et le test qui a échoué vérifie maintenant cette
> égalité plutôt que des bornes en dur.

> **Deux défauts trouvés par les tests avant toute exécution dans Unity.** Le premier fondu de
> couture, limité aux derniers pourcents de la largeur, rapprochait les deux bords sans jamais
> les faire coïncider : la cicatrice restait. Le second, plus insidieux, n'a été vu qu'en
> mesurant la distribution du relief — la somme d'octaves donnait une cloche si étroite que
> **90 % de la surface tenait entre 0,4 et 0,6**. Rien ne plantait et la carte n'était pas
> plate, mais le niveau des mers devenait un fil de rasoir (deux centièmes séparaient un monde
> sec d'un monde noyé) et toutes les planètes se ressemblaient. `Elevation_SpreadsAcrossItsWholeRange`
> et `SeaLevel_IsNotAKnifeEdge` verrouillent désormais les deux.

---

## 4. Tester la Phase 1

**Au lancement (Play sur `Bootstrap.unity`)** — la console doit afficher, sans aucune
erreur ni warning :

```
[Bootstrap] Configuration appliquee (cible : 60 FPS).
[Bootstrap] 5 services enregistres.
[FSM] Entree dans BootState
[FSM] Sortie de BootState
[FSM] Entree dans MainMenuState - le socle est operationnel.
```

**Depuis la Phase 11**, la fenêtre Game affiche en plus un vrai menu principal (« ESPACE » /
Nouvelle partie / Continuer / Quitter) — voir §5 pour le vérifier en détail. Le bouton
« Continuer » doit rester grisé tant qu'aucune sauvegarde n'existe.

**Tests unitaires** — `Window → General → Test Runner → EditMode → Run All`.
Voir §5 pour le compte total (722 tests, tous packages confondus).

**Build** — `File → Build Settings` : Android et iOS doivent être sélectionnables,
avec **`Bootstrap` en scène 0 et `GalaxyMap` en scène 1**. Si `GalaxyMap` manque, tout
fonctionne encore dans l'éditeur (qui sait charger n'importe quelle scène du projet) mais
« Nouvelle partie » ne fait rien sur l'appareil.

---

## 5. Tester les Phases 2-19 — galaxie, horloge, économie, empires, armées, diplomatie, recherche, espionnage, sauvegarde, interface, carte immersive, flottes, amiraux, colonisation, déplacement longue distance, IA dynamique et territoires

**D'abord, tester le menu principal : ouvrir `Assets/Scenes/Bootstrap.unity` et appuyer sur
Play.** Un panneau centré « ESPACE » doit apparaître avec trois boutons :
- **Continuer** doit être grisé (aucune sauvegarde n'existe encore au tout premier lancement).
- **Nouvelle partie** ouvre désormais l'écran de choix de faction (Phase 13) plutôt que de
  charger la galaxie directement.
- **Quitter** ne fait rien dans l'éditeur (`Application.Quit` n'agit qu'en build).

**Le nouvel écran de choix (Phase 13) :** cliquer **Nouvelle partie** doit afficher 6 boutons
(un par faction — nom, personnalité, pastille de couleur). En choisir une doit afficher un
second écran avec 6 noms de systèmes (les emplacements de départ candidats). Le bouton
**Retour** doit fonctionner aux deux étapes (second écran → premier écran → menu principal).
Choisir un système doit alors charger `GalaxyMap` (la console affiche la suite ci-dessous) —
essayez plusieurs combinaisons faction/système différentes à chaque partie : la console
`[Empires]` doit toujours confirmer que la faction choisie est bien marquée « joueur » et
occupe bien le système choisi (pas systématiquement le plus proche du centre), et que les 5 IA
se répartissent les 5 autres emplacements sans doublon.

**Ensuite, la galaxie elle-même : ouvrir `Assets/Scenes/GalaxyMap.unity` directement et
appuyer sur Play** (raccourci de développement — inutile de repasser par le menu à chaque
test). La console doit afficher, sans erreur ni warning :

```
[Bootstrap] Configuration appliquee (cible : 60 FPS).
[Bootstrap] 5 services enregistres.
[FSM] Entree dans BootState
[FSM] Sortie de BootState
[FSM] Entree dans MainMenuState - le socle est operationnel.
[GalaxyMap] Galaxie generee : 100 systemes, ~125 routes hyperspatiales.
[Economy] Demarree avec 5 types de batiments disponibles.
[Empires] Federation de l'Aube (joueur) : systeme d'origine <nom>.
[Empires] Sanctuaire de Vharin (Pacifist) : systeme d'origine <nom>.
[Empires] Essaim de Kethra (Expansionist) : systeme d'origine <nom>.
[Empires] Ligue Marchande d'Oskar (Mercantile) : systeme d'origine <nom>.
[Empires] Bastion de Drathmoor (Militarist) : systeme d'origine <nom>.
[Empires] Cartel des Confins (Opportunist) : systeme d'origine <nom>.
[Empires] 6 empires crees.
[Military] Demarree avec 7 types d'unites disponibles.
[Diplomacy] Demarree.
[Research] Demarree avec 21 paliers de recherche disponibles.
[Espionage] Demarree.
[Save] Demarree (<chemin>/savegame.json).
```

Dans la fenêtre Game :
- **Un fond spatial** (étoiles éparses, nébuleuses colorées en filaments) derrière toute la
  carte, et **100 systèmes visuellement variés** (plus clairs = plus développés, teintes
  différentes, certains avec un anneau qui tourne lentement, certains avec 1-2 lunes,
  tailles différentes) répartis dans un disque, reliés par un réseau de fines lignes (routes
  hyperspatiales). Le fond et le style de chaque système sont **identiques d'une session à
  l'autre** (mêmes graine que la galaxie).
- Les systèmes que vous ou une IA possédez affichent un **halo de couleur autour du
  marqueur** (couleur propre à chaque empire) : les frontières de chaque territoire doivent
  être visibles d'un coup d'œil, sans avoir à cliquer.
- **Zoomez arrière (molette ou pincement) :** seuls les points/halos restent visibles, aucun
  nom. **Zoomez à un niveau moyen :** le nom de chaque système visible apparaît
  automatiquement au-dessus, sans clic. **Zoomez proche :** une ligne de population et de
  développement s'ajoute sous le nom.
- **Glisser** (clic maintenu + déplacer, ou glisser au doigt) déplace la caméra ; **molette**
  (éditeur) ou **pincement à deux doigts** (mobile) zoome, avec des bornes qui empêchent de
  sortir de la galaxie ou de zoomer à l'infini.
- **Une barre en haut de l'écran** (`HudController`), toujours visible : date courante et
  vitesse (boutons **Pause/Lecture**, **Normal**, **Rapide** x2, **Très rapide** x4,
  **Maximum** x8 — un jour de jeu s'écoule toutes les 2 secondes réelles à vitesse Normale),
  trésor du joueur (5 ressources, abrégées en k/M au-delà de 1000), taux d'imposition courant
  avec boutons **-/+**, et deux boutons **Gestion**/**Menu** à droite.
- **Toucher un système** (tap bref, sans glisser) fait apparaître en bas à gauche le panneau
  du système sélectionné (`SystemInfoPanelController`) : nom, population, richesse,
  développement, stabilité, propriétaire (le nom de l'empire, ou « Independant » pour un
  système encore libre), gisements, nombre de routes, et la garnison présente (par empire,
  avec son nombre d'unités). Le nombre de systèmes « Independant » doit **diminuer au fil du
  temps** si vous laissez tourner l'horloge assez longtemps — les IA colonisent leurs voisins
  libres. Toucher le fond vide referme le panneau.
- **Touchez un système « Independant »** : le panneau affiche en plus, depuis la Phase 16,
  « Colonisation : X Infanterie requise » et le nombre de colons perdus à l'installation. Le
  chiffre doit varier d'un système à l'autre — un système peu peuplé et peu développé exige
  1 unité, un système à 4000 M d'habitants et développement 5 en exige 6 — et les pertes doivent
  être d'autant plus lourdes que la stabilité affichée est basse.
- **Touchez votre système d'origine** (celui portant le nom de votre empire) : le même
  panneau affiche en plus une section Économie (bouton **Investir**, coût croissant, et un
  bouton par type de bâtiment — « (construit) » et inactif une fois bâti, production visible
  dans le trésor une fois la construction achevée). Investissez jusqu'à développement 5/5 :
  le bouton doit alors se désactiver et afficher « Développement maximal atteint », sans
  jamais débiter de crédits au-delà. Section Armée ensuite (garnison et
  puissance estimée, le nom de l'Amiral de la garnison avec ses trois bonus/malus — Attaque,
  Vitesse, Défense, dont un toujours négatif — depuis la Phase 15, un bouton par type d'unité
  pour recruter — 7 depuis la Phase 14 : Infanterie, Blindés, Forces spéciales, Chasseurs,
  Frégate, Croiseur, Cuirassé, chacun avec sa description de rôle affichée en dessous —, un
  bouton par système voisin pour y envoyer toute la garnison, et depuis la Phase 17 un bouton
  **« Déplacer une flotte »** qui arme un mode de ciblage : le prochain système touché sur la
  carte — **n'importe lequel, si lointain soit-il** — devient la destination, l'itinéraire est
  calculé automatiquement le long des routes hyperspatiales et la durée dépend de la distance
  totale. Un clic dans le vide annule le ciblage). Recruter au-delà de 10 unités
  sur un même système doit être refusé (message en console) : le plafond par flotte introduit
  en Phase 14. Envoyer une garnison vers un système libre le colonise à l'arrivée **si elle
  transporte assez d'Infanterie** (Phase 16) — sinon le départ est refusé avec un message en
  console nommant l'exigence ; à l'arrivée, les colons perdus sont décomptés et le reste de la
  flotte devient la garnison de la nouvelle colonie (visible dans l'onglet Flottes). Vers un
  système ennemi, déclenche une bataille — le résultat (victoire/défaite, pertes des deux
  camps) est systématiquement journalisé dans la console, même panneau fermé. **Une victoire
  sans Infanterie survivante détruit la garnison adverse mais ne capture pas le système**
  (console : « victoire sans occupation ») : essayez d'attaquer avec une flotte de Chasseurs
  seuls pour le vérifier.
- **Vérifiez le déplacement longue distance (Phase 17)** : envoyez une flotte vers un système
  **lointain** et laissez tourner l'horloge. L'onglet Flottes doit montrer l'itinéraire restant
  et la date d'arrivée ; les systèmes **traversés en chemin ne doivent jamais être colonisés**
  (ce ne sont que des points de navigation) ; seule la destination finale déclenche une
  colonisation, un renfort ou une bataille. Sauvegardez pendant qu'une flotte est en vol puis
  rechargez : elle doit reprendre son trajet au bon endroit — et **recharger deux fois de suite
  ne doit pas la dupliquer**.
- **Vérifiez les rencontres spatiales (Phase 17)** : envoyez deux flottes de camps différents
  dans un même couloir. Quand elles se croisent, une **fenêtre de rencontre** s'ouvre et met le
  jeu en pause, avec des options cohérentes avec le statut diplomatique — Combattre / Se
  replier / Négocier en guerre, Négocier / Commercer / Piraterie / Passer son chemin en paix,
  jamais de Piraterie envers un allié ou un partenaire de pacte. Choisir « Se replier » ne doit
  **pas** rouvrir immédiatement une rencontre avec la flotte qu'on vient de fuir. Fermer la
  fenêtre relance le temps si c'est bien elle qui l'avait mis en pause.
- **Touchez le système d'origine d'une IA** : le panneau doit afficher le nom de cet empire
  comme propriétaire, et sa garnison si elle en a recruté une — confirmation visuelle que
  l'attribution et l'armée IA fonctionnent pour les 5 IA, pas seulement le joueur.
- **Vérifiez l'IA dynamique (Phase 18)** : laissez tourner l'horloge en Maximum sur plusieurs
  années de jeu. Les IA doivent **posséder plusieurs systèmes** (onglet Empires), et leurs
  colonies doivent monter en développement et recevoir une garnison — avant cette phase, elles
  restaient éternellement au développement 0 et sans la moindre unité. L'onglet Flottes montre
  des trajets IA de **plusieurs sauts** : elles ne se limitent plus à leurs voisins directs.
  L'**Expansionniste doit visiblement s'étendre plus loin que le Pacifiste** (rayons de 6 et 2
  sauts), et le **Militariste doit avoir le système le plus développé** de la galaxie plutôt
  que plusieurs systèmes moyens — c'est la seule personnalité qui concentre son effort sur sa
  capitale.
- **Le bouton « Gestion »** ouvre une fenêtre centrale à six onglets (`ManagementWindowController`) :
  - **Empires** : les 6 empires (nom, rôle — « Vous » pour le joueur, la personnalité pour
    chaque IA —, nombre de systèmes, Credits). En accélérant l'horloge (Maximum), les Credits
    des 5 IA doivent progresser **sans aucune intervention** — la preuve la plus directe que
    l'IA fonctionne.
  - **Flottes** *(Phase 14, étendu Phase 15)* : liste des flottes du joueur, chacune avec son
    nom (« Flotte N »), sa position (système d'origine, ou « En route vers... » si en
    déplacement), sa composition, sa puissance estimée, et le nom de son Amiral avec ses trois
    bonus/malus (Attaque, Vitesse, Défense). Recruter puis détacher/envoyer une garnison doit
    faire apparaître une nouvelle entrée, avec un Amiral différent de celui de la garnison
    d'origine ; tenter d'envoyer une deuxième flotte en même temps sans avoir recherché la
    Logistique doit être refusé (message en console) — le plafond de flottes en campagne
    simultanée introduit en Phase 14. Une flotte dont l'Amiral a un bonus de vitesse positif
    doit arriver plus vite qu'une flotte identique sans ce bonus.
  - **Diplomatie** : statut/opinion envers chaque IA avec boutons d'action (Guerre, Pacte,
    Alliance, Paix, Rompre selon le statut courant), et les propositions reçues en attente
    (Accepter/Refuser). En laissant tourner l'horloge en Maximum, les IA doivent se déclarer
    la guerre entre elles ou se proposer des pactes selon leur personnalité — visible en
    filtrant la console sur `[Diplomacy]` — et une attaque IA ne doit jamais survenir sans
    qu'une ligne `[Diplomacy] ... declare la guerre` ne l'ait précédée.
  - **Recherche** : domaine actif (« Aucun » au tout début), palier et bonus courants des 7
    domaines, bouton **Activer** par domaine. Activez un domaine puis accélérez l'horloge :
    son palier doit progresser et finir par se compléter (console `[Research]`), et son bonus
    (ex. Économie) doit se répercuter sur la production correspondante dans le trésor.
  - **Espionnage** : puissance d'espionnage du joueur et, pour chaque IA, son
    contre-espionnage estimé avec cinq boutons (Vol tech, Sabotage, Révolte, Influence,
    Découvrir). Une mission contre une cible faible doit réussir sans laisser de trace côté
    opinion ; contre une cible forte, elle doit échouer, coûter quand même le crédit dépensé,
    et faire chuter l'opinion de la cible envers vous — visible en filtrant la console sur
    `[Espionage]`.
  - **Sauvegarde** : état du fichier, boutons **Sauvegarder maintenant**/**Recharger**. Jouez
    quelques mois, changez des choses (impôts, construction, recherche...), sauvegardez,
    modifiez encore l'état, puis rechargez : tout doit revenir exactement à l'état sauvegardé.
- **Le bouton « Menu »** ouvre le menu pause (`PauseMenuController`, met l'horloge en pause) :
  Reprendre, Sauvegarder maintenant, Recharger, Menu principal (retour à `Bootstrap` via
  `ISceneLoader`), Quitter le jeu. Depuis le menu principal, **Continuer** doit maintenant être
  actif et vous ramener exactement où vous étiez.
- **Quittez complètement Play et relancez** (`GalaxyMap.unity` directement, ou via
  Continuer) : la console doit afficher `[Save] Sauvegarde existante chargee au demarrage.` et
  la partie doit reprendre exactement où elle en était, sur la **même** galaxie (positions et
  noms de systèmes identiques d'une session à l'autre, grâce à la graine désormais fixe).

**Tests unitaires** (inclus dans le Run All du Test Runner, 722 au total) :
`GalaxyGeneratorTests`, `GalaxyMapTests`, `HyperlaneLinkTests`, `StarSystemNameGeneratorTests`
(Phase 2) ; `GameDateTests`, `GameClockSettingsTests`, `GameClockTests` (Phase 3) ;
`ResourceBundleTests`, `EconomyServiceTests` (Phase 4, plus des tests Phase 5/6 sur la
séparation des trésors par empire) ; `EmpirePlacementTests`, `EmpireFactoryTests`,
`EmpireRegistryTests`, `AIDecisionMakerTests` (Phase 5, étendus en Phase 13 avec
`AssignHomeSystems` — réordonnancement pur des emplacements candidats — et
`playerDefinitionOverride` — n'importe quelle faction peut devenir le joueur, avec repli sur le
comportement d'avant cette phase si absent ou hors roster) ; `UnitBundleTests` (Phase 6, étendu
en Phase 14 avec les 3 nouveaux types de vaisseaux — `Get`/`Of`/`Scale`/`IsGreaterOrEqualTo`),
`CombatResolverTests` (Phase 6 — le plus important : vainqueur déterministe selon le ratio de
puissance, fractions de pertes vérifiées valeur par valeur, cas limites d'une défense vide ou
de deux camps à puissance nulle), `MilitaryServiceTests` (recrutement → garnison, colonisation,
combat avec transfert de propriété, retraite après défaite, fusion de garnisons, entretien,
blocage d'un déplacement vers un système étranger sans guerre déclarée, le plafond de 10 unités
par flotte au recrutement (Phase 14), le plafond de flottes en déplacement simultané lié à la
Logistique (Phase 14), `GetFleetsForEmpire`, le nom automatique d'une flotte, et désormais —
Phase 15 — le bonus de vitesse d'un Amiral qui raccourcit un trajet, les bonus d'attaque/défense
qui augmentent la puissance observée sur `BattleResolvedEvent`, un système non défendu qui ne
casse pas le calcul avec une flotte défenseuse nulle, `RestoreGarrison` qui préserve un Amiral
explicite ou en génère un déterministe, une flotte détachée qui obtient son propre Amiral),
`MilitaryDecisionMakerTests` (chaque personnalité respecte son seuil d'agressivité — le
Pacifiste n'attaque jamais même en surnombre écrasant —, une seule action par appel, aucune
attaque sans guerre déjà déclarée par la diplomatie, et désormais — Phase 14 — la réserve
d'Infanterie en priorité généralisée aux 7 types d'unités) ; `DiplomacyServiceTests`
(Phase 7 — le plus important : statut symétrique/opinion dirigée, validation des préconditions
par type de proposition, résolution instantanée pour une IA cible contre mise en attente pour
le joueur, effets de chaque proposition acceptée — échange de ressources/territoires,
tribut d'ultimatum —, refus d'un ultimatum déclenchant une guerre automatique, dérive mensuelle
de l'opinion, revenu de traité commercial), `DiplomacyDecisionMakerTests` (propose la paix
avant d'envisager la guerre, ne déclare la guerre que si l'avantage dépasse le seuil de la
personnalité, ne propose un pacte qu'au-dessus du seuil d'opinion, une seule action par appel) ;
`ResearchServiceTests` (Phase 8 — génération journalière de points selon population/développement,
progression et complétion de palier avec report du surplus sur le palier suivant, plusieurs
paliers complétés le même jour si les points le permettent, points perdus sans exception une
fois le domaine au maximum, bonus cumulatif, progressions indépendantes entre domaines et
entre empires), `ResearchDecisionMakerTests` (choisit le premier domaine non maximal dans
l'ordre de préférence de la personnalité, ne change rien tant que le domaine actif progresse
encore) ; `EspionageServiceTests` (Phase 9 — le plus important : succès/échec déterministe
selon le rapport de puissance, coût toujours payé même en cas d'échec, pénalité d'opinion sur
échec, effet propre à chaque mission — vol du domaine à l'écart le plus grand, sabotage réduit
le développement, révolte réduit la stabilité sans jamais devenir négative, influence améliore
l'opinion, découverte retourne la garnison réelle —, dégradation propre quand un service
optionnel est indisponible), `EspionageDecisionMakerTests` (seuil et mission préférée propres
à chaque personnalité, le Pacifiste n'espionne jamais, ignore les cibles sans territoire sans
planter) ; `SaveServiceTests` (Phase 10 — aller-retour complet capture puis application pour
chaque type d'état : systèmes, bâtiments complétés uniquement, trésor et taux d'imposition,
garnisons, relations/opinions diplomatiques, progression de recherche et domaine actif, date
de l'horloge ; robustesse face à un fichier absent ou corrompu sans jamais lever d'exception ;
les méthodes `Restore*` ne publient aucun événement ; étendu en Phase 14 avec l'aller-retour des
4 nouveaux types de vaisseaux et du nom de flotte, et en Phase 15 avec l'aller-retour exact de
l'Amiral — nom et trois bonus — plutôt qu'un nouveau généré à la volée) ; `HudFormatterTests` et
`SaveFileLocatorTests` (Phase 11 — formatage des dates/montants/pourcentages, chemin et
existence du fichier de sauvegarde ; seule logique de cette phase qui ne touche ni `OnGUI` ni
`ServiceLocator`, donc la seule testable en EditMode) ; `StarSystemVisualProfileTests`
(Phase 12 — déterminisme, variété sur 100 systèmes, bornes valides du nombre de lunes et du
facteur de taille ; seule logique de cette phase qui ne touche ni `OnGUI` ni le rendu — le
fond, les halos et les labels restent vérifiables seulement en Play Mode, voir plus bas) ;
`AdmiralTests` (Phase 15 — déterminisme du hachage, décorrélation par `ownerId` et par
`fleetId`, bornes valides des trois bonus, exactement un malus garanti, nom jamais vide, variété
sur un grand échantillon, aller-retour exact du constructeur direct) ; `ColonizationRulesTests`
(Phase 16 — exigence toujours entre 1 et 6 sur l'intégralité des plages générées, monotonie en
population et en développement, pertes toujours entre 1 et l'exigence, pertes décroissantes avec
la stabilité, consommation totale à stabilité minimale, valeurs de référence) ;
`HyperlanePathfinderTests` (Phase 17 — chemin trouvé sur un graphe multi-sauts, plus court en
**distance** et non en nombre de sauts, extrémités incluses, chemin trivial origine = destination,
aucun chemin si le filtre bloque tous les intermédiaires, **le prédicat ne s'applique pas aux
extrémités**, départage déterministe) ; `EncounterRulesTests` (Phase 17 — issues disponibles par
statut diplomatique, jamais de Piraterie envers un allié ou sous pacte, le Pacifiste
(`AggressionThreshold` nul) ne choisit jamais Combattre ni Piraterie, repli sous le seuil de
rapport de force, commerce si traité commercial, déterminisme, aucune division par zéro). `EmpireHoldingsTests`, `FleetRoutingTests` et `ExpansionPlannerTests` (Phase 18 — capitale =
le système le plus développé et jamais délogée par une colonie neuve, puissance sommée sur tout
le territoire, voisins déduits de **tous** les systèmes possédés ; parcours en largeur
multi-source avec la bonne origine, système étranger **atteint mais jamais traversé**, rayon
d'expansion respecté, déterminisme ; cible la moins exigeante à portée, **aucun plan renvoyé
tant que la garnison ne suffit pas** — donc aucun détachement orphelin —, réserve conservée,
offensive sur le système ennemi le moins défendu, aucune offensive hors état de guerre).
`TerritoryTests` (Phase 19 — chaque système à l'intérieur de sa propre cellule, **aucun point
d'une cellule plus proche d'un autre système** (la propriété de Voronoï elle-même), arête taguée
« voisin *j* » toujours équidistante des deux sites, aire strictement positive, déterminisme,
arguments invalides rejetés ; éventail de remplissage bien dimensionné et dégradé du centre vers
le bord, **arête intérieure à un empire jamais dessinée** — c'est ce qui fait la différence
entre un bloc continu et une mosaïque —, frontière contestée plus intense qu'une façade sur le
vide, rubans jamais hors du disque, maillage vidé quand un empire perd tout ; paliers de lecture,
opacités monotones, frontière toujours plus lisible que la zone qu'elle borde, nom de faction
éteint dès que les noms de systèmes prennent le relais).
`FleetPresentationTests` (Phase 20 — avancement nul au départ, moitié à mi-parcours, **borné aux
deux extrémités** donc un vaisseau ne sort jamais de son tronçon même immobilisé par une
rencontre spatiale, étape de durée nulle qui ne divise pas par zéro, cap suivant le sens de
marche, points confondus renvoyant un cap défini, flotte stationnée qui ne dessine rien, et un
balayage de 160 jours vérifiant qu'aucune position ne dépasse les extrémités). La
Phase 14
(refonte des flottes) n'introduit pas de nouvelle classe de test dédiée : ses ajouts (plafonds,
`GetFleetsForEmpire`, généralisation de `SplitAttackForce`, nouveaux types de vaisseaux)
étendent des classes existantes, listées ci-dessus à leur phase d'origine ; l'onglet Flottes de
`ManagementWindowController` reste, comme le reste de `Espace.UI`, vérifiable seulement en Play
Mode. La Phase 16 étend de même `MilitaryServiceTests` (refus de départ faute d'Infanterie,
reste de la flotte en garnison sur la colonie, dissolution quand la flotte n'embarquait que le
strict nécessaire, victoire sans Infanterie qui ne capture pas, victoire avec Infanterie qui
capture toujours) et `MilitaryDecisionMakerTests` (l'IA vise le voisin le moins exigeant, recrute
au-delà de sa cible de personnalité quand un système libre est à portée, recrute de l'Infanterie
en guerre quand elle n'en a aucune). La Phase 17 étend à son tour `MilitaryServiceTests`
(déplacement multi-sauts réussi, **un point de passage libre n'est jamais colonisé**,
destination changée de propriétaire en vol → repli et non bataille, durée totale égale à
`⌈distance totale / vitesse⌉`, rencontre déclenchée une seule fois par tronçon, aucune rencontre
entre flottes du même empire, un repli issu d'une rencontre ne re-déclenche pas, une flotte gelée
en attente de décision compte toujours comme déployée) et `SaveServiceTests` (aller-retour d'une
flotte en transit avec itinéraire, étape, dates, nom et Amiral ; **recharger deux fois ne
duplique pas les flottes en vol**) ; `EncounterWindowController` reste, comme tout `Espace.UI`,
vérifiable seulement en Play Mode. La Phase 18 étend enfin `MilitaryDecisionMakerTests` (recrute
sur le système le plus loin de sa cible et non sur la capitale, colonise au-delà des voisins
directs, **un point de passage n'est jamais colonisé**, et surtout **plafond de flottes atteint →
aucun détachement**), `AIDecisionMakerTests` (investit sur le système le moins développé, le
Militariste sur sa capitale, départage sur l'identifiant à développement égal) et
`DiplomacyDecisionMakerTests` (un rival ne bordant qu'une colonie est bien pris en compte,
puissance sommée sur tout le territoire, aucun empire limitrophe → aucune action).

**Points à vérifier en priorité sur appareil réel** — la partie la plus délicate à garantir
sans pouvoir ouvrir l'éditeur ici :
- le geste de pincement (`GalaxyCameraController`, API `EnhancedTouch`) et la distinction
  tap/glisser (`GalaxySelectionController`) ;
- que les boutons du HUD, du panneau système, de la fenêtre de gestion et des menus
  répondent bien au tactile (traduit automatiquement par Unity sur Android/iOS, mais un
  point à confirmer sur appareil) ;
- que la sauvegarde survit bien à une mise en arrière-plan réelle de l'application (pas
  seulement à un Play/Stop dans l'éditeur), le scénario mobile le plus courant.

La logique de génération de galaxie, celle de l'horloge/calendrier, la formule de production
économique, le placement des systèmes d'origine (*farthest-point sampling*), l'arbitrage de
décision de l'IA par personnalité, la formule de combat, les décisions militaires de l'IA,
l'évaluation des propositions diplomatiques et l'arbitrage guerre/paix/pacte de
`DiplomacyDecisionMaker`, la génération/progression/complétion des paliers de recherche de
`ResearchService`, et les formules de puissance/contre-espionnage et de vol de technologie
d'`EspionageService`, ont chacune été recoupées indépendamment par un script Python qui
reproduit l'algorithme (voir les commentaires de `GalaxyGenerator`, `GameClock`,
`EconomyService`, `EmpirePlacement`, `AIDecisionMaker`, `CombatResolver`,
`MilitaryDecisionMaker`, `ProposalEvaluator`, `DiplomacyDecisionMaker`, `ResearchService` et
`EspionageService` pour le détail) ; la Phase 10 n'introduit pas de nouvelle formule mais un
script Python recoupe tout de même la logique de filtrage de `SaveService.Capture` (quelles
entrées valent la peine d'être écrites) et la fidélité d'un aller-retour JSON. La Phase 11
recoupe de la même façon le formatage de `HudFormatter` (abréviations k/M, arrondi des
pourcentages) — le reste de cette phase (agencement `OnGUI`) est de la présentation pure,
vérifiable seulement en Play Mode, pas par un script indépendant. La Phase 12 recoupe le
mélange de bits et la dérivation des attributs visuels de `StarSystemVisualProfile`
(déterminisme, bornes, variété sur un grand échantillon, proportion d'anneaux proche du
réglage configuré) — le fond spatial, les halos de territoire et les labels de zoom restent,
eux, du rendu pur, non testables en EditMode comme le reste du rendu du projet. La Phase 13
recoupe le réordonnancement d'`EmpirePlacement.AssignHomeSystems` (identité pour un index
absent ou hors bornes, emplacement choisi en tête, ordre relatif du reste conservé, aucune
perte ni duplication d'emplacement) — `FactionPickerController` (agencement `OnGUI`) reste,
comme tous les écrans `Espace.UI`, vérifiable seulement en Play Mode. La Phase 14 recoupe la
réserve en priorité de l'Infanterie de `MilitaryDecisionMaker.SplitAttackForce` (généralisée
aux 7 types) et les deux plafonds introduits (10 unités par flotte au recrutement, nombre de
flottes en déplacement simultané lié aux paliers de Logistique complétés) — l'onglet Flottes de
`ManagementWindowController` reste, lui aussi, vérifiable seulement en Play Mode. La Phase 15
recoupe le mélange de bits d'`Admiral.Compute` (déterminisme, décorrélation par `fleetId` et par
`ownerId`, bornes des trois bonus, contrainte « exactement un malus » sur un grand échantillon).
La Phase 16 recoupe les deux formules de `ColonizationRules` sur l'intégralité des plages
générées (exigence bornée à 1..6, monotonie en population et en développement, pertes bornées à
1..exigence et décroissantes avec la stabilité), plus la propriété structurante qui rend l'IA
viable : la cible de garnison effective dans le pire cas (2 gardées + 6 requises = 8) reste sous
le plafond de 10 unités par flotte de la Phase 14. La Phase 17 réimplémente enfin en Python un
Dijkstra pondéré par la distance à partir de la seule description du plan, et le confronte à
`HyperlanePathfinder` sur des graphes construits pour piéger l'implémentation (détour imposé par
le prédicat, égalité parfaite entre deux itinéraires, composantes disjointes, destination bloquée
par le prédicat) ; le même script recoupe l'arithmétique de durée — arrondi unique sur la
distance cumulée, monotonie étape par étape, plancher d'un jour par étape, et le fait qu'un
trajet de 3 sauts de 10 unités à vitesse 4 coûte 8 jours là où un arrondi par étape en coûterait
9 — puis vérifie que le couloir de test des rencontres garantit bien un chevauchement des deux
flottes pour **les neuf combinaisons** de bonus de vitesse d'Amiral possibles. La Phase 18
réimplémente à son tour le parcours en largeur multi-source à partir de la seule description du
plan et le confronte sur des graphes construits pour piéger l'implémentation (deux sources
concurrentes, système étranger terminal, contournement forcé d'un tiers, rayon borné), vérifie
que la règle de choix de cible à trois critères est un **ordre total** — donc insensible à
l'ordre d'itération du dictionnaire, ce qui dispense de tout départage aléatoire —, et chiffre
les deux propriétés qui rendent l'IA viable : un parcours unique en O(V+E) contre les ~10⁶
opérations qu'un Dijkstra par candidat coûterait chaque mois, et une cible de garnison ramenée
de 40 à 24 unités pour un Militariste à cinq systèmes.

### Vérifier les territoires (Phase 19)

**Au zoom le plus éloigné**, chaque empire doit apparaître comme un **bloc coloré continu** —
pas une constellation de disques —, avec son nom en majuscules posé en son centre. Deux empires
qui se touchent doivent afficher une frontière **vive et doublée** (chacun sa couleur, de son
côté) ; une bordure donnant sur le vide ou sur un système libre doit rester à peine visible.
C'est la lecture principale : d'un coup d'œil, on doit voir qui est encerclé et où sont les
fronts.

**En zoomant progressivement**, quatre choses doivent se produire dans cet ordre : le nom de
faction s'efface (vers 30-46 % de zoom), les noms de systèmes prennent le relais (comportement
inchangé depuis la Phase 12), la couleur des zones s'atténue continûment (0,30 → 0,08), et
l'épaisseur des frontières diminue par paliers pour rester à peu près constante à l'écran.
**Aucune saccade ne doit être perceptible** au franchissement d'un palier : seules les
frontières sont reconstruites, en ~0,12 ms.

**En conquérant ou en colonisant un système** (attendre qu'une IA le fasse, ou le faire
soi-même) : sa cellule doit changer de couleur immédiatement, et la frontière doit se
**redessiner au bon endroit** — un système pris à un voisin fait avancer la ligne de front d'une
cellule entière, il ne se contente pas de changer de teinte.

**Vérification chiffrée** — le découpage est confronté à sa propre définition dans
`TerritoryTests` : chaque système est à l'intérieur de sa cellule, aucun point d'une cellule
n'est plus proche d'un autre système, chaque arête taguée « voisin *j* » est effectivement
équidistante des deux sites, et les cellules **pavent exactement** le disque galactique (écart
d'aire cumulée mesuré : 0,0000 %). Cette dernière propriété est la plus utile : une seule
cellule mal découpée laisserait un trou ou un recouvrement, invisible à l'œil sur une carte
sombre mais fatal dès qu'on colorie deux empires voisins.

### Ajouter et vérifier la musique (Phase 21)

1. **Déposer les fichiers** dans `Assets/Resources/Music`, nommés `01_…`, `02_…` pour fixer
   l'ordre (`.ogg` de préférence). Le dossier contient un `LISEZ-MOI.txt` qui rappelle la marche
   à suivre et sert de tableau des sources et licences.
2. **Lancer `Tools → Espace → Configure Audio Import`.** La console indique combien de fichiers
   ont été réimportés en `Streaming`/`Vorbis`. **Cette étape n'est pas facultative** : sans elle,
   quelques morceaux suffisent à faire fermer l'application par Android (voir §3, Phase 21).
3. **Play sur `Bootstrap.unity`.** La console doit afficher
   `[Music] N morceau(x) charge(s), volume 60 %`, et le premier morceau démarre en fondu dès
   l'écran de menu.
4. **Vérifier l'enchaînement** — laisser tourner jusqu'à la fin d'un morceau : le son doit
   descendre en 2 s, laisser 3 à 8 s de silence, puis remonter en 2 s sur le morceau suivant. Le
   dernier morceau enchaîne sur le premier.
5. **Vérifier la persistance entre scènes** — « Nouvelle partie », puis menu pause →
   « Menu principal » : la musique **ne doit pas repartir du début** à chaque changement d'écran.
6. **Régler le volume** — menu pause, ligne « Musique » : « Son coupé / Son actif » et les
   boutons `-` / `+`. Le titre du morceau en cours est affiché. Le réglage doit survivre à une
   fermeture complète de l'application (il est dans `PlayerPrefs`, pas dans la sauvegarde).

Sans aucun fichier audio, la console affiche `[Music] Aucun morceau charge` en *Info* et le bloc
« Musique » du menu pause n'apparaît pas — c'est le comportement attendu, pas un défaut.

**Tests unitaires** (inclus dans le Run All du Test Runner) : `MusicPlaylistCursorTests` (17) et
`MusicSettingsTests` (4) couvrent le démarrage immédiat, la montée et la descente des fondus, la
durée du silence et son bornage, la boucle sur une playlist de 1 et de 3 morceaux, le morceau
plus court que le fondu, la playlist vide et la frame de durée nulle.

---

## 6. Feuille de route

| Phase | Contenu | État |
|---|---|---|
| 1 | Socle technique : services, événements, machine à états, configuration | ✅ terminée |
| 2 | Carte galactique : 100 systèmes, génération procédurale, caméra tactile, sélection | ✅ terminée |
| 3 | Horloge de jeu : temps continu, pause, vitesses | ✅ terminée |
| 4 | Économie : production, bâtiments, impôts, investissement | ✅ terminée |
| 5 | Empires et IA de base (personnalités, gestion économique autonome) | ✅ terminée |
| 6 | Armées, résolution automatique des combats, colonisation | ✅ terminée |
| 7 | Diplomatie (alliances, traités, embargos, ultimatums...) | ✅ terminée |
| 8 | Recherche (arbre technologique, 7 domaines) | ✅ terminée |
| 9 | Espionnage (agents, sabotage, vol de technologie) | ✅ terminée |
| 10 | Sauvegarde JSON automatique | ✅ terminée |
| 11 | Interface complète (menu, écrans de gestion, HUD) | ✅ terminée |
| 12 | Carte galactique immersive (fond, systèmes stylés, territoires, zoom) + corrections | ✅ terminée |
| 13 | Choix de faction et de système de départ | ✅ terminée |
| 14 | Refonte des flottes (rôles des vaisseaux, flottes nommées, plafond lié à la technologie) | ✅ terminée |
| 15 | Amiraux (bonus/malus, un par flotte) | ✅ terminée |
| 16 | Colonisation stratégique (population/développement/stabilité/défense, pertes dynamiques) | ✅ terminée |
| 17 | Déplacement longue distance (itinéraire automatique, durée selon la distance) + rencontres spatiales | ✅ terminée |
| 18 | IA plus dynamique (multi-système, expansion longue distance, rayon par personnalité) | ✅ terminée |
| 19 | Territoires et frontières (cellules de contrôle, frontières contestées, noms de factions, échelle de lecture) | ✅ terminée |
| 20.1 | Flottes visibles et sélectionnables sur la carte (vaisseaux orientés, trajectoires) | ✅ terminée |
| 20.2 | Fiche de système refondue (Concept B) : composition de flotte, colonisation explicite | ✅ terminée |
| 20.3 | Planification d'offensive et suivi des opérations | ✅ terminée |
| 21 | Musique d'ambiance (playlist en boucle, fondus, réglage du volume) | ✅ terminée |
| 21.1 | Socle visuel des écrans d'ouverture (palettes par faction, habillage, rendu de planète) | ✅ terminée |
| 21.2 | Menu principal « galaxie vivante » | ✅ terminée |
| 21.3 | Choix de civilisation « prise de contrôle » + six espèces | ✅ terminée |
| 21.4 | Sélection du monde d'origine « orbite » | ✅ terminée |
| 22.1 | Démographie et richesse vivantes | ✅ terminée |
| 22.2 | Fiscalité non linéaire et stabilité évolutive | ✅ terminée |
| 22.3 | Nourriture et énergie réellement consommées | ✅ terminée |
| 22.4 | Coût d'administration payé en influence | ✅ terminée |
| 22.5 | Entretien de flotte contraignant | ✅ terminée |
| 22.6 | Espionnage gradué, branché dans le service | ✅ terminée |
| 22.7 | Évaluation stratégique de l'IA | ✅ terminée |
| 22.8 | Les cinq preneurs de décision partagent la même évaluation | ✅ terminée |

Chaque phase est développée, testée et validée avant de passer à la suivante. Un seul système
complexe à la fois (consigne du brief), toujours en vigueur : les Phases 12 à 18 remplacent
l'ancienne Phase 12 « Équilibrage », éclatée en sept phases après une demande de refonte
étendue (carte immersive, choix de faction, flottes, amiraux, colonisation, déplacement, IA)
formulée une fois le jeu testé pour la première fois dans l'éditeur. Les points 7 (technologie
→ nombre de flottes), 8 (refonte des flottes) et 9 (rôles des vaisseaux) de cette demande sont
regroupés en une seule Phase 14 : « flotte » doit devenir une entité persistante et nommée
avant qu'un plafond ou un rôle par type de vaisseau ait un sens, les séparer forcerait à
réécrire deux fois la même chose. L'IA (Phase 18) a été volontairement traitée en dernier : elle
pilote déjà économie/recherche/espionnage/diplomatie/armée une fois par mois chacune, et la
retoucher avant la refonte des flottes/colonisation/déplacement aurait obligé à la retoucher une
seconde fois une fois ces mécaniques changées — ce qui s'est vérifié, la Phase 18 consistant
précisément à rattraper les Phases 16 et 17 dans les cinq decision makers. La **Phase 19** est
née d'une demande distincte, formulée après la refonte V1 : rendre les zones d'influence
réellement visibles. Elle regroupe cette demande et la mise à l'échelle des libellés selon le
zoom, parce que les deux se répondent — des zones pleines à intensité constante ruineraient la
lisibilité au zoom rapproché, et des noms de factions n'auraient aucun sens sans territoires
pour les porter. La vision multi-planètes par système (demandée
pour une version future) n'est pas une phase à part : c'est une contrainte de conception
respectée dans chacune des phases ci-dessus plutôt qu'une fonctionnalité à construire
maintenant.

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
