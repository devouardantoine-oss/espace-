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

> **Statut : Phase 15 terminée** — carte galactique (100 systèmes), horloge de jeu, économie,
> 6 empires (1 joueur + 5 IA), armées (sept types d'unités dont quatre classes de vaisseaux —
> Chasseurs, Frégate, Croiseur, Cuirassé —, flottes nommées et commandées chacune par un Amiral
> aux bonus/malus propres, plafonnées à 10 unités, nombre de flottes en campagne simultanée lié
> à la recherche en Logistique, recrutement, résolution automatique des combats, colonisation),
> diplomatie (guerre/paix/alliances/pactes de
> non-agression, opinion, traités commerciaux, embargos, ultimatums, échanges de ressources et
> de territoires), recherche (7 domaines, 3 paliers chacun, bonus sur la production, le combat,
> la vitesse des flottes et les gains d'opinion), espionnage (cinq missions déterministes selon
> un rapport de puissance), une sauvegarde JSON automatique (la partie reprend exactement où
> elle en était après une fermeture ou une mise en arrière-plan, sur un seul fichier local), une
> interface complète (menu principal, barre d'état permanente, panneau de système contextuel,
> fenêtre de gestion à onglets — dont un onglet Flottes — et menu pause), une carte galactique
> immersive (fond spatial procédural, systèmes stylés, halos de territoire par couleur d'empire,
> noms/détails affichés selon le niveau de zoom), et un écran de choix en début de partie : le
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
> des flottes (Phase 14) et amiraux (Phase 15, ci-dessus) sont terminées ; restent la
> colonisation stratégique, le déplacement longue distance et les rencontres spatiales, et une
> IA plus dynamique. Un seul système complexe à la fois, comme depuis la Phase 1.

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
│   └── GalaxyMap.unity           # scène jouable : galaxie + horloge + économie + empires + armées + diplomatie + recherche + espionnage + sauvegarde (Phases 2-10)
├── Settings/                     # assets URP (générés par le script de setup)
├── ScriptableObjects/            # instances de données éditables
│   ├── GameConfig.asset
│   ├── GameClockConfig.asset
│   ├── GalaxyConfig.asset        # graine fixe depuis la Phase 10 (voir §3, Briques de la sauvegarde)
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

**Depuis la Phase 11**, la fenêtre Game affiche en plus un vrai menu principal (« ESPACE » /
Nouvelle partie / Continuer / Quitter) — voir §5 pour le vérifier en détail. Le bouton
« Continuer » doit rester grisé tant qu'aucune sauvegarde n'existe.

**Tests unitaires** — `Window → General → Test Runner → EditMode → Run All`.
Voir §5 pour le compte total (434 tests, tous packages confondus).

**Build** — `File → Build Settings` : Android et iOS doivent être sélectionnables,
avec `Bootstrap` en scène 0.

---

## 5. Tester les Phases 2-15 — galaxie, horloge, économie, empires, armées, diplomatie, recherche, espionnage, sauvegarde, interface, carte immersive, flottes et amiraux

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
  bouton par système voisin pour y envoyer toute la garnison). Recruter au-delà de 10 unités
  sur un même système doit être refusé (message en console) : le plafond par flotte introduit
  en Phase 14. Envoyer une garnison vers un système libre le colonise à l'arrivée ; vers un
  système ennemi, déclenche une bataille — le résultat
  (victoire/défaite, pertes des deux camps) est systématiquement journalisé dans la console,
  même panneau fermé.
- **Touchez le système d'origine d'une IA** : le panneau doit afficher le nom de cet empire
  comme propriétaire, et sa garnison si elle en a recruté une — confirmation visuelle que
  l'attribution et l'armée IA fonctionnent pour les 5 IA, pas seulement le joueur.
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

**Tests unitaires** (inclus dans le Run All du Test Runner, 434 au total) :
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
sur un grand échantillon, aller-retour exact du constructeur direct). La Phase 14 (refonte des
flottes) n'introduit pas de nouvelle classe de test dédiée : ses ajouts (plafonds,
`GetFleetsForEmpire`, généralisation de `SplitAttackForce`, nouveaux types de vaisseaux)
étendent des classes existantes, listées ci-dessus à leur phase d'origine ; l'onglet Flottes de
`ManagementWindowController` reste, comme le reste de `Espace.UI`, vérifiable seulement en Play
Mode.

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
| 16 | Colonisation stratégique (population/développement/stabilité/défense, pertes dynamiques) | à venir |
| 17 | Déplacement longue distance + rencontres spatiales | à venir |
| 18 | IA plus dynamique, ajustée aux nouvelles règles | à venir |

Chaque phase est développée, testée et validée avant de passer à la suivante. Un seul système
complexe à la fois (consigne du brief), toujours en vigueur : les Phases 12 à 18 remplacent
l'ancienne Phase 12 « Équilibrage », éclatée en sept phases après une demande de refonte
étendue (carte immersive, choix de faction, flottes, amiraux, colonisation, déplacement, IA)
formulée une fois le jeu testé pour la première fois dans l'éditeur. Les points 7 (technologie
→ nombre de flottes), 8 (refonte des flottes) et 9 (rôles des vaisseaux) de cette demande sont
regroupés en une seule Phase 14 : « flotte » doit devenir une entité persistante et nommée
avant qu'un plafond ou un rôle par type de vaisseau ait un sens, les séparer forcerait à
réécrire deux fois la même chose. L'IA (Phase 18) est volontairement traitée en dernier : elle
pilote déjà économie/recherche/espionnage/diplomatie/armée une fois par mois chacune, et la
retoucher avant la refonte des flottes/colonisation/déplacement obligerait à la retoucher une
seconde fois une fois ces mécaniques changées. La vision multi-planètes par système (demandée
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
