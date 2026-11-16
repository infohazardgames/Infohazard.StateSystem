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
using System.Text.RegularExpressions;
using Infohazard.StateSystem.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Infohazard.StateSystem.Tests.Editor {
    public class UnitTest_RenamedOverridePaths {
        private ComponentOverride _asset;

        [SetUp]
        public void SetUp() {
            _asset = ScriptableObject.CreateInstance<ComponentOverride>();
            _asset.name = "TestOverride";
            _asset.TypeName = typeof(RenamedMembersTestTarget).FullName;
            _asset.NotifyChanged();
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(_asset);
        }

        /// <summary>
        /// Add an override using a current path, then rewrite its stored path to an old one,
        /// as if the asset had been authored before the member was renamed.
        /// </summary>
        private SerializedValueOverride AddOverrideWithStoredPath(string currentPath, string storedPath) {
            _asset.AddOverride(currentPath);
            SerializedValueOverride value = Array.Find(_asset.Overrides, o => o.Path == currentPath);
            Assert.IsNotNull(value, $"Could not add override for {currentPath}");
            value.Path = storedPath;
            _asset.NotifyChanged();
            return value;
        }

        [Test]
        public void Initialize_FieldWithFormerlySerializedAs_ResolvesAndReportsRename() {
            SerializedValueOverride value = AddOverrideWithStoredPath("_speed", "_oldSpeed");

            Assert.IsTrue(value.Valid);
            Assert.AreEqual("_oldSpeed", value.Path);
            Assert.AreEqual("_speed", value.RenamedPath);
            Assert.AreEqual("_speed", value.FieldName);
        }

        [Test]
        public void Initialize_FieldWithFormerlyOverriddenAs_ResolvesAndReportsRename() {
            SerializedValueOverride value = AddOverrideWithStoredPath("_distance", "_oldDistance");

            Assert.IsTrue(value.Valid);
            Assert.AreEqual("_distance", value.RenamedPath);
        }

        [Test]
        public void Initialize_PropertyWithFormerlyOverriddenAs_ResolvesAndReportsRename() {
            SerializedValueOverride value = AddOverrideWithStoredPath("IsOn", "OldIsOn");

            Assert.IsTrue(value.Valid);
            Assert.IsTrue(value.IsProperty);
            Assert.AreEqual("IsOn", value.RenamedPath);
        }

        [Test]
        public void Initialize_NestedPathWithRenamedParentAndChild_RenamesBothParts() {
            SerializedValueOverride value = AddOverrideWithStoredPath("_spring/stiffness", "_oldSpring/oldStiffness");

            Assert.IsTrue(value.Valid);
            Assert.AreEqual("_spring/stiffness", value.RenamedPath);
        }

        [Test]
        public void Initialize_NestedPathWithOnlyChildRenamed_RenamesChild() {
            SerializedValueOverride value = AddOverrideWithStoredPath("_spring/stiffness", "_spring/oldStiffness");

            Assert.IsTrue(value.Valid);
            Assert.AreEqual("_spring/stiffness", value.RenamedPath);
        }

        [Test]
        public void Initialize_CurrentPath_HasNoRename() {
            _asset.AddOverride("_count");
            SerializedValueOverride value = _asset.Overrides[0];

            Assert.IsTrue(value.Valid);
            Assert.IsFalse(value.HasRenamedPath);
            Assert.IsFalse(_asset.HasRenamedPaths);
        }

        [Test]
        public void Initialize_UnknownPath_IsInvalidWithNoRename() {
            SerializedValueOverride value = AddOverrideWithStoredPath("_count", "_doesNotExist");

            Assert.IsFalse(value.Valid);
            Assert.IsFalse(value.HasRenamedPath);
            Assert.AreEqual(0, value.FieldChain.Count);
        }

        [Test]
        public void ApplyRenamedPaths_RewritesStoredPaths() {
            SerializedValueOverride speed = AddOverrideWithStoredPath("_speed", "_oldSpeed");
            SerializedValueOverride spring = AddOverrideWithStoredPath("_spring/stiffness", "_oldSpring/oldStiffness");
            Assert.IsTrue(_asset.HasRenamedPaths);

            Assert.IsTrue(_asset.ApplyRenamedPaths());

            Assert.AreEqual("_speed", speed.Path);
            Assert.AreEqual("_spring/stiffness", spring.Path);
            Assert.IsFalse(_asset.HasRenamedPaths);
            Assert.IsFalse(_asset.ApplyRenamedPaths());

            _asset.NotifyChanged();
            Assert.IsTrue(speed.Valid);
            Assert.IsFalse(speed.HasRenamedPath);
        }

        [Test]
        public void ApplyRenamedPaths_NewPathAlreadyOverridden_LeavesOldPathAndWarns() {
            SerializedValueOverride old = AddOverrideWithStoredPath("_speed", "_oldSpeed");
            _asset.AddOverride("_speed");

            LogAssert.Expect(LogType.Warning, new Regex("cannot rename override path"));
            Assert.IsFalse(_asset.ApplyRenamedPaths());

            Assert.AreEqual("_oldSpeed", old.Path);
            Assert.IsTrue(old.HasRenamedPath);
        }

        [Test]
        public void SetFieldValue_BeforeMigration_SetsRenamedMembers() {
            SerializedValueOverride speed = AddOverrideWithStoredPath("_speed", "_oldSpeed");
            speed.Value = 12.5f;
            SerializedValueOverride stiffness = AddOverrideWithStoredPath("_spring/stiffness", "_oldSpring/oldStiffness");
            stiffness.Value = 3f;
            SerializedValueOverride isOn = AddOverrideWithStoredPath("IsOn", "OldIsOn");
            isOn.Value = true;

            // Same as ComponentOverride.ApplyTo, which requires a Component (not possible in an editor-only assembly).
            RenamedMembersTestTarget target = new RenamedMembersTestTarget();
            foreach (SerializedValueOverride value in _asset.Overrides) {
                value.SetFieldValue(target);
            }

            Assert.AreEqual(12.5f, target.Speed);
            Assert.AreEqual(3f, target.Spring.stiffness);
            Assert.IsTrue(target.IsOn);
        }

        [Test]
        public void Migrate_RecordsRenamesAndUnresolvedPaths() {
            AddOverrideWithStoredPath("_speed", "_oldSpeed");
            AddOverrideWithStoredPath("_count", "_doesNotExist");
            ComponentOverrideMigration.Result result = new ComponentOverrideMigration.Result();

            bool changed = ComponentOverrideMigration.Migrate(_asset, "Test.asset", result);

            Assert.IsTrue(changed);
            Assert.AreEqual(1, result.ModifiedAssetCount);
            CollectionAssert.AreEqual(new[] { "Test.asset: _oldSpeed -> _speed" }, result.Renamed);
            Assert.AreEqual(1, result.Unresolved.Count);
            StringAssert.Contains("_doesNotExist", result.Unresolved[0]);
        }

        [Test]
        public void Migrate_MissingType_ReportsUnresolvedType() {
            _asset.TypeName = "Not.A.Real.Type";
            ComponentOverrideMigration.Result result = new ComponentOverrideMigration.Result();

            Assert.IsFalse(ComponentOverrideMigration.Migrate(_asset, "Test.asset", result));
            Assert.AreEqual(1, result.Unresolved.Count);
            StringAssert.Contains("Not.A.Real.Type", result.Unresolved[0]);
        }
    }
}
