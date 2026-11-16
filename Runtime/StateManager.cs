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
using Object = UnityEngine.Object;

namespace Infohazard.StateSystem {
    /// <summary>
    /// Broadcasts named states to every <see cref="IStateBehaviour"/> in its children.
    /// </summary>
    /// <remarks>
    /// A manager can have a parent manager. While it has one, it follows the states of the root of its hierarchy,
    /// and states set on it directly are remembered but not applied. They apply once it is unparented. When the
    /// parent changes, states that were on through the old hierarchy and aren't set in the new one are turned off.
    /// </remarks>
    public class StateManager : MonoBehaviour {
        [SerializeField] private StateManager _parent;

        private IStateBehaviour[] _behaviours;
        private Dictionary<string, bool> _statesActive;
        private List<StateManager> _children;
        private bool _initialized;

        private static readonly Dictionary<string, bool> EmptyStates = new Dictionary<string, bool>();

        public StateManager Parent {
            get => _parent;
            set {
                Initialize();
                if (value) value.Initialize();

                if (value == _parent) return;

                for (StateManager ancestor = value; ancestor; ancestor = ancestor._parent) {
                    if (ancestor == this) {
                        Debug.LogError($"Can't make {value.name} the parent of {name}: it is {name} or one of its " +
                                       "descendants.", this);
                        return;
                    }
                }

                var oldStates = new Dictionary<string, bool>(EffectiveStates);

                if (_parent && _parent._children != null) _parent._children.Remove(this);
                if (value && !value._children.Contains(this)) value._children.Add(this);
                _parent = value;

                ApplyStateChange(oldStates, EffectiveStates);
            }
        }

        /// <summary>
        /// The manager at the top of this one's hierarchy, whose states this one follows.
        /// </summary>
        public StateManager Root {
            get {
                StateManager root = this;
                while (root._parent) root = root._parent;
                return root;
            }
        }

        /// <summary>
        /// The states this manager applies: its own if it has no parent, otherwise its root's.
        /// </summary>
        private Dictionary<string, bool> EffectiveStates => Root._statesActive ?? EmptyStates;

        private void Awake() {
            Initialize();
        }

        private void Initialize() {
            if (_initialized) return;

            _initialized = true;
            _statesActive = new Dictionary<string, bool>();
            _children = new List<StateManager>();
            RefreshStateBehaviours();
        }

        private void Start() {
            // Hook up a parent assigned in the inspector. If one was set in code since Awake, it's already hooked up.
            if (!_parent || _parent._children == null || _parent._children.Contains(this)) return;

            StateManager tempParent = _parent;
            _parent = null;
            Parent = tempParent;
        }

        private void OnDestroy() {
            if (_parent && _parent._children != null) {
                _parent._children.Remove(this);
            }

            if (_children == null) return;
            foreach (StateManager child in _children.ToList()) {
                if (child) child.Parent = null;
            }
        }

        private void ApplyInternalStates() {
            foreach (KeyValuePair<string, bool> item in EffectiveStates) {
                SetStateActiveInternal(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Turn off states that were on in <paramref name="oldStates"/> and aren't set in
        /// <paramref name="newStates"/>, then apply <paramref name="newStates"/>.
        /// </summary>
        private void ApplyStateChange(Dictionary<string, bool> oldStates, Dictionary<string, bool> newStates) {
            foreach (KeyValuePair<string, bool> item in oldStates) {
                if (item.Value && !newStates.ContainsKey(item.Key)) {
                    SetStateActiveInternal(item.Key, false);
                }
            }

            foreach (KeyValuePair<string, bool> item in newStates.ToList()) {
                SetStateActiveInternal(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Find the state behaviours in this object's children again. Newly found ones get the current states;
        /// ones no longer found have this manager's active states turned off.
        /// </summary>
        public void RefreshStateBehaviours() {
            if (!_initialized) {
                Initialize();
                return;
            }

            IStateBehaviour[] oldBehaviours = _behaviours;
            _behaviours = GetComponentsInChildren<IStateBehaviour>(true);

            if (oldBehaviours != null) {
                foreach (IStateBehaviour removed in oldBehaviours) {
                    if (IsDestroyed(removed) || Array.IndexOf(_behaviours, removed) >= 0) continue;
                    foreach (KeyValuePair<string, bool> item in EffectiveStates) {
                        if (item.Value) removed.SetStateActive(item.Key, false);
                    }
                }
            }

            ApplyInternalStates();
        }

        public bool IsStateActiveOnAny(string state) => _behaviours?.Any(b => b.IsStateActive(state)) ?? false;

        /// <summary>
        /// Whether every behaviour that has <paramref name="state"/> has it active. False if none have it.
        /// </summary>
        public bool IsStateActiveOnAll(string state) {
            if (_behaviours == null) return false;

            bool any = false;
            foreach (IStateBehaviour behaviour in _behaviours) {
                if (IsDestroyed(behaviour) || !behaviour.HasState(state)) continue;
                if (!behaviour.IsStateActive(state)) return false;
                any = true;
            }

            return any;
        }

        public void ForgetState(string state) => _statesActive?.Remove(state);

        public bool SetStateActive(string state, bool active) {
            Initialize();

            if (_statesActive.TryGetValue(state, out bool curValue) && curValue == active) {
                return false;
            }

            _statesActive[state] = active;

            if (!_parent) {
                SetStateActiveInternal(state, active);
            }

            return true;
        }

        private void SetStateActiveInternal(string state, bool active) {
            Initialize();

            foreach (IStateBehaviour b in _behaviours) {
                if (IsDestroyed(b)) continue;
                b.SetStateActive(state, active);
            }

            foreach (StateManager child in _children) {
                if (child) child.SetStateActiveInternal(state, active);
            }
        }

        private static bool IsDestroyed(IStateBehaviour behaviour) => behaviour is Object obj && !obj;
    }
}
