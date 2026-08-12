using System.Collections.Generic;
using Espace.Core;
using Espace.Gameplay.Galaxy;

namespace Espace.Gameplay.Chronicle
{
    /// <summary>Une ligne du journal : ce qui s'est passe, quand, et ou aller voir.</summary>
    public readonly struct GameNotice
    {
        /// <summary>Nature de l'avis, qui determine son niveau via <see cref="NoticeRules"/>.</summary>
        public readonly NoticeKind Kind;

        /// <summary>Niveau d'attention, deduit une fois a la construction.</summary>
        public readonly NoticeTier Tier;

        /// <summary>Texte court, une ligne. L'interface fait 700 unites de large : jamais de paragraphe.</summary>
        public readonly string Text;

        /// <summary>Date de jeu a laquelle l'avis a ete emis.</summary>
        public readonly GameDate Date;

        /// <summary>Systeme concerne, si l'avis en designe un. Sert de destination a l'appui.</summary>
        public readonly StarSystemId? Subject;

        public GameNotice(NoticeKind kind, string text, GameDate date, StarSystemId? subject = null)
        {
            Kind = kind;
            Text = text;
            Date = date;
            Subject = subject;
            Tier = NoticeRules.TierOf(kind);
        }
    }

    /// <summary>
    /// Journal des avis : les derniers evenements de la partie, du plus recent au plus ancien
    /// (Phase 24, etape 1).
    /// <para>
    /// <b>Ce que ca change.</b> Jusqu'ici une partie ne laissait aucune trace : le joueur ne
    /// pouvait ni verifier ce qu'il avait manque, ni raconter ce qui s'etait passe. C'est la
    /// moitie du travail narratif, pour un dixieme du cout — il n'y a aucun contenu a ecrire,
    /// seulement a montrer ce que le jeu publie deja.
    /// </para>
    /// <para>
    /// <b>Structure pure</b>, sans <c>MonoBehaviour</c> ni <c>ServiceLocator</c> : elle se
    /// verifie sans scene, comme <see cref="SystemGlyph"/> ou <c>EmpireStateBand</c>.
    /// </para>
    /// <para>
    /// <b>Non sauvegarde, volontairement.</b> C'est un fil d'actualite, pas une archive : la
    /// meme decision que pour les rapports d'operation de la Phase 20, qu'il remplace. Le jour
    /// ou les fragments du journal narratif arriveront (etape 3), <i>eux</i> devront persister —
    /// ce sont deux objets differents.
    /// </para>
    /// </summary>
    public sealed class NoticeLog
    {
        /// <summary>
        /// Nombre d'avis conserves. Au-dela, les plus anciens sont oublies.
        /// <para>
        /// Soixante couvre plusieurs annees de jeu a rythme normal, et tient en memoire sans
        /// qu'on ait a y penser. Le journal n'est pas une archive : remonter au-dela n'aiderait
        /// personne.
        /// </para>
        /// </summary>
        public const int Capacity = 60;

        private readonly List<GameNotice> _notices = new List<GameNotice>(Capacity);

        /// <summary>Avis du plus recent au plus ancien.</summary>
        public IReadOnlyList<GameNotice> Notices => _notices;

        /// <summary>
        /// Avis d'attention <see cref="NoticeTier.Important"/> ou plus, non encore consultes.
        /// C'est le nombre affiche par le compteur du bandeau.
        /// </summary>
        public int UnreadCount { get; private set; }

        /// <summary>Vrai si au moins un avis non lu est <see cref="NoticeTier.Critical"/>.</summary>
        public bool HasUnreadCritical { get; private set; }

        /// <summary>Ajoute un avis en tete et oublie le plus ancien si la capacite est atteinte.</summary>
        public void Add(GameNotice notice)
        {
            _notices.Insert(0, notice);

            if (_notices.Count > Capacity)
            {
                _notices.RemoveAt(_notices.Count - 1);
            }

            // Les avis d'information ne comptent pas : le compteur ne doit signaler que ce qui
            // merite un detour. Sinon il est en permanence a une valeur elevee et cesse d'etre
            // une information.
            if (notice.Tier == NoticeTier.Information)
            {
                return;
            }

            UnreadCount++;

            if (notice.Tier == NoticeTier.Critical)
            {
                HasUnreadCritical = true;
            }
        }

        /// <summary>Remet le compteur a zero. Appele quand le joueur ouvre le journal.</summary>
        public void MarkAllRead()
        {
            UnreadCount = 0;
            HasUnreadCritical = false;
        }

        /// <summary>Vide le journal — au demarrage d'une nouvelle partie.</summary>
        public void Clear()
        {
            _notices.Clear();
            MarkAllRead();
        }

        /// <summary>
        /// Les <paramref name="count"/> avis les plus recents d'un niveau au moins egal a
        /// <paramref name="minimumTier"/>. Sert aux vues etroites, qui ne peuvent pas tout
        /// afficher.
        /// </summary>
        public List<GameNotice> MostRecent(int count, NoticeTier minimumTier = NoticeTier.Information)
        {
            var result = new List<GameNotice>(count);

            foreach (GameNotice notice in _notices)
            {
                if (result.Count >= count)
                {
                    break;
                }

                if (notice.Tier >= minimumTier)
                {
                    result.Add(notice);
                }
            }

            return result;
        }
    }
}
