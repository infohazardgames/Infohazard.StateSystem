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
using Infohazard.StateSystem.Editor;
using Infohazard.StateSystem.Tests.PlayMode;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Infohazard.StateSystem.Tests.Editor {
    /// <summary>
    /// The override inspector and value drawing, the state list drawer, the state behaviour inspector, and the
    /// migration report.
    /// </summary>
    public class UnitTest_EditorTooling : StateSystemTestFixture {
        private static void DrawInspector(Object target) {
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(target);
            try {
                ImGuiHarness.Run(() => editor.OnInspectorGUI());
            } finally {
                Object.DestroyImmediate(editor);
            }
        }

        #region ComponentOverrideInspector

        /// <summary>
        /// An override of <see cref="TestStateBehaviour"/> with one path of every <see cref="SerializableType"/>.
        /// </summary>
        private ComponentOverride CreateOverrideOfEveryType() {
            ComponentOverride asset = CreateOverride<TestStateBehaviour>();
            var seen = new HashSet<SerializableType>();
            foreach (string path in asset.ValidPaths.ToList()) {
                var probe = new SerializedValueOverride { Path = path };
                probe.FirstTimeInitialize(typeof(TestStateBehaviour));
                if (seen.Add(probe.Type)) asset.AddOverride(path);
            }

            CollectionAssert.AreEquivalent(
                Enum.GetValues(typeof(SerializableType)).Cast<SerializableType>().Where(t => t != SerializableType.None),
                asset.Overrides.Select(o => o.Type), "Every type should be covered.");
            return asset;
        }

        [Test]
        public void OverrideInspector_EveryValueType_Draws() {
            // Suspected bug #4: the drawer cast boxed byte/sbyte/short/ushort/uint to int and ulong to long.
            ComponentOverride asset = CreateOverrideOfEveryType();
            Assert.AreEqual("ComponentOverrideInspector", UnityEditor.Editor.CreateEditor(asset).GetType().Name);

            DrawInspector(asset);
        }

        [Test]
        public void OverrideInspector_Viewing_DoesNotChangeOrDirtyTheAsset() {
            // Suspected bug #4: a value drawn as a wider type (an int field shown as a long) never compared equal,
            // so every repaint wrote it back and marked the asset dirty.
            ComponentOverride asset = CreateOverrideOfEveryType();
            Dictionary<string, object> before = asset.Overrides.ToDictionary(o => o.Path, o => o.Value);
            int dirtyCount = EditorUtility.GetDirtyCount(asset);

            DrawInspector(asset);

            Assert.AreEqual(dirtyCount, EditorUtility.GetDirtyCount(asset), "Viewing shouldn't dirty the asset.");
            foreach (SerializedValueOverride value in asset.Overrides) {
                Assert.AreEqual(before[value.Path], value.Value, value.Path);
                Assert.AreEqual(before[value.Path]?.GetType(), value.Value?.GetType(), value.Path);
            }
        }

        [Test]
        public void OverrideInspector_InvalidOverride_Draws() {
            // Suspected bug #15: an invalid override has no FieldName, which broke drawing.
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f), ("fieldB", 2f));
            SetStoredPath(asset, asset.Overrides[0], "removedField");

            DrawInspector(asset);
        }

        [Test]
        public void OverrideInspector_InvalidNestedOverride_Draws() {
            // Suspected bug #15.
            ComponentOverride asset = CreateOverride<TestStateBehaviour>(("nestedValue/inner/x", 1f));
            SetStoredPath(asset, asset.Overrides[0], "nestedValue/removed/x");

            DrawInspector(asset);
        }

        [Test]
        public void OverrideInspector_UnknownType_Draws() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f));
            asset.TypeName = "Not.A.Real.Type";

            DrawInspector(asset);
        }

        [Test]
        public void OverrideInspector_OnEnable_AppliesRenamedPathsAndDirties() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldE", 1f));
            SetStoredPath(asset, asset.Overrides[0], "oldFieldE");
            int dirtyCount = EditorUtility.GetDirtyCount(asset);

            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(asset);
            Object.DestroyImmediate(editor);

            Assert.AreEqual("fieldE", asset.Overrides[0].Path);
            Assert.Greater(EditorUtility.GetDirtyCount(asset), dirtyCount);
        }

        [Test]
        public void UpdateHeading_NestsHeadingsForSortedPaths() {
            ComponentOverride asset = CreateOverride<TestStateBehaviour>(
                ("floatValue", 0f), ("nestedValue/inner/x", 0f), ("nestedValue/inner/y", 0), ("nestedValue/value", 0f),
                ("structValue/x", 0f));
            var expected = new Dictionary<string, string[]> {
                ["floatValue"] = new string[0],
                ["nestedValue/inner/x"] = new[] { "nestedValue", "inner" },
                ["nestedValue/inner/y"] = new[] { "nestedValue", "inner" },
                ["nestedValue/value"] = new[] { "nestedValue" },
                ["structValue/x"] = new[] { "structValue" },
            };

            ImGuiHarness.Run(() => {
                int indent = EditorGUI.indentLevel;
                var headers = new List<string>();
                foreach (string path in expected.Keys) {
                    SerializedValueDrawing.UpdateHeading(asset.Overrides.Single(o => o.Path == path), headers);
                    CollectionAssert.AreEqual(expected[path], headers, path);
                    Assert.AreEqual(indent + headers.Count, EditorGUI.indentLevel, path);
                }

                EditorGUI.indentLevel = indent;
            });
        }

        #endregion

        #region StateListDrawer

        /// <summary>
        /// Draw the component's default or custom inspector with its state list expanded.
        /// </summary>
        private static void DrawWithStatesExpanded(Component component) {
            var serializedObject = new SerializedObject(component);
            serializedObject.FindProperty("_states").isExpanded = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            DrawInspector(component);
        }

        private static State[] States(Component component) =>
            GetField<State[]>(GetField<StateList>(component, "_states"), "_states");

        [Test]
        public void StateBehaviourInspector_Draws() {
            var behaviour = CreateStateBehaviour<TestFieldsStateBehaviour>(
                new StateSpec("A", CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f))) { BlockedBy = { "B" } },
                new StateSpec("B", null));
            Assert.AreEqual("StateBehaviourInspector", UnityEditor.Editor.CreateEditor(behaviour).GetType().Name);

            DrawWithStatesExpanded(behaviour);
        }

        [Test]
        public void StateListDrawer_RenamesDuplicateStateNames() {
            var behaviour = CreateStateBehaviour<TestFieldsStateBehaviour>(
                new StateSpec("A", null), new StateSpec("A", null), new StateSpec("A", null));

            DrawWithStatesExpanded(behaviour);

            CollectionAssert.AreEqual(new[] { "A", "A_1", "A_2" }, States(behaviour).Select(s => s.Name));
        }

        [Test]
        public void StateListDrawer_ClearsOverrideForAnotherType() {
            var behaviour = CreateStateBehaviour<TestFieldsStateBehaviour>(
                new StateSpec("Wrong", CreateOverride<TestExteriorTarget>(("speed", 1f))),
                new StateSpec("Right", CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f))));

            DrawWithStatesExpanded(behaviour);

            Assert.IsNull(States(behaviour)[0].Values);
            Assert.IsNotNull(States(behaviour)[1].Values);
        }

        [Test]
        public void StateListDrawer_OverrideWithUnknownType_IsKept() {
            // Suspected bug #13: values.Type was null, which threw, and the override would have been cleared.
            ComponentOverride unknown = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f));
            unknown.TypeName = "Not.A.Real.Type";
            var behaviour = CreateStateBehaviour<TestFieldsStateBehaviour>(new StateSpec("A", unknown));

            DrawWithStatesExpanded(behaviour);

            Assert.AreSame(unknown, States(behaviour)[0].Values,
                           "An override whose type is missing (e.g. mid-refactor) shouldn't be thrown away.");
        }

        [Test]
        public void StateListDrawer_ExteriorWithoutComponent_KeepsOverrides() {
            // Suspected bug #13: with no component, the owner type was Component, so every override was cleared.
            TestExteriorTarget target = CreateGameObject("Target").AddComponent<TestExteriorTarget>();
            ComponentOverride values = CreateOverride<TestExteriorTarget>(("speed", 1f));
            ExteriorStateBehaviour exterior = AddExteriorStateBehaviour(target, new StateSpec("Fast", values));
            SetField(exterior, "_component", null);

            DrawWithStatesExpanded(exterior);

            Assert.AreSame(values, States(exterior)[0].Values);
        }

        [Test]
        public void StateListDrawer_ExteriorWithComponent_ClearsOverrideForAnotherType() {
            TestExteriorTarget target = CreateGameObject("Target").AddComponent<TestExteriorTarget>();
            ExteriorStateBehaviour exterior = AddExteriorStateBehaviour(
                target, new StateSpec("Wrong", CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f))));

            DrawWithStatesExpanded(exterior);

            Assert.IsNull(States(exterior)[0].Values);
        }

        /// <summary>
        /// Create the drawer's ReorderableList for a component's state list, as the inspector would.
        /// </summary>
        private static ReorderableList CreateDrawerList(SerializedObject serializedObject) {
            var drawer = new StateListDrawer();
            MethodInfo createList = typeof(StateListDrawer).GetMethod(
                "CreateList", BindingFlags.Instance | BindingFlags.NonPublic);
            return (ReorderableList) createList!.Invoke(drawer, new object[] { serializedObject.FindProperty("_states") });
        }

        private TestFieldsStateBehaviour CreateBlockingStates() {
            // A is blocked by C; B by nothing; C by A and B.
            return CreateStateBehaviour<TestFieldsStateBehaviour>(
                new StateSpec("A", null) { BlockedBy = { "C" } },
                new StateSpec("B", null),
                new StateSpec("C", null) { BlockedBy = { "A", "B" } });
        }

        private static Dictionary<string, string[]> BlockedByNames(Component component) {
            State[] states = States(component);
            return states.ToDictionary(
                s => s.Name,
                s => Enumerable.Range(0, states.Length).Where(i => (s.BlockedBy & (1 << i)) != 0)
                               .Select(i => states[i].Name).ToArray());
        }

        [Test]
        public void StateListDrawer_Reorder_KeepsBlockedByPointingAtSameStates() {
            // Suspected bug #14: BlockedBy holds state indices, and reordering didn't remap them.
            TestFieldsStateBehaviour behaviour = CreateBlockingStates();
            var serializedObject = new SerializedObject(behaviour);
            ReorderableList list = CreateDrawerList(serializedObject);

            // What ReorderableList does on a drag: move the element, then report the move.
            list.serializedProperty.MoveArrayElement(0, 2);
            list.onReorderCallbackWithDetails?.Invoke(list, 0, 2);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            CollectionAssert.AreEqual(new[] { "B", "C", "A" }, States(behaviour).Select(s => s.Name));
            Dictionary<string, string[]> blockedBy = BlockedByNames(behaviour);
            CollectionAssert.AreEquivalent(new[] { "C" }, blockedBy["A"]);
            CollectionAssert.IsEmpty(blockedBy["B"]);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, blockedBy["C"]);
        }

        [Test]
        public void StateListDrawer_Remove_KeepsBlockedByPointingAtSameStates() {
            // Suspected bug #14: removing a state shifted later indices without remapping masks.
            TestFieldsStateBehaviour behaviour = CreateBlockingStates();
            var serializedObject = new SerializedObject(behaviour);
            ReorderableList list = CreateDrawerList(serializedObject);

            list.index = 1;
            if (list.onRemoveCallback != null) {
                list.onRemoveCallback(list);
            } else {
                // ReorderableList's default remove.
                list.serializedProperty.DeleteArrayElementAtIndex(1);
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            CollectionAssert.AreEqual(new[] { "A", "C" }, States(behaviour).Select(s => s.Name));
            Dictionary<string, string[]> blockedBy = BlockedByNames(behaviour);
            CollectionAssert.AreEquivalent(new[] { "C" }, blockedBy["A"]);
            CollectionAssert.AreEquivalent(new[] { "A" }, blockedBy["C"], "B was removed.");
        }

        #endregion

        #region Migration

        [Test]
        public void Migration_FormatResult_ListsRenamesAndProblems() {
            var result = new ComponentOverrideMigration.Result { ModifiedAssetCount = 1, AssetCount = 3 };
            result.Renamed.Add("A.asset: _old -> _new");
            result.Unresolved.Add("B.asset: path '_gone' not found on Foo");

            string text = ComponentOverrideMigration.FormatResult(result);

            StringAssert.StartsWith("Migrated component overrides: 1 path(s) renamed in 1 of 3 asset(s).", text);
            StringAssert.Contains("1 problem(s) remain.", text);
            StringAssert.Contains("\n  Renamed A.asset: _old -> _new", text);
            StringAssert.Contains("\n  Problem: B.asset: path '_gone' not found on Foo", text);
        }

        [Test]
        public void Migration_Migrate_DirtiesChangedAsset() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldE", 1f));
            SetStoredPath(asset, asset.Overrides[0], "oldFieldE");
            int dirtyCount = EditorUtility.GetDirtyCount(asset);

            Assert.IsTrue(ComponentOverrideMigration.Migrate(asset, "Test.asset", null));

            Assert.AreEqual("fieldE", asset.Overrides[0].Path);
            Assert.Greater(EditorUtility.GetDirtyCount(asset), dirtyCount);
            Assert.IsFalse(ComponentOverrideMigration.Migrate(asset, "Test.asset", null), "Nothing left to do.");
        }

        #endregion
    }
}
