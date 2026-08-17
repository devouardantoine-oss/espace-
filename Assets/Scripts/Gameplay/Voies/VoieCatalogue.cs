using System.Collections.Generic;

namespace Espace.Gameplay.Voies
{
    /// <summary>
    /// Les quatre voies, telles que le codex les livre (Phase 24, etape 7).
    /// <para>
    /// <b>Contenu, pas comportement</b> — meme separation que <c>CodexLibrary</c> et
    /// <c>DecisionCatalogue</c>. Les textes vivent ici, la mecanique dans
    /// <see cref="VoieService"/>.
    /// </para>
    /// <para>
    /// <b>Aucune voie n'est gratuite</b>, exactement comme aucune option de decision ne l'est
    /// (Phase 24, etape 5). Un test verifie que chacune annonce un cout, parce qu'une reponse
    /// sans contrepartie serait <i>la</i> reponse, et la courbe cesserait d'etre un probleme.
    /// </para>
    /// <para>
    /// <b>Deconcentration et Compression ne doivent surtout pas se ressembler.</b> Elles rendent
    /// toutes deux un monde a personne, et il aurait ete facile de les coder de la meme facon —
    /// deux voies identiques n'en font qu'une. La difference est reelle et tient en une phrase :
    /// la Deconcentration laisse derriere elle <b>un monde developpe que n'importe qui peut
    /// coloniser</b>, la Compression n'y laisse <b>rien, mais tout le monde vous en veut</b>.
    /// </para>
    /// </summary>
    public static class VoieCatalogue
    {
        /// <summary>
        /// Fragment du codex qui ouvre les voies. C'est l'aveu : « j'ai essaye les quatre ».
        /// </summary>
        public const int UnlockingFragment = 4;

        private static readonly VoieDefinition[] Definitions =
        {
            new VoieDefinition(
                Voie.Deconcentration,
                "Deconcentration",
                "Le monde passe en autonomie : il cesse de compter dans le cout administratif et de vous verser quoi que ce soit.",
                "Il ne vous appartient plus, et il reste developpe : n'importe qui peut venir s'y installer.",
                "Refusee. Perdre le registre, c'etait perdre l'Empire par definition.",
                needsASystem: true,
                isPlayable: true),

            new VoieDefinition(
                Voie.Compression,
                "Compression",
                "Le monde est evacue puis abandonne, pour repasser sous le plafond.",
                "Population et installations perdues, et tous les empires vous en tiennent rigueur.",
                "Le Redecoupage de −220. Ca a marche deux siecles.",
                needsASystem: true,
                isPlayable: true),

            new VoieDefinition(
                Voie.Coercition,
                "Coercition",
                "La garnison remplace l'administration : le cout bascule de l'Influence vers la tresorerie.",
                "On echange une ruine contre une autre — c'est plus cher, et la stabilite chute partout.",
                "Ce que le Bastion a fait tout seul, sans qu'on le lui demande, pendant huit cents ans.",
                needsASystem: false,
                isPlayable: true),

            new VoieDefinition(
                Voie.Transformation,
                "Transformation",
                "Retracer le reseau d'hyperroutes pour rendre l'unification impossible.",
                "Irreversible, et la carte change pour tout le monde — y compris pour vous.",
                "La Dispersion. Personne ne sait si c'est ce qui l'a sauve de quelque chose, ou ce qui l'a acheve.",
                needsASystem: false,
                isPlayable: false)
        };

        /// <summary>Les quatre voies, dans l'ordre ou l'Empire les a tentees.</summary>
        public static IReadOnlyList<VoieDefinition> All
        {
            get { return Definitions; }
        }

        /// <summary>La definition d'une voie.</summary>
        public static VoieDefinition Of(Voie voie)
        {
            return Definitions[(int)voie];
        }

        /// <summary>
        /// Pourquoi la Transformation n'est pas encore jouable, dit au joueur plutot que tu.
        /// <para>
        /// <b>Ce texte est une dette assumee, pas une excuse.</b> Retracer le reseau demande de
        /// rendre les liens de la carte mutables — ils sont aujourd'hui un tableau construit une
        /// fois — puis de revalider l'itineraire de <i>chaque flotte en vol</i>, de reconstruire
        /// la partition de territoires et le rendu des liens, le tout en garantissant que la
        /// galaxie reste connexe. Une route fermee au mauvais endroit la coupe en deux et rend la
        /// partie injouable.
        /// </para>
        /// </summary>
        public const string TransformationPending =
            "Les plans existent. Le reseau, lui, ne se laisse pas encore retracer.";
    }
}
