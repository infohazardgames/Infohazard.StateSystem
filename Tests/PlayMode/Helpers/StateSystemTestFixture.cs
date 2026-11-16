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
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Infohazard.StateSystem.Tests.PlayMode {
    /// <summary>
    /// Describes one state for <see cref="StateSystemTestFixture.SetStates"/>.
    /// </summary>
    public class StateSpec {
        public string Name { get; }
        public ComponentOverride Values { get; }

        /// <summary>
        /// Names of the states that block this one. Turned into the index bitmask the state stores.
        /// </summary>
        public List<string> BlockedBy { get; } = new List<string>();

        /// <summary>
        /// Raw blocked-by bitmask, combined with <see cref="BlockedBy"/>. For masks that names can't express.
        /// </summary>
        public int ExtraBlockedByMask { get; set; }

        /// <summary>
        /// The serialized initial active flag.
        /// </summary>
        public bool Active { get; set; }

        public StateSpec(string name, ComponentOverride values) {
            Name = name;
            Values = values;
        }
    }

    /// <summary>
    /// Shared helpers for StateSystem tests: builds override assets and state lists, invokes lifecycle methods in
    /// edit mode, and destroys everything a test created.
    /// </summary>
    public abstract class StateSystemTestFixture {
        private const BindingFlags AnyInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void StateSystemTestFixtureTearDown() {
            DestroyTracked();
        }

        /// <summary>
        /// Destroy every object the test created, newest first.
        /// </summary>
        protected void DestroyTracked() {
            for (int i = _created.Count - 1; i >= 0; i--) {
                if (_created[i]) Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        #region Objects

        /// <summary>
        /// Track an object so it is destroyed after the test.
        /// </summary>
        protected T Track<T>(T obj) where T : Object {
            _created.Add(obj);
            return obj;
        }

        /// <summary>
        /// Create a tracked GameObject.
        /// </summary>
        protected GameObject CreateGameObject(string name, bool active = true, Transform parent = null) {
            GameObject obj = new GameObject(name);
            if (parent) obj.transform.SetParent(parent, false);
            obj.SetActive(active);
            return Track(obj);
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Create a tracked, in-memory <see cref="ComponentOverride"/> for <typeparamref name="T"/> with the given
        /// path/value pairs. Paths must be valid for the type.
        /// </summary>
        protected ComponentOverride CreateOverride<T>(params (string path, object value)[] values) {
            return CreateOverride(typeof(T), values);
        }

        /// <summary>
        /// Create a tracked, in-memory <see cref="ComponentOverride"/> for <paramref name="type"/> with the given
        /// path/value pairs. Paths must be valid for the type.
        /// </summary>
        protected ComponentOverride CreateOverride(Type type, params (string path, object value)[] values) {
            ComponentOverride asset = Track(ScriptableObject.CreateInstance<ComponentOverride>());
            asset.name = ComponentOverride.DefaultNameForType(type);
            asset.TypeName = type.FullName;
            asset.NotifyChanged();

            foreach ((string path, object value) in values) {
                SerializedValueOverride added = AddOverride(asset, path);
                SetStoredValue(added, value);
            }

            return asset;
        }

        /// <summary>
        /// Add an override for a valid path and return it.
        /// </summary>
        protected static SerializedValueOverride AddOverride(ComponentOverride asset, string path) {
            asset.AddOverride(path);
            SerializedValueOverride added = asset.Overrides.FirstOrDefault(o => o.Path == path);
            Assert.IsNotNull(added, $"Couldn't add an override for '{path}' on {asset.TypeName}.");
            return added;
        }

        /// <summary>
        /// Change an override's stored path without validating it (as if the asset was authored against members
        /// that have since changed), then re-resolve the asset.
        /// </summary>
        protected static void SetStoredPath(ComponentOverride asset, SerializedValueOverride value, string storedPath) {
            value.Path = storedPath;
            asset.NotifyChanged();
        }

        /// <summary>
        /// Set an override's value, converting it to the form <see cref="SerializedValueOverride.Value"/> stores:
        /// integer and char types are stored as <c>long</c>.
        /// </summary>
        public static void SetStoredValue(SerializedValueOverride value, object newValue) {
            value.Value = value.Type switch {
                SerializableType.Byte or SerializableType.SByte or SerializableType.Short or
                    SerializableType.UShort or SerializableType.Int or SerializableType.UInt or
                    SerializableType.Long or SerializableType.Char => Convert.ToInt64(newValue),
                SerializableType.ULong => unchecked((long) Convert.ToUInt64(newValue)),
                SerializableType.Float => Convert.ToSingle(newValue),
                SerializableType.Double => Convert.ToDouble(newValue),
                _ => newValue,
            };
        }

        #endregion

        #region States

        /// <summary>
        /// Build a <see cref="State"/> struct from a spec. <paramref name="allNames"/> gives the index of each
        /// blocking state.
        /// </summary>
        public static State CreateState(StateSpec spec, IList<string> allNames) {
            int mask = spec.ExtraBlockedByMask;
            foreach (string blocker in spec.BlockedBy) {
                int index = allNames.IndexOf(blocker);
                Assert.GreaterOrEqual(index, 0, $"Unknown blocking state '{blocker}'.");
                mask |= 1 << index;
            }

            object state = new State();
            SetField(state, "_name", spec.Name);
            SetField(state, "_values", spec.Values);
            SetField(state, "_blockedBy", mask);
            SetField(state, "_isActive", spec.Active);
            return (State) state;
        }

        /// <summary>
        /// Build a <see cref="StateList"/> from specs.
        /// </summary>
        public static StateList CreateStateList(params StateSpec[] specs) {
            List<string> names = specs.Select(s => s.Name).ToList();
            return new StateList(specs.Select(s => CreateState(s, names)).ToArray());
        }

        /// <summary>
        /// Replace the state list (the <c>_states</c> field) of a <see cref="StateBehaviour"/>,
        /// <see cref="ExteriorStateBehaviour"/> or similar component.
        /// </summary>
        public static StateList SetStates(Component owner, params StateSpec[] specs) {
            StateList list = CreateStateList(specs);
            SetField(owner, "_states", list);
            return list;
        }

        /// <summary>
        /// Add a state behaviour to <paramref name="obj"/> with the given states. Doesn't initialize it.
        /// </summary>
        protected static T AddStateBehaviour<T>(GameObject obj, params StateSpec[] specs) where T : Component {
            // In play mode, AddComponent on an active object runs Awake at once, before the states are set.
            bool wasActive = obj.activeSelf;
            if (Application.isPlaying) obj.SetActive(false);

            T behaviour = obj.AddComponent<T>();
            SetStates(behaviour, specs);

            obj.SetActive(wasActive);
            return behaviour;
        }

        /// <summary>
        /// Create a GameObject with a state behaviour that has the given states. Doesn't initialize it.
        /// </summary>
        protected T CreateStateBehaviour<T>(params StateSpec[] specs) where T : Component {
            return AddStateBehaviour<T>(CreateGameObject(typeof(T).Name), specs);
        }

        /// <summary>
        /// Add an <see cref="ExteriorStateBehaviour"/> that overrides <paramref name="target"/>.
        /// Doesn't initialize it.
        /// </summary>
        protected static ExteriorStateBehaviour AddExteriorStateBehaviour(Component target,
                                                                          params StateSpec[] specs) {
            GameObject obj = target.gameObject;
            bool wasActive = obj.activeSelf;
            if (Application.isPlaying) obj.SetActive(false);

            ExteriorStateBehaviour behaviour = obj.AddComponent<ExteriorStateBehaviour>();
            SetField(behaviour, "_component", target);
            SetStates(behaviour, specs);

            obj.SetActive(wasActive);
            return behaviour;
        }

        #endregion

        #region State managers

        /// <summary>
        /// Default state names for <see cref="AddManagedBehaviour"/>.
        /// </summary>
        public static readonly string[] DefaultManagedStates = { "A", "B", "C" };

        /// <summary>
        /// Add a child object under <paramref name="parent"/> with a <see cref="TestFieldsStateBehaviour"/>.
        /// Each named state overrides the field with the same index (state 0 sets fieldA to 1, state 1 sets fieldB
        /// to 2, and so on). Defaults to states A, B and C.
        /// </summary>
        protected TestFieldsStateBehaviour AddManagedBehaviour(Transform parent, string name = "Behaviour",
                                                               bool active = true, params string[] states) {
            if (states == null || states.Length == 0) states = DefaultManagedStates;

            GameObject obj = CreateGameObject(name, false, parent);
            var specs = new StateSpec[states.Length];
            for (int i = 0; i < states.Length; i++) {
                int field = i % TestFieldsStateBehaviour.FieldCount;
                ComponentOverride values = CreateOverride<TestFieldsStateBehaviour>(
                    (TestFieldsStateBehaviour.FieldNames[field], (object) (float) (field + 1)));
                specs[i] = new StateSpec(states[i], values);
            }

            var behaviour = AddStateBehaviour<TestFieldsStateBehaviour>(obj, specs);
            obj.SetActive(active);
            InvokeLifecycleInEditMode(behaviour);
            return behaviour;
        }

        /// <summary>
        /// Create an object with a <see cref="StateManager"/>, initialized (Awake has run).
        /// <paramref name="addChildren"/> can add state behaviours under it first, so the manager finds them.
        /// </summary>
        protected StateManager CreateManager(string name, Transform parent = null,
                                             Action<Transform> addChildren = null) {
            GameObject obj = CreateGameObject(name, false, parent);
            addChildren?.Invoke(obj.transform);
            var manager = obj.AddComponent<StateManager>();
            obj.SetActive(true);
            InvokeLifecycleInEditMode(manager);
            return manager;
        }

        /// <summary>
        /// Create an initialized <see cref="StateManager"/> with one <see cref="TestFieldsStateBehaviour"/> child
        /// that has states A, B and C.
        /// </summary>
        protected StateManager CreateManager(string name, out TestFieldsStateBehaviour behaviour,
                                             Transform parent = null) {
            TestFieldsStateBehaviour added = null;
            StateManager manager = CreateManager(name, parent, t => added = AddManagedBehaviour(t, name + "Behaviour"));
            behaviour = added;
            return manager;
        }

        /// <summary>
        /// The children a manager broadcasts to (its private <c>_children</c> list).
        /// </summary>
        public static List<StateManager> GetChildren(StateManager manager) {
            return GetField<List<StateManager>>(manager, "_children");
        }

        #endregion

        #region Reflection

        /// <summary>
        /// Set a (possibly private, possibly inherited) instance field by name. Works on boxed structs.
        /// </summary>
        public static void SetField(object target, string fieldName, object value) {
            FindField(target.GetType(), fieldName).SetValue(target, value);
        }

        /// <summary>
        /// Get a (possibly private, possibly inherited) instance field by name.
        /// </summary>
        public static T GetField<T>(object target, string fieldName) {
            return (T) FindField(target.GetType(), fieldName).GetValue(target);
        }

        private static FieldInfo FindField(Type type, string fieldName) {
            for (Type t = type; t != null; t = t.BaseType) {
                FieldInfo field = t.GetField(fieldName, AnyInstance);
                if (field != null) return field;
            }

            throw new ArgumentException($"Field {fieldName} not found on {type}.");
        }

        /// <summary>
        /// In edit mode, Unity doesn't call lifecycle methods on components added by tests. Call a parameterless
        /// lifecycle method (such as Awake) by name, searching base classes. Does nothing in play mode, where Unity
        /// already calls it.
        /// </summary>
        public static void InvokeLifecycleInEditMode(Component component, string methodName = "Awake") {
            if (Application.isPlaying) return;
            InvokeLifecycle(component, methodName);
        }

        /// <summary>
        /// Call a parameterless lifecycle method (such as Awake) by name, searching base classes.
        /// </summary>
        public static void InvokeLifecycle(Component component, string methodName) {
            for (Type t = component.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType) {
                MethodInfo method = t.GetMethod(methodName, AnyInstance, null, Type.EmptyTypes, null);
                if (method == null) continue;

                try {
                    method.Invoke(component, null);
                } catch (TargetInvocationException e) when (e.InnerException != null) {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                }

                return;
            }

            throw new ArgumentException($"{component.GetType().Name} has no {methodName} method.");
        }

        #endregion
    }
}
