using System;
using System.Collections.Generic;

namespace Espace.Gameplay.Empires
{
    /// <summary>
    /// Registre passif des empires d'une partie : mêmes rôle et style que
    /// <see cref="Espace.Gameplay.Galaxy.GalaxyMap"/> pour les systèmes — accès en O(1) par
    /// identifiant, aucune logique de simulation.
    /// </summary>
    public sealed class EmpireRegistry
    {
        private readonly Empire[] _empires;
        private readonly Dictionary<int, Empire> _byId;

        /// <summary>Tous les empires de la partie, joueur inclus.</summary>
        public IReadOnlyList<Empire> Empires => _empires;

        /// <summary>L'empire du joueur.</summary>
        public Empire PlayerEmpire { get; }

        /// <exception cref="ArgumentException">Si <paramref name="empires"/> est vide, contient un identifiant duplique, ou aucun/plusieurs empires joueur.</exception>
        public EmpireRegistry(IReadOnlyList<Empire> empires)
        {
            if (empires == null || empires.Count == 0)
            {
                throw new ArgumentException("Au moins un empire est requis.", nameof(empires));
            }

            _empires = new Empire[empires.Count];
            _byId = new Dictionary<int, Empire>(empires.Count);
            Empire playerEmpire = null;

            for (int i = 0; i < empires.Count; i++)
            {
                Empire empire = empires[i];
                if (empire == null)
                {
                    throw new ArgumentException($"L'empire a l'index {i} est nul.", nameof(empires));
                }

                if (!_byId.TryAdd(empire.Id, empire))
                {
                    throw new ArgumentException($"Identifiant d'empire duplique : {empire.Id}.", nameof(empires));
                }

                _empires[i] = empire;

                if (empire.IsPlayerControlled)
                {
                    if (playerEmpire != null)
                    {
                        throw new ArgumentException("Plusieurs empires sont marques IsPlayerControlled.", nameof(empires));
                    }

                    playerEmpire = empire;
                }
            }

            if (playerEmpire == null)
            {
                throw new ArgumentException("Aucun empire n'est marque IsPlayerControlled.", nameof(empires));
            }

            PlayerEmpire = playerEmpire;
        }

        /// <exception cref="KeyNotFoundException">Si aucun empire ne porte cet identifiant.</exception>
        public Empire GetEmpire(int id)
        {
            if (_byId.TryGetValue(id, out Empire empire))
            {
                return empire;
            }

            throw new KeyNotFoundException($"Aucun empire avec l'identifiant {id}.");
        }

        /// <summary>Variante non levante de <see cref="GetEmpire"/>.</summary>
        public bool TryGetEmpire(int id, out Empire empire)
        {
            return _byId.TryGetValue(id, out empire);
        }
    }
}
