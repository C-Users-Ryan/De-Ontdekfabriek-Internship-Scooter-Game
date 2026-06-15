using System;
using System.Collections.Generic;
using UnityEngine;

namespace KenyaScooter.Core
{
    /// <summary>
    /// Minimal component pool (Req §2: no Instantiate/Destroy during play). All
    /// instances are created up front in the constructor; Get() returns an INACTIVE
    /// instance so the caller can position and configure it before activating.
    /// If the pool runs dry it expands — that is a tuning problem, so it is logged.
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform parent;
        private readonly Stack<T> available;
        private readonly Action<T> onCreated;

        /// <summary>Every instance ever created by this pool, for rewind registration and reconciliation.</summary>
        public readonly List<T> AllInstances = new List<T>();

        public ObjectPool(T prefab, Transform parent, int initialSize, Action<T> onCreated = null)
        {
            this.prefab = prefab;
            this.parent = parent;
            this.onCreated = onCreated;
            available = new Stack<T>(initialSize);
            for (int i = 0; i < initialSize; i++)
                available.Push(CreateInstance());
        }

        /// <summary>Returns an inactive instance. Configure it, then SetActive(true).</summary>
        public T Get()
        {
            if (available.Count > 0)
                return available.Pop();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ObjectPool] Pool for '{prefab.name}' exhausted — expanding. Increase the configured pool size.");
#endif
            return CreateInstance();
        }

        public void Release(T instance)
        {
            instance.gameObject.SetActive(false);
            available.Push(instance);
        }

        /// <summary>
        /// Rebuilds the available stack from actual GameObject states. Needed after a
        /// rewind, which can reactivate released instances (and vice versa) directly
        /// via IRewindable, bypassing Get/Release bookkeeping (M17).
        /// </summary>
        public void ReconcileAvailability()
        {
            available.Clear();
            for (int i = 0; i < AllInstances.Count; i++)
                if (!AllInstances[i].gameObject.activeInHierarchy)
                    available.Push(AllInstances[i]);
        }

        private T CreateInstance()
        {
            T instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.gameObject.SetActive(false);
            AllInstances.Add(instance);
            onCreated?.Invoke(instance);
            return instance;
        }
    }
}
