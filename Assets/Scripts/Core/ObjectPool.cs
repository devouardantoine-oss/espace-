using System;
using System.Collections.Generic;

namespace Espace.Core
{
    /// <summary>
    /// Pool d'objets generique, en C# pur.
    /// <para>
    /// <b>Pourquoi des le depart ?</b> Les phases suivantes vont creer et detruire beaucoup
    /// d'instances a la volee (projectiles, unites, barres de vie, lignes de trajectoire).
    /// Sur mobile, <c>Instantiate</c>/<c>Destroy</c> en boucle est la premiere cause de pics
    /// de GC et de saccades. Poser le pool maintenant evite d'en reecrire trois variantes
    /// plus tard — le code duplique est explicitement interdit par le cahier des charges.
    /// </para>
    /// <para>
    /// <b>Pourquoi <c>where T : class</c> et pas <c>where T : Component</c> ?</b> Pour rester
    /// utilisable aussi bien avec des GameObjects (via une fabrique
    /// <c>() =&gt; Object.Instantiate(prefab)</c>) qu'avec des objets purement logiques, et
    /// surtout pour rester testable sans lancer le Play Mode.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Type mis en pool.</typeparam>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Stack<T> _available;
        private readonly int _maxSize;

        /// <summary>Nombre d'instances actuellement disponibles dans le pool.</summary>
        public int AvailableCount => _available.Count;

        /// <summary>Nombre total d'instances creees par la fabrique depuis la construction.</summary>
        public int TotalCreated { get; private set; }

        /// <summary>Nombre d'instances actuellement sorties du pool (en cours d'utilisation).</summary>
        public int ActiveCount => TotalCreated - _available.Count;

        /// <summary>
        /// Cree un pool.
        /// </summary>
        /// <param name="factory">Fabrique appelee quand le pool est vide. Obligatoire.</param>
        /// <param name="onGet">Rappel a la sortie du pool (ex : activer le GameObject, reinitialiser la vie).</param>
        /// <param name="onRelease">Rappel au retour dans le pool (ex : desactiver le GameObject).</param>
        /// <param name="prewarmCount">
        /// Instances creees immediatement. A regler sur le pic attendu : le cout de creation
        /// est ainsi paye au chargement plutot qu'en pleine bataille.
        /// </param>
        /// <param name="maxSize">
        /// Plafond d'instances conservees. Au-dela, les objets rendus sont abandonnes
        /// (et passes a <paramref name="onRelease"/>) pour ne pas retenir de memoire inutile.
        /// </param>
        public ObjectPool(
            Func<T> factory,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            int prewarmCount = 0,
            int maxSize = 256)
        {
            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), "La taille maximale doit etre strictement positive.");
            }

            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _onGet = onGet;
            _onRelease = onRelease;
            _maxSize = maxSize;
            _available = new Stack<T>(Math.Max(prewarmCount, 4));

            Prewarm(prewarmCount);
        }

        /// <summary>
        /// Cree a l'avance <paramref name="count"/> instances (dans la limite de <c>maxSize</c>).
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count && _available.Count < _maxSize; i++)
            {
                T instance = CreateInstance();
                _onRelease?.Invoke(instance);
                _available.Push(instance);
            }
        }

        /// <summary>
        /// Sort une instance du pool, ou en cree une si le pool est vide.
        /// </summary>
        public T Get()
        {
            T instance = _available.Count > 0 ? _available.Pop() : CreateInstance();
            _onGet?.Invoke(instance);
            return instance;
        }

        /// <summary>
        /// Rend une instance au pool.
        /// </summary>
        /// <returns>
        /// <c>true</c> si l'instance a ete conservee, <c>false</c> si le pool etait plein
        /// (l'appelant reste alors responsable de sa destruction definitive).
        /// </returns>
        public bool Release(T instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            _onRelease?.Invoke(instance);

            if (_available.Count >= _maxSize)
            {
                // Le pool est plein : on ne garde pas l'instance. TotalCreated est decremente
                // pour que ActiveCount reste coherent.
                TotalCreated--;
                return false;
            }

            _available.Push(instance);
            return true;
        }

        /// <summary>
        /// Vide le pool. <paramref name="disposeAction"/> permet de detruire reellement
        /// les instances conservees (ex : <c>Object.Destroy</c>).
        /// </summary>
        public void Clear(Action<T> disposeAction = null)
        {
            if (disposeAction != null)
            {
                foreach (T instance in _available)
                {
                    disposeAction(instance);
                }
            }

            TotalCreated -= _available.Count;
            _available.Clear();
        }

        private T CreateInstance()
        {
            T instance = _factory.Invoke();
            if (instance == null)
            {
                throw new InvalidOperationException($"ObjectPool<{typeof(T).Name}>: la fabrique a retourne null.");
            }

            TotalCreated++;
            return instance;
        }
    }
}
