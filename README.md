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

> **Statut : Phase 9 terminée** — carte galactique (100 systèmes), horloge de jeu, économie,
> 6 empires (1 joueur + 5 IA), armées (recrutement, résolution automatique des combats,
> colonisation), diplomatie (guerre/paix/alliances/pactes de non-agression, opinion, traités
> commerciaux, embargos, ultimatums, échanges de ressources et de territoires), recherche
> (7 domaines, 3 paliers chacun, bonus sur la production, le combat, la vitesse des flottes et
> les gains d'opinion), et désormais espionnage : cinq missions déterministes (vol de
> technologie, sabotage, incitation à la révolte, influence de gouvernement, découverte
> d'armées) résolues selon un rapport de puissance — jamais de hasard, mais une mission ratée
> est toujours découverte et coûte une pénalité d'opinion. Le domaine de recherche Espionnage,
> banqué depuis la Phase 8, a désormais un effet réel.

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
│   └── GalaxyMap.unity           # scène jouable : galaxie + horloge + économie + empires + armées + diplomatie + recherche + espionnage (Phases 2-9)
├── Settings/                     # assets URP (générés par le script de setup)
├── ScriptableObjects/            # instances de données éditables
│   ├── GameConfig.asset
│   ├── GameClockConfig.asset
│   ├── GalaxyConfig.asset
│   ├── Buildings/                # 5 types de bâtiments (1 par ressource)
│   ├── Empires/                  # 6 empires : le joueur + 1 par personnalité IA
│   ├── Units/                    # 4 types d'unités (Infanterie, Blindés, Forces spéciales, Flotte spatiale)
│   └── Research/                 # 21 paliers de recherche (3 x 7 domaines)
├── Scripts/
│   ├── Core/                     # → Espace.Core     (aucune dépendance sortante)
│   ├── Data/                     # → Espace.Data     (ScriptableObjects et types génériques)
│   ├── Managers/                 # → Espace.Managers (composition de l'application)
│   ├── Gameplay/                 # → Espace.Gameplay (référence Core + Data)
│   │   ├── Galaxy/               #     carte galactique, génération, caméra, sélection
│   │   ├── Economy/              #     production, bâtiments, impôts, investissement
│   │   ├── Empires/              #     identité, personnalités, décisions IA autonomes
│   │   ├── Military/             #     unités, flottes, combat automatique, colonisation
│   │   ├── Diplomacy/            #     statut guerre/paix/alliance, opinion, propositions
│   │   ├── Research/             #     domaines, paliers, points, bonus par domaine
│   │   └── Espionage/            #     missions déterministes, puissance/contre-espionnage
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
> `BuildingType`, `EmpireDefinition`) vivent dans `Espace.Gameplay`, pas dans `Espace.Data`.
> Seuls les types véritablement transverses (`ResourceType`, `ResourceBundle`, `GameConfig`)
> restent dans `Espace.Data`.
>
> L'assembly `Espace.UI` sera ajoutée avec ses premiers scripts (Phase 11).

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
| `EmpireDebugPanel` | contrôle temporaire | liste les 6 empires (nom, personnalité, systèmes, Credits) en haut de l'écran — seul moyen d'observer l'IA sans dérouler la console |

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
| `UnitType` / `UnitTypeDefinition` | contenu | 4 types (Infanterie, Blindés, Forces spéciales, Flotte spatiale), ScriptableObject — puissance, vitesse, coûts, durée de recrutement, entretien, développement minimal requis |
| `UnitBundle` | quantité d'unités | même pattern que `ResourceBundle` (Phase 4) : 4 champs `int` nommés plutôt qu'un tableau, pour la même raison d'immuabilité réelle |
| `Fleet` | groupe d'unités | stationnée (garnison d'un système) ou en déplacement ; **au plus une flotte stationnée par (système, propriétaire)** — toute arrivée fusionne avec la garnison existante, ce qui évite à la résolution de combat de devoir combiner plusieurs flottes du même camp |
| `CombatResolver` | résolution de bataille | **déterministe, sans hasard** : la puissance de chaque camp (quantité × puissance du catalogue, modulée par moral/commandement/terrain) décide du vainqueur ; la fraction de pertes de chaque camp est proportionnelle à la puissance adverse relative au total — testable sans stub de générateur aléatoire |
| `IMilitaryService` / `MilitaryService` | armées de tous les empires | même architecture que `EconomyService` : recrutement en file (mirroring `BuildingInstance`), entretien journalier prélevé via `IEconomyService.TrySpend` (nouvelle méthode générique, réutilisée aussi par la construction/l'investissement pour éviter de dupliquer la logique de dépense) |
| `MilitaryDecisionMaker` | décision militaire IA | même séparation que `AIDecisionMaker` : recrutement jusqu'à la garnison cible, puis colonisation d'un voisin libre, puis — seulement pour les personnalités qui s'y autorisent — une attaque ; **une seule action par appel**, toujours au moins 2 unités gardées à domicile |
| `MilitaryController` | orchestration | seul composant Phase 6 à dépendre d'un autre `Start()` non garanti (`EmpireRegistry`, `IEconomyService`) : initialisation différée à `Update` plutôt qu'à un événement, faute d'événement naturel à attendre pour un service qui doit exister avant que d'autres ne le cherchent |
| `MilitaryDebugPanel` | contrôle temporaire | recrutement et envoi de la garnison entière vers un voisin, empilé au-dessus de l'encart de construction économique |

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
| `DiplomacyDebugPanel` | contrôle temporaire | statut/opinion envers chaque IA avec boutons d'action (guerre, pacte, alliance, paix, rupture), et les propositions reçues en attente (accepter/refuser) ; échanges de ressources/territoires et ultimatums non exposés en bouton (charge utile numérique peu adaptée à l'IMGUI tactile) mais entièrement implémentés et testés au niveau du service |

**Résolution des propositions : instantanée pour l'IA, en attente pour le joueur.** Une
proposition dont la cible est une IA est évaluée et résolue au moment même où elle est
soumise (`ProposalEvaluator`, appelé par `DiplomacyService`) — pas de file d'attente pour des
décisions qui n'ont pas besoin d'attendre une saisie humaine. Une proposition qui cible le
joueur est mise en attente et publiée via `ProposalReceivedEvent`, jusqu'à ce que
`TryRespondToProposal` soit appelée (bouton du `DiplomacyDebugPanel`).

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
| `ResearchDebugPanel` | contrôle temporaire | domaine actif, palier et bonus courants des 7 domaines, bouton pour rediriger le focus — empilé en bas à gauche, au-dessus du trésor |

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
| `EspionageDebugPanel` | contrôle temporaire | puissance d'espionnage du joueur, un bouton par mission pour chaque IA, dernier résultat de découverte d'armées — empilé en haut à droite, sous `DiplomacyDebugPanel` |

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
Voir §5 pour le compte total (340 tests, tous packages confondus).

**Build** — `File → Build Settings` : Android et iOS doivent être sélectionnables,
avec `Bootstrap` en scène 0.

---

## 5. Tester les Phases 2-9 — galaxie, horloge, économie, empires, armées, diplomatie, recherche et espionnage

**Ouvrir `Assets/Scenes/GalaxyMap.unity` et appuyer sur Play.** La console doit afficher,
sans erreur ni warning :

```
[Bootstrap] Configuration appliquee (cible : 60 FPS).
[Bootstrap] 4 services enregistres.
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
[Military] Demarree avec 4 types d'unites disponibles.
[Diplomacy] Demarree.
[Research] Demarree avec 21 paliers de recherche disponibles.
[Espionage] Demarree.
```

Dans la fenêtre Game :
- **100 points colorés** (plus clairs = plus développés) répartis dans un disque, reliés par
  un réseau de fines lignes (routes hyperspatiales).
- **Glisser** (clic maintenu + déplacer, ou glisser au doigt) déplace la caméra ; **molette**
  (éditeur) ou **pincement à deux doigts** (mobile) zoome, avec des bornes qui empêchent de
  sortir de la galaxie ou de zoomer à l'infini.
- **Toucher un système** (tap bref, sans glisser) affiche son détail dans l'encart en haut à
  gauche : nom, population, richesse, développement, stabilité, propriétaire (le nom de
  l'empire, ou « Independant » pour un système encore libre), gisements, nombre de routes,
  et **désormais la garnison** présente (par empire, avec son nombre d'unités). Le nombre de
  systèmes « Independant » doit **diminuer au fil du temps** si vous laissez tourner l'horloge
  assez longtemps — les IA colonisent leurs voisins libres. Toucher le fond vide referme
  l'encart.
- **En haut à droite**, un second encart affiche la date courante (format `0001-01-02`) et
  la vitesse. Avec des réglages par défaut, un jour de jeu s'écoule toutes les 2 secondes
  réelles. Boutons : **Pause/Lecture**, **Normal**, **Rapide** (x2), **Très rapide** (x4),
  **Maximum** (x8).
- **En haut au centre**, un nouvel encart liste les **6 empires** : nom, rôle (« Vous » pour
  le joueur, la personnalité pour chaque IA), nombre de systèmes, Credits en réserve. En
  accélérant l'horloge (Maximum), les Credits des 5 IA doivent progresser **sans aucune
  intervention** — c'est la preuve la plus directe que l'IA fonctionne.
- **En bas à gauche**, le trésor du joueur (5 ressources) et le taux d'imposition courant
  (25% par défaut), avec des boutons **-10%/+10%**.
- **Juste au-dessus**, un septième encart affiche votre recherche : domaine actif (« Aucun »
  au tout début), palier et bonus courants des 7 domaines, et un bouton **Activer** par
  domaine. Activez un domaine puis accélérez l'horloge : son palier doit progresser et
  finir par se compléter (visible en filtrant la console sur `[Research]`), et son bonus
  (ex. Économie) doit se répercuter sur la production correspondante dans le trésor.
- **Touchez votre système d'origine** (celui portant le nom de votre empire) : un troisième
  encart apparaît en bas à droite avec un bouton **Investir** (augmente le développement,
  coût croissant) et un bouton par type de bâtiment. Un bâtiment déjà construit affiche
  « (construit) » et devient inactif ; sa production doit apparaître dans le trésor une fois
  sa durée de construction écoulée.
- **Un quatrième encart, empilé juste au-dessus du précédent**, affiche votre garnison
  (nombre d'unités et puissance estimée), un bouton par type d'unité pour recruter (visible
  après le délai de recrutement), et un bouton par système voisin pour y envoyer toute votre
  garnison. Envoyer une garnison vers un système libre le colonise à l'arrivée ; vers un
  système ennemi, déclenche une bataille — le résultat (victoire/défaite, pertes des deux
  camps) est systématiquement journalisé dans la console, même sans ce panneau ouvert.
- **Touchez le système d'origine d'une IA** : le panneau du haut-gauche doit afficher le nom
  de cet empire comme propriétaire, et sa garnison si elle en a recruté une — confirmation
  visuelle que l'attribution et l'armée IA fonctionnent pour les 5 IA, pas seulement le joueur.
- **En haut à droite**, un sixième encart liste votre relation avec chacune des 5 IA (statut,
  opinion) avec des boutons d'action (Guerre, Pacte, Alliance, Paix, Rompre selon le statut
  courant), et les propositions reçues en attente avec des boutons Accepter/Refuser. En
  laissant tourner l'horloge en Maximum, les IA doivent se déclarer la guerre entre elles ou se
  proposer des pactes selon leur personnalité — visible en filtrant la console sur `[Diplomacy]`
  — et une attaque IA ne doit plus jamais survenir sans qu'une ligne `[Diplomacy] ... declare la
  guerre` ne l'ait précédée.
- **Juste en dessous**, un huitième encart affiche votre puissance d'espionnage et, pour
  chaque IA, son contre-espionnage estimé avec cinq boutons (Vol tech, Sabotage, Révolte,
  Influence, Découvrir). Tentez une mission contre une cible faible (contre-espionnage bas) :
  elle doit réussir sans laisser de trace côté opinion. Tentez-en une contre une cible forte :
  elle doit échouer, vous coûter quand même le crédit dépensé, et l'opinion de la cible envers
  vous doit chuter — visible en filtrant la console sur `[Espionage]`.

> Ces huit encarts sont des outils de mise au point temporaires (IMGUI), pas les écrans
> finaux (Phase 11) — voir les commentaires de `GalaxyMapController`, `GameClockDebugPanel`,
> `EconomyDebugPanel`, `EmpireDebugPanel`, `MilitaryDebugPanel`, `DiplomacyDebugPanel`,
> `ResearchDebugPanel` et `EspionageDebugPanel`.

**Tests unitaires** (inclus dans le Run All du Test Runner, 340 au total) :
`GalaxyGeneratorTests`, `GalaxyMapTests`, `HyperlaneLinkTests`, `StarSystemNameGeneratorTests`
(Phase 2) ; `GameDateTests`, `GameClockSettingsTests`, `GameClockTests` (Phase 3) ;
`ResourceBundleTests`, `EconomyServiceTests` (Phase 4, plus des tests Phase 5/6 sur la
séparation des trésors par empire) ; `EmpirePlacementTests`, `EmpireFactoryTests`,
`EmpireRegistryTests`, `AIDecisionMakerTests` (Phase 5) ; `UnitBundleTests`,
`CombatResolverTests` (Phase 6 — le plus important : vainqueur déterministe selon le ratio de
puissance, fractions de pertes vérifiées valeur par valeur, cas limites d'une défense vide ou
de deux camps à puissance nulle), `MilitaryServiceTests` (recrutement → garnison, colonisation,
combat avec transfert de propriété, retraite après défaite, fusion de garnisons, entretien, et
désormais le blocage d'un déplacement vers un système étranger sans guerre déclarée),
`MilitaryDecisionMakerTests` (chaque personnalité respecte son seuil d'agressivité — le
Pacifiste n'attaque jamais même en surnombre écrasant —, une seule action par appel, et
désormais aucune attaque sans guerre déjà déclarée par la diplomatie) ; `DiplomacyServiceTests`
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
planter).

**Points à vérifier en priorité sur appareil réel** — la partie la plus délicate à garantir
sans pouvoir ouvrir l'éditeur ici :
- le geste de pincement (`GalaxyCameraController`, API `EnhancedTouch`) et la distinction
  tap/glisser (`GalaxySelectionController`) ;
- que les boutons des huit panneaux IMGUI répondent bien au tactile (traduit
  automatiquement par Unity sur Android/iOS, mais un point à confirmer sur appareil).

La logique de génération de galaxie, celle de l'horloge/calendrier, la formule de production
économique, le placement des systèmes d'origine (*farthest-point sampling*), l'arbitrage de
décision de l'IA par personnalité, la formule de combat, les décisions militaires de l'IA,
l'évaluation des propositions diplomatiques et l'arbitrage guerre/paix/pacte de
`DiplomacyDecisionMaker`, la génération/progression/complétion des paliers de recherche de
`ResearchService`, et désormais les formules de puissance/contre-espionnage et de vol de
technologie d'`EspionageService`, ont chacune été recoupées indépendamment par un script
Python qui reproduit l'algorithme : voir les commentaires de `GalaxyGenerator`, `GameClock`,
`EconomyService`, `EmpirePlacement`, `AIDecisionMaker`, `CombatResolver`,
`MilitaryDecisionMaker`, `ProposalEvaluator`, `DiplomacyDecisionMaker`, `ResearchService` et
`EspionageService` pour le détail.

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
| 10 | Sauvegarde JSON automatique | à venir |
| 11 | Interface complète (menu, écrans de gestion, HUD) | à venir |
| 12 | Équilibrage | à venir |

Chaque phase est développée, testée et validée avant de passer à la suivante. Un seul système
complexe à la fois (consigne du brief) : la Phase 9 n'a touché ni la sauvegarde, ni l'interface
finale — tous les systèmes de jeu du brief (économie, diplomatie, recherche, espionnage,
guerre) sont désormais implémentés, il ne reste que la persistance et l'habillage.

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
