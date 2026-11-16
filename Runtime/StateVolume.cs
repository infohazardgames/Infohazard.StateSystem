using System.Collections.Generic;
using Infohazard.Core;
using UnityEngine;

namespace Infohazard.StateSystem {
    /// <summary>
    /// Turns a state on for each <see cref="StateManager"/> inside a <see cref="TriggerVolume"/>.
    /// </summary>
    /// <remarks>
    /// An occupant's manager is found on the collider's object or its parents, so several colliders can belong to
    /// one manager. The state stays on while any of them is inside any volume with the same state name.
    /// </remarks>
    public class StateVolume : MonoBehaviour {
        [SerializeField] private string _stateName;
        [SerializeField] private TriggerVolume _triggerVolume;

        /// <summary>
        /// How many occupant objects of each manager are inside this volume.
        /// </summary>
        private readonly Dictionary<StateManager, int> _occupantActiveCount = new();

        /// <summary>
        /// The manager each occupant object counted toward when it entered.
        /// </summary>
        private readonly Dictionary<GameObject, StateManager> _occupantManagers = new();

        /// <summary>
        /// How many volumes currently hold each (manager, state) on, across all volumes.
        /// </summary>
        private static readonly Dictionary<(StateManager manager, string state), int> SharedCounts = new();

        /// <summary>
        /// Clear the counts shared between volumes. Called automatically when play mode starts; typically
        /// shouldn't be called otherwise.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStaticState() {
            SharedCounts.Clear();
        }

        private void Reset() {
            _triggerVolume = GetComponent<TriggerVolume>();
        }

        private void OnEnable() {
            _triggerVolume.TriggerEntered += TriggerVolume_TriggerEntered;
            _triggerVolume.TriggerExited += TriggerVolume_TriggerExited;

            foreach (GameObject occupant in _triggerVolume.Occupants) {
                TriggerVolume_TriggerEntered(occupant);
            }
        }

        private void OnDisable() {
            _triggerVolume.TriggerEntered -= TriggerVolume_TriggerEntered;
            _triggerVolume.TriggerExited -= TriggerVolume_TriggerExited;

            foreach (GameObject occupant in _triggerVolume.Occupants) {
                TriggerVolume_TriggerExited(occupant);
            }
        }

        private void TriggerVolume_TriggerEntered(GameObject other) {
            if (!other || _occupantManagers.ContainsKey(other)) return;

            StateManager manager = other.GetComponentInParent<StateManager>(true);
            if (!manager) return;

            _occupantManagers[other] = manager;
            _occupantActiveCount.TryGetValue(manager, out int count);
            _occupantActiveCount[manager] = count + 1;
            if (count > 0) return;

            (StateManager, string) key = (manager, _stateName);
            SharedCounts.TryGetValue(key, out int shared);
            SharedCounts[key] = shared + 1;
            if (shared == 0) manager.SetStateActive(_stateName, true);
        }

        private void TriggerVolume_TriggerExited(GameObject other) {
            // Use the manager recorded on entry: the occupant (and its manager) may have been destroyed or moved
            // to another parent since then.
            if (ReferenceEquals(other, null) || !_occupantManagers.Remove(other, out StateManager manager)) return;
            if (!_occupantActiveCount.TryGetValue(manager, out int count)) return;

            if (count > 1) {
                _occupantActiveCount[manager] = count - 1;
                return;
            }

            _occupantActiveCount.Remove(manager);
            ReleaseShared(manager);
        }

        private void ReleaseShared(StateManager manager) {
            (StateManager, string) key = (manager, _stateName);
            if (!SharedCounts.TryGetValue(key, out int shared)) return;

            if (shared > 1) {
                SharedCounts[key] = shared - 1;
                return;
            }

            SharedCounts.Remove(key);
            if (manager) manager.SetStateActive(_stateName, false);
        }
    }
}
