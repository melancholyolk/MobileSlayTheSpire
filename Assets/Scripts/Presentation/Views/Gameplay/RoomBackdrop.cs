using System;
using System.Linq;
using System.Threading.Tasks;
using Spire;
using SpireResources;
using UnityEngine;

namespace SpirePresentation
{
    [Serializable]
    public sealed class RoomBackdropBinding
    {
        public RoomKind room;
        public int prefabId;
    }

    public sealed class RoomBackdrop : MonoBehaviour
    {
        [SerializeField]
        RectTransform container;
        [SerializeField]
        RoomBackdropBinding[] rooms;
        ResourceManager resources;
        ResourceScope scope;
        Run observed;
        int room = -1, generation;
        public Task Ready { get; private set; } = Task.CompletedTask;
        public int CurrentPrefabId { get; private set; }

        public void Configure(ResourceManager manager)
        {
            resources = manager;
        }

        public void Observe(Run run)
        {
            if (run == null || run.Current < 0 || run.Phase == Phase.Map)
            {
                Clear();
                return;
            }

            if (observed == run && room == run.Current)
                return;
            Clear();
            observed = run;
            room = run.Current;
            var binding = rooms.FirstOrDefault(b => b.room == run.Map[run.Current].Kind);
            if (binding == null)
            {
                Debug.LogWarning("Missing room background: " + run.Map[run.Current].Kind);
                return;
            }

            scope = resources.CreateScope();
            CurrentPrefabId = binding.prefabId;
            Ready = Load(scope, binding.prefabId, generation);
        }

        async Task Load(ResourceScope owner, int prefabId, int version)
        {
            try
            {
                var go = await owner.InstantiateAsync(prefabId, container, false);
                if (version != generation || owner.IsDisposed || this == null)
                    return;
                // The prefab authors its own stretch, sorting, animation and child layout.
                go.SetActive(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Clear();
            }
        }

        public void Clear()
        {
            generation++;
            scope?.Dispose();
            scope = null;
            observed = null;
            room = -1;
            CurrentPrefabId = 0;
            Ready = Task.CompletedTask;
        }

        void OnDestroy()
        {
            Clear();
        }
    }
}
