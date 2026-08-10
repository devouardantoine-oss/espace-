using UnityEngine;

namespace Espace.Gameplay.Espionage
{
    /// <summary>Issue d'une operation clandestine.</summary>
    public enum EspionageOutcome
    {
        /// <summary>Reussite sans que personne ne sache d'ou vient le coup.</summary>
        Discreet,

        /// <summary>Reussite, mais l'operation est attribuee : l'effet a lieu et la relation en paie le prix.</summary>
        Attributed,

        /// <summary>Echec sans consequence diplomatique : l'agent n'a rien obtenu et n'a pas ete identifie.</summary>
        Failed,

        /// <summary>Echec et identification : le pire des cas.</summary>
        Exposed
    }

    /// <summary>
    /// Resolution d'une operation d'espionnage (Phase 22, P6).
    /// <para>
    /// <b>Le defaut corrige.</b> L'ancienne regle tenait en une comparaison :
    /// <c>puissanceAttaque &gt; puissanceDefense</c>, ou la defense valait
    /// <c>10 x stabilite</c>. Comme la stabilite est toujours inferieure a 1, <b>l'attaquant
    /// gagnait toujours</b> a recherche egale. Il n'y avait ni probabilite, ni risque, ni
    /// detection distincte de l'echec — seulement un cout en credits.
    /// </para>
    /// <para>
    /// <b>Ce qui remplace : une decision continue.</b> Le joueur choisit <em>combien
    /// d'influence engager</em>. Peu d'influence, l'operation est bon marche mais hasardeuse ;
    /// beaucoup, elle est presque sure mais vide les caisses dont l'administration a besoin. Il
    /// n'y a plus de bouton « espionner », il y a un pari a dimensionner.
    /// </para>
    /// <para>
    /// <b>Quatre issues, pas deux.</b> La reussite et la discretion sont deux questions
    /// separees : on peut reussir en se faisant voir, ou echouer sans laisser de trace. C'est
    /// cette separation qui rend une operation ratee supportable et une operation reussie
    /// parfois regrettable.
    /// </para>
    /// <para>
    /// <b>Fonction pure, generateur injecte</b> : le hasard entre par un parametre plutot que
    /// d'etre tire a l'interieur. C'est ce qui permet de verifier la distribution des issues
    /// sans mode Play, et de rejouer une resolution a l'identique.
    /// </para>
    /// </summary>
    public static class EspionageResolution
    {
        /// <summary>Chance de reussite quand les deux camps sont a egalite parfaite.</summary>
        public const float EvenOddsChance = 0.5f;

        /// <summary>Chance de reussite plafond : une operation n'est jamais acquise, quel que soit l'investissement.</summary>
        public const float MaximumChance = 0.92f;

        /// <summary>Chance de reussite plancher : une operation desesperee garde une part de chance.</summary>
        public const float MinimumChance = 0.05f;

        /// <summary>Risque d'attribution d'une operation reussie, quand l'attaquant domine largement.</summary>
        public const float MinimumExposure = 0.05f;

        /// <summary>Risque d'attribution d'une operation ratee, quand le defenseur domine largement.</summary>
        public const float MaximumExposure = 0.85f;

        /// <summary>
        /// Puissance d'une operation : l'influence engagee, amplifiee par le reseau et la
        /// recherche.
        /// <para>
        /// La racine carree de l'influence est volontaire — <b>rendements decroissants</b> :
        /// doubler la mise n'achete pas deux fois la reussite. Sans cela, un empire riche
        /// s'offrirait la certitude, ce qui ramenerait au systeme binaire d'avant.
        /// </para>
        /// </summary>
        public static float AttackPower(float influenceCommitted, float networkStrength, float researchBonus)
        {
            float influence = Mathf.Max(0f, influenceCommitted);
            return Mathf.Sqrt(influence) * Mathf.Max(0.1f, networkStrength) * (1f + Mathf.Max(0f, researchBonus));
        }

        /// <summary>
        /// Puissance du contre-espionnage : la stabilite du systeme vise, la recherche adverse,
        /// et la vigilance acquise.
        /// <para>
        /// <b>La vigilance monte a chaque tentative</b>, reussie ou non. Frapper deux fois au
        /// meme endroit devient nettement plus dur — c'est ce qui empeche de repeter une
        /// operation rentable jusqu'a l'absurde, sans qu'aucun delai arbitraire soit impose.
        /// </para>
        /// </summary>
        public static float DefencePower(float stability, float researchBonus, float vigilance)
        {
            return Mathf.Max(0.1f, stability) * (1f + Mathf.Max(0f, researchBonus)) * (1f + Mathf.Max(0f, vigilance));
        }

        /// <summary>
        /// Chance de reussite, entre <see cref="MinimumChance"/> et <see cref="MaximumChance"/>.
        /// <para>
        /// Rapport de forces plutot que difference : deux camps a egalite tombent a 50 % quelle
        /// que soit leur puissance absolue, ce qui garde le modele lisible aussi bien entre
        /// petites puissances qu'entre grandes.
        /// </para>
        /// </summary>
        public static float SuccessChance(float attackPower, float defencePower)
        {
            float attack = Mathf.Max(0f, attackPower);
            float defence = Mathf.Max(0f, defencePower);
            float total = attack + defence;

            if (total <= 0f)
            {
                return EvenOddsChance;
            }

            return Mathf.Clamp(attack / total, MinimumChance, MaximumChance);
        }

        /// <summary>
        /// Risque que l'operation soit attribuee a son commanditaire.
        /// <para>
        /// Une operation ratee expose bien plus qu'une operation reussie : c'est en echouant
        /// qu'on laisse des traces. Le rapport de forces module les deux.
        /// </para>
        /// </summary>
        public static float ExposureChance(float attackPower, float defencePower, bool succeeded)
        {
            float dominance = SuccessChance(attackPower, defencePower);
            float ceiling = succeeded ? MaximumExposure * 0.5f : MaximumExposure;

            return Mathf.Lerp(ceiling, MinimumExposure, dominance);
        }

        /// <summary>
        /// Resout une operation.
        /// </summary>
        /// <param name="attackPower">Voir <see cref="AttackPower"/>.</param>
        /// <param name="defencePower">Voir <see cref="DefencePower"/>.</param>
        /// <param name="successRoll">Tirage uniforme dans [0,1[ pour la reussite.</param>
        /// <param name="exposureRoll">Tirage uniforme dans [0,1[ pour l'attribution. Un second tirage, car reussir et se faire voir sont deux questions distinctes.</param>
        public static EspionageOutcome Resolve(float attackPower, float defencePower, float successRoll, float exposureRoll)
        {
            bool succeeded = successRoll < SuccessChance(attackPower, defencePower);
            bool exposed = exposureRoll < ExposureChance(attackPower, defencePower, succeeded);

            if (succeeded)
            {
                return exposed ? EspionageOutcome.Attributed : EspionageOutcome.Discreet;
            }

            return exposed ? EspionageOutcome.Exposed : EspionageOutcome.Failed;
        }

        /// <summary>Vrai si l'issue produit l'effet recherche, quelles qu'en soient les suites diplomatiques.</summary>
        public static bool Succeeded(EspionageOutcome outcome)
        {
            return outcome == EspionageOutcome.Discreet || outcome == EspionageOutcome.Attributed;
        }

        /// <summary>Vrai si la victime sait qui l'a frappee.</summary>
        public static bool WasAttributed(EspionageOutcome outcome)
        {
            return outcome == EspionageOutcome.Attributed || outcome == EspionageOutcome.Exposed;
        }
    }
}
