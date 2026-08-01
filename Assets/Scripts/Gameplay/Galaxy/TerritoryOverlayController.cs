using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Empires;
using UnityEngine;

namespace Espace.Gameplay.Galaxy
{
    /// <summary>
    /// Zones d'influence des empires sur la carte galactique : chaque systeme colore sa cellule
    /// de controle, les cellules d'un meme empire forment un bloc continu, et seules les
    /// frontieres avec un <i>autre</i> empire sont soulignees (Phase 19, concept « frontieres
    /// stellaires »).
    /// <para>
    /// <b>Remplace les halos de la Phase 12.</b> Un halo par systeme ne se rejoignait jamais a
    /// celui de son voisin : l'œil lisait des taches, jamais un territoire, et encore moins une
    /// ligne de front. Le decoupage en cellules (<see cref="TerritoryPartition"/>) donne des
    /// frontieres exactes pour un cout <i>inferieur</i> a l'ancien rendu — deux maillages
    /// combines la ou cent <see cref="SpriteRenderer"/> etaient mis a jour a chaque frame.
    /// </para>
    /// <para>
    /// <b>Deux sources de reconstruction, deliberement dissociees :</b> le remplissage ne depend
    /// que des proprietaires, les frontieres dependent en plus du palier de zoom (leur largeur
    /// doit rester a peu pres constante a l'ecran, voir
    /// <see cref="TerritoryLevelOfDetail.BorderWidthFor"/>). Un changement de zoom ne
    /// reconstruit donc jamais le remplissage. L'<i>opacite</i>, elle, ne reconstruit rien du
    /// tout : elle est portee par la teinte des materiaux, les couleurs de sommets ne stockant
    /// qu'une intensite relative.
    /// </para>
    /// <para>
    /// <b>Changement de proprietaire detecte par sondage, comme en Phase 12 :</b> au moins deux
    /// evenements peuvent changer un proprietaire (<c>SystemColonizedEvent</c>,
    /// <c>BattleResolvedEvent</c>) et rien ne garantit qu'il n'y en aura pas d'autres (echange
    /// diplomatique de territoire...). Comparer les <see cref="StarSystemState.OwnerId"/> a un
    /// cache reste correct quelle que soit la cause du changement, pour une centaine de
    /// comparaisons d'entiers par frame.
    /// </para>
    /// </summary>
    public sealed class TerritoryOverlayController : MonoBehaviour
    {
        /// <summary>
        /// Profondeur Z du remplissage : celle qu'occupaient les halos de la Phase 12, donc
        /// derriere les routes hyperspatiales (0,1) et les marqueurs de systeme (0), devant le
        /// fond spatial (2). Conserver cette valeur garantit que l'ordre de rendu de la scene
        /// est exactement celui deja valide.
        /// </summary>
        private const float FillDepth = 0.5f;

        /// <summary>Profondeur Z des frontieres : juste devant le remplissage, jamais devant les routes.</summary>
        private const float BorderDepth = 0.45f;

        /// <summary>Ecart d'opacite en deca duquel la teinte du materiau n'est pas reecrite.</summary>
        private const float AlphaEpsilon = 0.002f;

        /// <summary>
        /// Nuanciers acceptables, du plus adapte au dernier recours. Tous multiplient la couleur
        /// de sommet et melangent en transparence — les deux conditions du rendu des zones.
        /// <para>
        /// <c>Sprites/Default</c> vient en tete parce que c'est deja celui qu'utilisent tous les
        /// <see cref="SpriteRenderer"/> de la scene : son comportement est donc verifie dans ce
        /// projet precis. <c>UI/Default</c> ferme la marche car il force un test de profondeur
        /// permissif hors d'un Canvas, ce qui ferait passer les zones devant les systemes.
        /// </para>
        /// </summary>
        private static readonly string[] ShaderCandidates =
        {
            "Sprites/Default",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "UI/Default",
        };

        private readonly TerritoryMeshData _meshData = new TerritoryMeshData();
        private readonly Dictionary<int, Color> _ownerColors = new Dictionary<int, Color>();

        private GalaxyMap _map;
        private IReadOnlyList<TerritoryCell> _cells;
        private GalaxyCameraController _cameraController;
        private Camera _camera;

        private int[] _ownerIds;
        private int[] _lastKnownOwnerIds;

        private EmpireRegistry _empireRegistry;

        private Mesh _fillMesh;
        private Mesh _borderMesh;
        private Material _fillMaterial;
        private Material _borderMaterial;

        private bool _fillDirty;
        private bool _bordersDirty;
        private bool _hasLoggedFirstFill;
        private bool _hasTier;
        private TerritoryDetailTier _tier;
        private float _appliedFillAlpha = -1f;
        private float _appliedBorderAlpha = -1f;

        /// <summary>
        /// Cable l'affichage. Appele une seule fois par <see cref="GalaxyMapController"/>, qui
        /// possede deja le decoupage.
        /// </summary>
        /// <param name="map">Galaxie affichee.</param>
        /// <param name="cells">Cellules de controle, <b>dans le meme ordre que <c>map.Systems</c></b>.</param>
        /// <param name="cameraController">Camera de la carte, pour lire les bornes de zoom.</param>
        /// <param name="camera">Camera rendue, pour lire le zoom courant.</param>
        public void Initialize(
            GalaxyMap map,
            IReadOnlyList<TerritoryCell> cells,
            GalaxyCameraController cameraController,
            Camera camera)
        {
            _map = map;
            _cells = cells;
            _cameraController = cameraController;
            _camera = camera;

            if (_map == null || _cells == null || _cells.Count != _map.Systems.Count)
            {
                GameLog.Error("[Territoires] Decoupage absent ou desynchronise de la galaxie : les zones ne seront pas affichees.");
                _cells = null;
                return;
            }

            _ownerIds = new int[_map.Systems.Count];
            _lastKnownOwnerIds = new int[_map.Systems.Count];
            for (int i = 0; i < _ownerIds.Length; i++)
            {
                _ownerIds[i] = StarSystemState.UnownedOwnerId;
                _lastKnownOwnerIds[i] = StarSystemState.UnownedOwnerId;
            }

            Shader shader = FindShader();
            if (shader == null)
            {
                GameLog.Error("[Territoires] Aucun nuancier transparent disponible : les zones ne seront pas affichees.");
                _cells = null;
                return;
            }

            _fillMesh = CreateMesh("TerritoryFill");
            _borderMesh = CreateMesh("TerritoryBorders");
            _fillMaterial = CreateMaterial(shader, "TerritoryFillMaterial");
            _borderMaterial = CreateMaterial(shader, "TerritoryBorderMaterial");

            CreateRenderer("Fill", _fillMesh, _fillMaterial);
            CreateRenderer("Borders", _borderMesh, _borderMaterial);

            // Trace de demarrage, sur le meme modele que « [GalaxyMap] Galaxie generee : ... » :
            // c'est le seul moyen, sur un appareil, de distinguer « les zones ne s'affichent
            // pas » de « c'est l'ancien binaire qui tourne ».
            GameLog.Info($"[Territoires] {_cells.Count} cellules de controle decoupees, nuancier « {shader.name} ».");
        }

        private void Update()
        {
            if (_cells == null)
            {
                return;
            }

            ResolveEmpireRegistry();
            DetectOwnershipChanges();
            UpdateLevelOfDetail();

            if (_fillDirty)
            {
                TerritoryMeshBuilder.BuildFill(_cells, _ownerIds, _ownerColors, FillDepth, _meshData);
                _meshData.ApplyTo(_fillMesh);
                _fillDirty = false;

                // Une seule fois, a la premiere reconstruction porteuse de geometrie : si ce
                // compte reste a zero alors que des empires existent, c'est que les couleurs ne
                // sont pas indexees sur les memes identifiants que StarSystemState.OwnerId.
                if (!_hasLoggedFirstFill && _meshData.Vertices.Count > 0)
                {
                    _hasLoggedFirstFill = true;
                    GameLog.Info($"[Territoires] Zones affichees : {_meshData.Triangles.Count / 3} triangles pour {_ownerColors.Count} empires.");
                }
            }

            if (_bordersDirty)
            {
                TerritoryMeshBuilder.BuildBorders(
                    _cells, _ownerIds, _ownerColors, TerritoryLevelOfDetail.BorderWidthFor(_tier), BorderDepth, _meshData);
                _meshData.ApplyTo(_borderMesh);
                _bordersDirty = false;
            }
        }

        private void OnDestroy()
        {
            // Maillages et materiaux crees par code : rien ne les libere automatiquement, et un
            // retour au menu principal suivi d'une nouvelle partie en creerait un jeu de plus a
            // chaque fois.
            DestroyGenerated(_fillMesh);
            DestroyGenerated(_borderMesh);
            DestroyGenerated(_fillMaterial);
            DestroyGenerated(_borderMaterial);
        }

        /// <summary>
        /// Le registre des empires n'existe qu'a partir du <c>Start</c> d'<c>EmpireController</c> :
        /// les zones restent donc vides au tout debut de la scene, puis se colorent des que les
        /// empires et leurs systemes d'origine sont attribues.
        /// </summary>
        private void ResolveEmpireRegistry()
        {
            if (_empireRegistry != null || !ServiceLocator.TryGet(out _empireRegistry))
            {
                return;
            }

            _ownerColors.Clear();
            foreach (Empire empire in _empireRegistry.Empires)
            {
                _ownerColors[empire.Id] = empire.Color;
            }

            _fillDirty = true;
            _bordersDirty = true;
        }

        private void DetectOwnershipChanges()
        {
            IReadOnlyList<StarSystemState> systems = _map.Systems;
            bool changed = false;

            for (int i = 0; i < systems.Count; i++)
            {
                int owner = systems[i].OwnerId;
                if (_lastKnownOwnerIds[i] == owner)
                {
                    continue;
                }

                _lastKnownOwnerIds[i] = owner;
                _ownerIds[i] = owner;
                changed = true;
            }

            if (changed)
            {
                _fillDirty = true;
                _bordersDirty = true;
            }
        }

        private void UpdateLevelOfDetail()
        {
            if (_cameraController == null || _camera == null)
            {
                return;
            }

            float zoomInFraction = TerritoryLevelOfDetail.ZoomInFraction(
                _camera.orthographicSize,
                _cameraController.MinOrthographicSize,
                _cameraController.MaxOrthographicSize);

            TerritoryDetailTier tier = TerritoryLevelOfDetail.TierFor(zoomInFraction);
            if (!_hasTier || tier != _tier)
            {
                _hasTier = true;
                _tier = tier;
                _bordersDirty = true;
            }

            ApplyAlpha(_fillMaterial, TerritoryLevelOfDetail.FillAlphaFor(zoomInFraction), ref _appliedFillAlpha);
            ApplyAlpha(_borderMaterial, TerritoryLevelOfDetail.BorderAlphaFor(zoomInFraction), ref _appliedBorderAlpha);
        }

        /// <summary>
        /// Ecrit l'opacite globale dans la teinte du materiau, seulement si elle a bouge de
        /// facon perceptible : la carte est immobile la plupart du temps, inutile de payer un
        /// appel natif par frame pour reecrire la meme valeur.
        /// </summary>
        private static void ApplyAlpha(Material material, float alpha, ref float applied)
        {
            if (material == null || Mathf.Abs(alpha - applied) < AlphaEpsilon)
            {
                return;
            }

            applied = alpha;
            material.color = new Color(1f, 1f, 1f, alpha);
        }

        private static Shader FindShader()
        {
            foreach (string name in ShaderCandidates)
            {
                Shader shader = Shader.Find(name);
                if (shader != null)
                {
                    return shader;
                }
            }

            return null;
        }

        private static Mesh CreateMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            return mesh;
        }

        /// <summary>
        /// Cree le materiau et fixe explicitement les proprietes que <c>Sprites/Default</c>
        /// attend normalement du <see cref="SpriteRenderer"/> (<c>_RendererColor</c>,
        /// <c>_Flip</c>). Elles ont une valeur par defaut correcte dans le nuancier, mais un
        /// <see cref="MeshRenderer"/> ne les alimente pas : les poser ici rend le rendu
        /// independant de ce detail d'implementation.
        /// </summary>
        private static Material CreateMaterial(Shader shader, string name)
        {
            var material = new Material(shader) { name = name };

            if (material.HasProperty("_RendererColor"))
            {
                material.SetColor("_RendererColor", Color.white);
            }

            if (material.HasProperty("_Flip"))
            {
                material.SetVector("_Flip", Vector4.one);
            }

            material.color = new Color(1f, 1f, 1f, 0f);
            return material;
        }

        private void CreateRenderer(string name, Mesh mesh, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, worldPositionStays: false);

            child.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            // Hygiene mobile : la carte est en 2D non eclairee, tout le pipeline d'ombres et de
            // sondes de lumiere est du travail pur perte sur ces deux maillages.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private static void DestroyGenerated(Object generated)
        {
            if (generated == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generated);
            }
            else
            {
                DestroyImmediate(generated);
            }
        }
    }
}
