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
using Infohazard.Core.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Infohazard.StateSystem.Editor {
    [CustomPropertyDrawer(typeof(StateList))]
    public class StateListDrawer : DraggableListDrawer {
        private const int Gap = 4;
        private static readonly List<string> Headers = new();
        private string[] _stateNames;
        private SerializedObject _serializedObject;

        protected override ReorderableList CreateList(SerializedProperty prop) {
            SerializedProperty listProp = GetListProperty(prop);
            ReorderableList list = base.CreateList(prop);
            list.elementHeightCallback = index => GetElementHeight(listProp, index);
            list.onAddDropdownCallback = ListDropdown;
            list.onReorderCallbackWithDetails = RemapBlockedByAfterMove;
            list.onRemoveCallback = RemoveState;
            return list;
        }

        /// <summary>
        /// Remap every state's <c>_blockedBy</c> mask with <paramref name="mapIndex"/>, which gives each old state
        /// index its new index, or -1 if that state is gone. Bits past the end of the list are left alone.
        /// </summary>
        private static void RemapBlockedBy(SerializedProperty listProperty, int oldCount, Func<int, int> mapIndex) {
            for (int i = 0; i < listProperty.arraySize; i++) {
                SerializedProperty maskProperty =
                    listProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_blockedBy");
                int oldMask = maskProperty.intValue;
                int newMask = 0;
                for (int bit = 0; bit < 32; bit++) {
                    if ((oldMask & (1 << bit)) == 0) continue;
                    int newBit = bit < oldCount ? mapIndex(bit) : bit;
                    if (newBit >= 0 && newBit < 32) newMask |= 1 << newBit;
                }

                maskProperty.intValue = newMask;
            }
        }

        /// <summary>
        /// After a state is dragged from <paramref name="oldIndex"/> to <paramref name="newIndex"/> (the list has
        /// already moved it), keep each <c>_blockedBy</c> mask pointing at the same states.
        /// </summary>
        private static void RemapBlockedByAfterMove(ReorderableList list, int oldIndex, int newIndex) {
            SerializedProperty listProperty = list.serializedProperty;
            RemapBlockedBy(listProperty, listProperty.arraySize, i => {
                if (i == oldIndex) return newIndex;
                if (oldIndex < newIndex && i > oldIndex && i <= newIndex) return i - 1;
                if (newIndex < oldIndex && i >= newIndex && i < oldIndex) return i + 1;
                return i;
            });
            listProperty.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Remove the selected state and keep each <c>_blockedBy</c> mask pointing at the same states.
        /// </summary>
        private static void RemoveState(ReorderableList list) {
            SerializedProperty listProperty = list.serializedProperty;
            int index = list.index;
            if (index < 0 || index >= listProperty.arraySize) return;

            int oldCount = listProperty.arraySize;
            listProperty.DeleteArrayElementAtIndex(index);
            RemapBlockedBy(listProperty, oldCount, i => i == index ? -1 : i > index ? i - 1 : i);
            listProperty.serializedObject.ApplyModifiedProperties();

            if (list.index >= listProperty.arraySize) list.index = listProperty.arraySize - 1;
        }

        protected override SerializedProperty GetListProperty(SerializedProperty prop) {
            return prop.isArray ? prop : prop.FindPropertyRelative("_states");
        }

        /// <summary>
        /// The type the states override, or null if it isn't known yet (an <see cref="ExteriorStateBehaviour"/>
        /// with no component reports plain <see cref="Component"/>).
        /// </summary>
        private static Type GetStateType(SerializedProperty property) {
            Object target = property.serializedObject.targetObject;
            Type type = target is IStateBehaviour stateBehaviour ? stateBehaviour.Type : target.GetType();
            return type == typeof(Component) ? null : type;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (property.isExpanded) {
                Type type = GetStateType(property);
                SerializedProperty listProp = GetListProperty(property);
                List<string> names = new List<string>();
                for (int i = 0; i < listProp.arraySize; i++) {
                    SerializedProperty nameProperty = GetElementProperty(property, i).FindPropertyRelative("_name");

                    string suffix = "";
                    int num = 0;
                    while (names.Contains(nameProperty.stringValue + suffix)) {
                        num++;
                        suffix = "_" + num;
                    }

                    if (num > 0) nameProperty.stringValue += suffix;
                    names.Add(nameProperty.stringValue);

                    SerializedProperty valuesProperty = GetElementProperty(property, i).FindPropertyRelative("_values");
                    ComponentOverride values = valuesProperty.objectReferenceValue as ComponentOverride;

                    // Only clear an override when both types are known and don't match. A missing type may be
                    // temporary (mid-refactor, or the component isn't assigned yet).
                    if (values && type != null && values.Type != null && !values.Type.IsAssignableFrom(type)) {
                        valuesProperty.objectReferenceValue = null;
                    }
                }

                _stateNames = names.ToArray();
            }

            base.OnGUI(position, property, label);
        }

        private SerializedProperty CreateNewState(SerializedProperty listProperty, string name,
                                                  ComponentOverride values) {
            listProperty.serializedObject.Update();
            Undo.RecordObject(listProperty.serializedObject.targetObject, "Create State");
            listProperty.arraySize++;

            SerializedProperty newState = listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1);
            newState.FindPropertyRelative("_name").stringValue = name;
            newState.FindPropertyRelative("_values").objectReferenceValue = values;
            newState.FindPropertyRelative("_blockedBy").intValue = 0;
            newState.FindPropertyRelative("_isActive").boolValue = false;
            listProperty.serializedObject.ApplyModifiedProperties();

            return newState;
        }

        private void ListDropdown(Rect rect, ReorderableList list) {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Use Existing Override"),
                         false, data => { CreateNewState(list.serializedProperty, "Empty", null); }, null);

            if (GetStateType(list.serializedProperty) == null) {
                menu.AddDisabledItem(new GUIContent("Create New Override (assign the component first)"));
                menu.ShowAsContext();
                return;
            }

            menu.AddItem(new GUIContent("Create New Override"),
                         false, data => {
                             Type type = GetStateType(list.serializedProperty);
                             string path = EditorUtility.SaveFilePanelInProject(
                                 "Save New Override", ComponentOverride.DefaultNameForType(type), "asset", "Create");
                             if (!string.IsNullOrEmpty(path)) {
                                 ComponentOverride newOverride = ScriptableObject.CreateInstance<ComponentOverride>();
                                 newOverride.TypeName = type.FullName;
                                 AssetDatabase.CreateAsset(newOverride, path);
                                 CreateNewState(list.serializedProperty, newOverride.displayName, newOverride);
                             }
                         }, null);

            menu.ShowAsContext();
        }

        protected override float GetElementHeight(SerializedProperty prop, int index) {
            SerializedProperty element = GetElementProperty(prop, index);

            float total = EditorGUIUtility.singleLineHeight;
            if (!element.isExpanded) {
                return total;
            }

            SerializedProperty valuesProperty = element.FindPropertyRelative("_values");
            if (valuesProperty.objectReferenceValue is not ComponentOverride asset) {
                return total;
            }

            total += (asset.Overrides.Length + 1) *
                     (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);

            return total;
        }

        protected override void DrawElement(Rect rect, int index, bool isActive, bool isFocused,
                                            SerializedProperty prop) {
            SerializedProperty element = GetElementProperty(prop, index);

            Rect firstLineRect = rect;
            firstLineRect.height = EditorGUIUtility.singleLineHeight;
            Rect valuesRect = rect;
            valuesRect.yMin = firstLineRect.yMax + EditorGUIUtility.standardVerticalSpacing;

            MathUtility.SplitHorizontal(firstLineRect, Gap, out Rect r1, out Rect r2, 0.7f);
            MathUtility.SplitHorizontal(r1, Gap, out Rect r3, out Rect r4);
            MathUtility.SplitHorizontal(r2, Gap, out Rect r5, out Rect r6, 0.4f);

            SerializedProperty valuesProperty = element.FindPropertyRelative("_values");

            ComponentOverride asset = valuesProperty.objectReferenceValue as ComponentOverride;

            if (asset != null) {
                r3.xMin += 12;
                element.isExpanded = EditorGUI.Foldout(r3, element.isExpanded, GUIContent.none);
            }

            EditorGUI.PropertyField(r3, element.FindPropertyRelative("_name"), GUIContent.none);
            EditorGUI.PropertyField(r4, valuesProperty, GUIContent.none);

            TextAnchor align = GUI.skin.label.alignment;
            GUI.skin.label.alignment = TextAnchor.MiddleRight;

            r5.xMin += Mathf.Max(0, r5.width - 75);
            EditorGUI.LabelField(r5, new GUIContent("Blocked By:"));
            GUI.skin.label.alignment = align;

            r6.xMax -= r6.height + Gap;
            Rect r7 = new Rect(r6.xMax + Gap, r6.y, r6.height, r6.height);

            if (Application.isPlaying && prop.serializedObject.targetObject is IStateBehaviour stateBehavior) {
                string name = element.FindPropertyRelative("_name").stringValue;
                bool active = stateBehavior.IsStateActive(name);
                bool newActive = EditorGUI.Toggle(r7, active);
                if (newActive != active) {
                    stateBehavior.SetStateActive(name, newActive);
                }
            } else {
                EditorGUI.PropertyField(r7, element.FindPropertyRelative("_isActive"), GUIContent.none);
            }

            SerializedProperty maskProp = element.FindPropertyRelative("_blockedBy");
            maskProp.intValue = EditorGUI.MaskField(r6, maskProp.intValue, _stateNames) & ~(1 << index);

            if (asset && element.isExpanded) {
                DrawComponentOverrideGUI(valuesRect, asset);
            }
        }

        private void AddOverrideListDropdown(ComponentOverride asset) {
            GenericMenu menu = new GenericMenu();

            foreach (string path in asset.UnusedValidPaths) {
                menu.AddItem(new GUIContent(path),
                             false, (object data) => {
                                 Undo.RecordObject(asset, "Add Override");
                                 asset.AddOverride(path);
                                 EditorUtility.SetDirty(asset);
                             },
                             null);
            }

            menu.ShowAsContext();
        }

        private void RemoveOverride(ComponentOverride asset, int index) {
            Undo.RecordObject(asset, "Remove Override");
            asset.RemoveOverride(index);
            EditorUtility.SetDirty(asset);
        }

        private SerializedObject GetSerializedObject(ComponentOverride target) {
            if (_serializedObject?.targetObject != target) {
                if (target.ApplyRenamedPaths()) {
                    EditorUtility.SetDirty(target);
                }

                _serializedObject = new SerializedObject(target);
            }

            return _serializedObject;
        }

        public void DrawComponentOverrideGUI(Rect rect, ComponentOverride asset) {
            asset.CheckValid();

            SerializedObject serializedObject = GetSerializedObject(asset);
            serializedObject.Update();

            Rect lineRect = rect;
            lineRect.xMin += 16;
            lineRect.height = EditorGUIUtility.singleLineHeight;

            if (asset.Type == null) {
                EditorGUI.LabelField(lineRect, "Invalid Target Type", EditorStyles.boldLabel);
            } else {
                int toRemove = -1;
                Headers.Clear();
                int indent = EditorGUI.indentLevel;
                for (int i = 0; i < asset.Overrides.Length; i++) {
                    SerializedValueOverride prop = asset.Overrides[i];
                    SerializedValueDrawing.UpdateHeading(prop, Headers);

                    Rect valueRect = lineRect;
                    valueRect.xMax -= 20 + Gap;
                    Rect buttonRect = lineRect;
                    buttonRect.xMin = valueRect.xMax + Gap;

                    SerializedValueDrawing.DrawRect(valueRect, asset, prop);

                    if (GUI.Button(buttonRect, "X")) {
                        toRemove = i;
                    }

                    lineRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                EditorGUI.BeginDisabledGroup(!asset.UnusedValidPaths.Any());
                EditorGUI.indentLevel = indent;

                Rect addButtonRect = lineRect;
                addButtonRect.width = Mathf.Min(addButtonRect.width, 150);
                if (EditorGUI.DropdownButton(
                        addButtonRect,
                        new GUIContent("Add Override"),
                        FocusType.Keyboard)) {
                    AddOverrideListDropdown(asset);
                }

                EditorGUI.EndDisabledGroup();

                if (toRemove >= 0) {
                    RemoveOverride(asset, toRemove);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
