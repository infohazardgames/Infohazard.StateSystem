// The MIT License (MIT)
// 
// Copyright (c) 2026-present Val Miller
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

using Object = UnityEngine.Object;

namespace Infohazard.StateSystem {
    [Serializable]
    public struct State {
        [FormerlySerializedAs("name")] [SerializeField]
        private string _name;

        [FormerlySerializedAs("values")] [SerializeField]
        private ComponentOverride _values;

        [FormerlySerializedAs("blockedBy")] [SerializeField]
        private int _blockedBy;

        [FormerlySerializedAs("isActive")] [SerializeField]
        private bool _isActive;

        public bool IsBlocked { get; set; }
        public int[] FieldIDs { get; set; }
        public string Name => _name;
        public ComponentOverride Values => _values;
        public int BlockedBy => _blockedBy;
        public bool IsActive {
            get => _isActive;
            set => _isActive = value;
        }
    }

    /// <summary>
    /// A list of named states that override values on an owner object. When several active states override the
    /// same field, the state with the lower index wins. A state is blocked (active, but not applied) while any
    /// active, unblocked state in its <see cref="State.BlockedBy"/> mask is active.
    /// </summary>
    /// <remarks>
    /// When a field is first overridden, its current value is captured and restored once no state overrides it.
    /// The captured values aren't serialized, but the overridden field values and the active flags are. So cloning
    /// an object whose states are already applied (<c>Object.Instantiate</c> on a live, initialized object rather
    /// than a prefab) gives a clone that treats the overridden values as its defaults: turning those states off on
    /// the clone leaves the overridden values in place. Instantiate from prefabs instead.
    /// </remarks>
    [Serializable]
    public class StateList {
        [FormerlySerializedAs("states")] [SerializeField]
        private State[] _states;
        
        private Object _owner;
        private object[] _fieldDefaultValues;
        public StateList() { }
        public StateList(State[] states) {
            _states = states;
        }

        /// <summary>
        /// The most states whose blocking works; <see cref="State.BlockedBy"/> is a 32-bit mask of state indices.
        /// </summary>
        public const int MaxBlockingStates = 32;

        public void Initialize(Object owner) {
            _owner = owner;
            _states ??= Array.Empty<State>();

            if (_states.Length > MaxBlockingStates) {
                Debug.LogError($"{OwnerName}: has {_states.Length} states, but only the first " +
                               $"{MaxBlockingStates} can block other states.", owner);
            }

            if (!owner && _states.Any(s => s.Values)) {
                Debug.LogError("A state list has no object to apply its states to, so its values are skipped.");
            }

            Dictionary<string, int> fieldIDs = new Dictionary<string, int>();
            List<bool> statesActive = _states.Select(s => s.IsActive).ToList();
            for (int i = 0; i < _states.Length; i++) {
                ComponentOverride values = CanApply(_states[i]) ? _states[i].Values : null;

                _states[i].FieldIDs = new int[values ? values.Overrides.Length : 0];
                _states[i].IsActive = false;
                _states[i].IsBlocked = false;

                for (int j = 0; j < _states[i].FieldIDs.Length; j++) {
                    SerializedValueOverride value = values.Overrides[j];
                    if (!value.Valid) {
                        _states[i].FieldIDs[j] = -1;
                        continue;
                    }

                    // Key by the current member path, so a renamed path that hasn't been migrated yet and the
                    // current path control the same field.
                    string key = value.RenamedPath ?? value.Path;
                    if (!fieldIDs.TryGetValue(key, out int fieldID)) {
                        fieldID = fieldIDs.Count;
                        fieldIDs[key] = fieldID;
                    }

                    _states[i].FieldIDs[j] = fieldID;
                }
            }

            _fieldDefaultValues = new object[fieldIDs.Count];

            for (int i = 0; i < _states.Length; i++) {
                if (statesActive[i]) {
                    SetStateActive(i, true);
                }
            }
        }

        private string OwnerName => _owner ? _owner.name : "State list";

        /// <summary>
        /// Whether a state's values can be applied to the owner. Logs why not, and warns about overrides that don't
        /// resolve (those are skipped individually).
        /// </summary>
        private bool CanApply(State state) {
            ComponentOverride values = state.Values;
            if (!values || !_owner) return false;

            if (values.Type == null) {
                Debug.LogError($"{OwnerName}: state '{state.Name}' uses override '{values.name}', whose target type " +
                               $"'{values.TypeName}' was not found, so its values are skipped.", _owner);
                return false;
            }

            if (!values.Type.IsInstanceOfType(_owner)) {
                Debug.LogError($"{OwnerName}: state '{state.Name}' uses override '{values.name}', which is for " +
                               $"{values.Type.Name}, not {_owner.GetType().Name}, so its values are skipped.", _owner);
                return false;
            }

            values.WarnIfInvalidOverrides();
            return true;
        }

        private int GetStateIndex(string name) {
            if (_states == null) return -1;
            for (int i = 0; i < _states.Length; i++) {
                if (_states[i].Name == name) {
                    return i;
                }
            }
            return -1;
        }

        public bool HasState(string state) {
            return GetStateIndex(state) >= 0;
        }

        private bool IsStateBlocked(int stateIndex) {
            if (_states[stateIndex].BlockedBy == 0) return false;

            for (int i = 0; i < _states.Length; i++) {
                if (IsStateBlockedBy(stateIndex, i)) {
                    return true;
                }
            }

            return false;
        }

        private bool IsStateBlockedBy(int state, int blockingState) {
            return state != blockingState &&
                blockingState < MaxBlockingStates &&
                _states[blockingState].IsActive && 
                !_states[blockingState].IsBlocked && 
                (_states[state].BlockedBy & (1 << blockingState)) != 0;
        }

        public bool IsStateActive(string state) {
            int index = GetStateIndex(state);
            if (index < 0) return false;
            return IsStateActive(index);
        }

        public bool IsStateActive(int stateIndex) {
            return _states[stateIndex].IsActive;
        }

        public void SetStateActive(string state, bool active) {
            int stateIndex = GetStateIndex(state);
            if (stateIndex >= 0) {
                SetStateActive(stateIndex, active);
            }
        }

        public void SetStateActive(int stateIndex, bool active) {
            if (_states[stateIndex].IsActive == active) return;

            UpdateActiveAndBlocked(stateIndex, active, IsStateBlocked(stateIndex));
        }

        private void UpdateActiveAndBlocked(int stateIndex, bool active, bool isBlocked) {
            bool fieldStatusChanging = (active && !isBlocked) != (_states[stateIndex].IsActive && !_states[stateIndex].IsBlocked);

            _states[stateIndex].IsBlocked = isBlocked;
            _states[stateIndex].IsActive = active;

            if (fieldStatusChanging) {
                bool isActivating = active && !isBlocked;
                for (int i = 0; i < _states[stateIndex].FieldIDs.Length; i++) {
                    if (_states[stateIndex].FieldIDs[i] < 0) continue; // Override that doesn't resolve.

                    if (isActivating) {
                        ActivateField(stateIndex, i);
                    } else {
                        DeactivateField(stateIndex, i);
                    }
                }
            }

            for (int i = 0; i < _states.Length; i++) {
                if (i != stateIndex && 
                    _states[i].IsActive && 
                    IsStateBlockedBy(i, stateIndex) != _states[i].IsBlocked &&
                    IsStateBlocked(i) != _states[i].IsBlocked) {
                    UpdateActiveAndBlocked(i, _states[i].IsActive, !_states[i].IsBlocked);
                }
            }
        }

        private int GetFirstStateControllingField(int fieldID, int excludeState, out SerializedValueOverride value) {
            for (int i = 0; i < _states.Length; i++) {
                if (i == excludeState || !_states[i].IsActive || _states[i].IsBlocked) continue;

                int index = Array.IndexOf(_states[i].FieldIDs, fieldID);
                if (index >= 0) {
                    value = _states[i].Values.Overrides[index];
                    return i;
                }
            }

            value = null;
            return -1;
        }

        private void ActivateField(int stateIndex, int localFieldIndex) {
            int fieldID = _states[stateIndex].FieldIDs[localFieldIndex];
            int previous = GetFirstStateControllingField(fieldID, stateIndex, out _);

            if (previous >= 0 && previous < stateIndex) return;

            SerializedValueOverride valueOverride = _states[stateIndex].Values.Overrides[localFieldIndex];
            if (previous < 0) {
                _fieldDefaultValues[fieldID] = valueOverride.GetFieldValue(_owner);
            }
            valueOverride.SetFieldValue(_owner);
        }

        private void DeactivateField(int stateIndex, int localFieldIndex) {
            int fieldID = _states[stateIndex].FieldIDs[localFieldIndex];
            int previous = GetFirstStateControllingField(fieldID, stateIndex, out SerializedValueOverride previousValueOverride);

            if (previous >= 0 && previous < stateIndex) return;

            SerializedValueOverride valueOverride = _states[stateIndex].Values.Overrides[localFieldIndex];
            if (previous < 0) {
                valueOverride.SetFieldValue(_owner, _fieldDefaultValues[fieldID]);
            } else {
                previousValueOverride.SetFieldValue(_owner);
            }
        }
    }
}
