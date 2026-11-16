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
using Infohazard.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.StateSystem {
    [CreateAssetMenu(menuName = "Infohazard/Component Override")]
    public class ComponentOverride : ScriptableObject {
        [TypeSelect(typeof(Component), true, true, true)]
        [FormerlySerializedAs("typeName")] [SerializeField]
        private string _typeName;
        
        [FormerlySerializedAs("overrides")] [SerializeField]
        private SerializedValueOverride[] _overrides = Array.Empty<SerializedValueOverride>();
        
        private List<string> _validPaths;
        private Type _type;
        private bool _warnedInvalidOverrides;
        
        public string TypeName {
            get => _typeName;
            set {
                if (_typeName == value) return;

                _typeName = value;
                Refresh();
            }
        }
        
        public SerializedValueOverride[] Overrides => _overrides;

        public Type Type {
            get {
                if (_type == null) {
                    _type = TypeUtility.GetType(_typeName);
                }
                return _type;
            }
        }

        public List<string> ValidPaths {
            get {
                // Not cached while the type can't be found, in case it appears after a recompile.
                if (_validPaths == null || (_validPaths.Count == 0 && Type == null)) {
                    _validPaths = SerializedValueOverride.GetValidPathsForType(Type);
                }

                return _validPaths;
            }
        }

        public IEnumerable<string> UnusedValidPaths => ValidPaths.Where(t => _overrides.All(o => o.Path != t));

        private void OnEnable() {
            Refresh();
        }

        public void NotifyChanged() {
            Refresh();
        }

        public void NotifyChanged(int index) {
            _overrides[index].Initialize(Type);
        }

        public void AddOverride(string path) {
            if (_overrides.Any(t => t.Path == path)) return;
            int pathsIndex = ValidPaths.IndexOf(path);
            if (pathsIndex < 0) return;

            SerializedValueOverride newObj = new SerializedValueOverride { Path = path };
            newObj.FirstTimeInitialize(Type);

            Array.Resize(ref _overrides, _overrides.Length + 1);
            _overrides[_overrides.Length - 1] = newObj;

            for (int i = _overrides.Length - 1; i > 0; i--) {
                int prevIndex = ValidPaths.IndexOf(_overrides[i - 1].Path);
                if (prevIndex < pathsIndex) {
                    break;
                } else {
                    SerializedValueOverride temp = _overrides[i];
                    _overrides[i] = _overrides[i - 1];
                    _overrides[i - 1] = temp;
                }
            }
        }

        public void RemoveOverride(int index) {
            for (int i = index; i < _overrides.Length - 1; i++) {
                _overrides[i] = _overrides[i + 1];
            }
            Array.Resize(ref _overrides, _overrides.Length - 1);
        }

        public void CheckValid() {
            if (_overrides == null) return;
            foreach (SerializedValueOverride value in _overrides) {
                if (!value.Valid) {
                    value.Initialize(Type);
                }
            }
        }

        /// <summary>
        /// Whether any override's stored path refers to a member by a former name
        /// (see <see cref="FormerlyOverriddenAsAttribute"/>).
        /// </summary>
        public bool HasRenamedPaths => _overrides != null && _overrides.Any(o => o.HasRenamedPath);

        /// <summary>
        /// Rewrite any override paths that refer to members by a former name so they use the current name.
        /// An override is left unchanged (and a warning logged) if another override already uses the new path.
        /// </summary>
        /// <returns>Whether any path changed.</returns>
        public bool ApplyRenamedPaths() {
            if (_overrides == null) return false;

            bool changed = false;
            foreach (SerializedValueOverride value in _overrides) {
                if (!value.HasRenamedPath) continue;

                string newPath = value.RenamedPath;
                if (_overrides.Any(o => o != value && o.Path == newPath)) {
                    Debug.LogWarning($"{name}: cannot rename override path '{value.Path}' to '{newPath}' " +
                                     "because an override with that path already exists.", this);
                    continue;
                }

                value.ApplyRenamedPath();
                changed = true;
            }

            return changed;
        }

        private void Refresh() {
            _validPaths = null;
            _type = null;
            _warnedInvalidOverrides = false;

            if (_overrides != null) {
                foreach (SerializedValueOverride value in _overrides) {
                    value.Initialize(Type);
                }
            }
        }

        public static string DefaultNameForType(Type type) {
            return type.Name + "Override";
        }

        /// <summary>
        /// The asset name without the default suffix (see <see cref="DefaultNameForType"/>).
        /// An asset named exactly the default name gives the target type's name.
        /// </summary>
        public string displayName {
            get {
                if (Type == null) return name;

                string suffix = DefaultNameForType(Type);
                if (name == suffix) return Type.Name;
                if (name.EndsWith(suffix)) {
                    return name.Substring(0, name.Length - suffix.Length);
                } else {
                    return name;
                }
            }
        }

        /// <summary>
        /// Log a warning listing the overrides whose paths don't resolve, once per asset (until it is refreshed).
        /// </summary>
        /// <returns>Whether there are any invalid overrides.</returns>
        public bool WarnIfInvalidOverrides() {
            if (_overrides == null) return false;

            string[] invalid = _overrides.Where(o => !o.Valid).Select(o => $"'{o.Path}'").ToArray();
            if (invalid.Length == 0) return false;

            if (!_warnedInvalidOverrides) {
                _warnedInvalidOverrides = true;
                Debug.LogWarning($"{name}: these overrides don't resolve on {_typeName} and are skipped: " +
                                 $"{string.Join(", ", invalid)}.", this);
            }

            return true;
        }

        /// <summary>
        /// Apply every override to <paramref name="component"/>. Overrides that don't resolve are skipped with a
        /// warning. Throws if the component isn't of the target type.
        /// </summary>
        public void ApplyTo(Component component) {
            if (Type == null) {
                Debug.LogError($"{name}: target type '{_typeName}' was not found, so nothing was applied.", this);
                return;
            }

            if (!Type.IsAssignableFrom(component.GetType())) {
                throw new UnityException("Component " + component.name + " of incorrect type.");
            }

            WarnIfInvalidOverrides();
            foreach (SerializedValueOverride item in _overrides) {
                item.SetFieldValue(component);
            }
        }
    }
}
