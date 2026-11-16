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

using System.Linq;
using System.Text.RegularExpressions;
using Infohazard.StateSystem.Tests.PlayMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Infohazard.StateSystem.Tests.Editor {
    /// <summary>
    /// The override asset: target type, the list of overrides, display names, and applying values to a component.
    /// </summary>
    public class UnitTest_ComponentOverride : StateSystemTestFixture {
        private static string[] Paths(ComponentOverride asset) => asset.Overrides.Select(o => o.Path).ToArray();

        #region Type

        [Test]
        public void TypeName_ResolvesType() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>();

            Assert.AreEqual(typeof(TestFieldsStateBehaviour).FullName, asset.TypeName);
            Assert.AreEqual(typeof(TestFieldsStateBehaviour), asset.Type);
        }

        [Test]
        public void TypeName_Unknown_TypeIsNull() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>();
            asset.TypeName = "Not.A.Real.Type";
            Assert.IsNull(asset.Type);
        }

        [Test]
        public void TypeName_Changed_PathsAndOverridesFollowNewType() {
            // Suspected bug #24: the setter reset the cached type but not the cached paths or the overrides.
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f));
            Assert.IsTrue(asset.ValidPaths.Contains("fieldA"));

            asset.TypeName = typeof(TestExteriorTarget).FullName;

            Assert.AreEqual(typeof(TestExteriorTarget), asset.Type);
            Assert.IsTrue(asset.ValidPaths.Contains("speed"));
            Assert.IsFalse(asset.ValidPaths.Contains("fieldA"));
            Assert.IsFalse(asset.Overrides[0].Valid, "fieldA doesn't exist on the new type.");
        }

        [Test]
        public void TypeName_SetToSameValue_KeepsResolvedOverrides() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f));
            SerializedValueOverride value = asset.Overrides[0];

            asset.TypeName = asset.TypeName;

            Assert.IsTrue(value.Valid);
            Assert.AreEqual(1, value.FieldChain.Count);
        }

        #endregion

        #region Paths

        [Test]
        public void ValidPaths_MatchesPathsForType() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>();
            CollectionAssert.AreEqual(
                SerializedValueOverride.GetValidPathsForType(typeof(TestFieldsStateBehaviour)), asset.ValidPaths);
        }

        [Test]
        public void ValidPaths_UnknownType_IsEmpty() {
            // Suspected bug #22: GetValidPathsForType(null) threw NullReferenceException.
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>();
            asset.TypeName = "Not.A.Real.Type";

            Assert.DoesNotThrow(() => CollectionAssert.IsEmpty(asset.ValidPaths));
            Assert.DoesNotThrow(() => CollectionAssert.IsEmpty(asset.UnusedValidPaths.ToList()));
        }

        [Test]
        public void UnusedValidPaths_ExcludesOverriddenPaths() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldB", 1f));

            CollectionAssert.DoesNotContain(asset.UnusedValidPaths.ToList(), "fieldB");
            CollectionAssert.Contains(asset.UnusedValidPaths.ToList(), "fieldA");
            Assert.AreEqual(asset.ValidPaths.Count - 1, asset.UnusedValidPaths.Count());
        }

        #endregion

        #region AddOverride / RemoveOverride

        [Test]
        public void AddOverride_KeepsValidPathsOrder() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>();

            asset.AddOverride("fieldC");
            asset.AddOverride("fieldA");
            asset.AddOverride("fieldD");
            asset.AddOverride("fieldB");

            CollectionAssert.AreEqual(new[] { "fieldA", "fieldB", "fieldC", "fieldD" }, Paths(asset));
        }

        [Test]
        public void AddOverride_SetsTypeAndResolves() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>();

            asset.AddOverride("fieldA");

            Assert.AreEqual(SerializableType.Float, asset.Overrides[0].Type);
            Assert.IsTrue(asset.Overrides[0].Valid);
        }

        [Test]
        public void AddOverride_Duplicate_IsIgnored() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 3f));

            asset.AddOverride("fieldA");

            Assert.AreEqual(1, asset.Overrides.Length);
            Assert.AreEqual(3f, asset.Overrides[0].Value, "The existing value should be kept.");
        }

        [Test]
        public void AddOverride_InvalidPath_IsIgnored() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>();

            asset.AddOverride("notAField");

            Assert.AreEqual(0, asset.Overrides.Length);
        }

        [Test]
        public void AddOverride_NextToInvalidOverride_AddsIt() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldC", 1f));
            SetStoredPath(asset, asset.Overrides[0], "missing");

            asset.AddOverride("fieldA");

            CollectionAssert.AreEquivalent(new[] { "missing", "fieldA" }, Paths(asset));
        }

        [TestCase(0, new[] { "fieldB", "fieldC" })]
        [TestCase(1, new[] { "fieldA", "fieldC" })]
        [TestCase(2, new[] { "fieldA", "fieldB" })]
        public void RemoveOverride_RemovesIndex(int index, string[] expected) {
            ComponentOverride asset =
                CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f), ("fieldB", 2f), ("fieldC", 3f));

            asset.RemoveOverride(index);

            CollectionAssert.AreEqual(expected, Paths(asset));
        }

        #endregion

        #region Refreshing

        [Test]
        public void NotifyChangedIndex_ReinitializesThatOverride() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f), ("fieldB", 2f));
            asset.Overrides[1].Path = "missing";
            Assert.IsTrue(asset.Overrides[1].Valid, "Not re-resolved yet.");

            asset.NotifyChanged(1);

            Assert.IsFalse(asset.Overrides[1].Valid);
            Assert.IsTrue(asset.Overrides[0].Valid);
        }

        [Test]
        public void CheckValid_ReinitializesInvalidOverrides() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f));
            SetStoredPath(asset, asset.Overrides[0], "missing");
            asset.Overrides[0].Path = "fieldA";

            asset.CheckValid();

            Assert.IsTrue(asset.Overrides[0].Valid);
        }

        [Test]
        public void CheckValid_NullOverrides_DoesNotThrow() {
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>();
            SetField(asset, "_overrides", null);

            Assert.DoesNotThrow(asset.CheckValid);
            Assert.DoesNotThrow(asset.NotifyChanged);
            Assert.IsFalse(asset.HasRenamedPaths);
        }

        [Test]
        public void Instantiate_ResolvesPathsOnEnable() {
            // The resolved member chain isn't serialized, so a loaded or copied asset must resolve it in OnEnable.
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f), ("fieldB", 2f));

            ComponentOverride copy = Track(Object.Instantiate(asset));

            Assert.AreEqual(2, copy.Overrides.Length);
            Assert.AreNotSame(asset.Overrides[0], copy.Overrides[0]);
            foreach (SerializedValueOverride value in copy.Overrides) {
                Assert.IsTrue(value.Valid, value.ToString());
                Assert.AreEqual(1, value.FieldChain.Count);
            }

            Assert.AreEqual(2f, copy.Overrides[1].Value);
        }

        #endregion

        #region Names

        [Test]
        public void DefaultNameForType_AppendsOverride() {
            Assert.AreEqual("TestExteriorTargetOverride", ComponentOverride.DefaultNameForType(typeof(TestExteriorTarget)));
        }

        [Test]
        public void DisplayName_StripsDefaultSuffix() {
            ComponentOverride asset = CreateOverride<TestExteriorTarget>();

            asset.name = "FastTestExteriorTargetOverride";
            Assert.AreEqual("Fast", asset.displayName);

            asset.name = "Something Else";
            Assert.AreEqual("Something Else", asset.displayName);
        }

        [Test]
        public void DisplayName_DefaultName_IsNotEmpty() {
            // Suspected bug #22: an asset with exactly the default name (what "Create New Override" suggests)
            // had an empty display name, so the new state was named "".
            ComponentOverride asset = CreateOverride<TestExteriorTarget>();
            asset.name = ComponentOverride.DefaultNameForType(typeof(TestExteriorTarget));

            Assert.AreEqual("TestExteriorTarget", asset.displayName);
        }

        [Test]
        public void DisplayName_UnknownType_IsAssetName() {
            // Suspected bug #22: threw NullReferenceException.
            ComponentOverride asset = CreateOverride<TestExteriorTarget>();
            asset.name = "Broken";
            asset.TypeName = "Not.A.Real.Type";

            Assert.AreEqual("Broken", asset.displayName);
        }

        #endregion

        #region ApplyTo

        [Test]
        public void ApplyTo_SetsEveryOverride() {
            TestExteriorTarget target = CreateGameObject("Target").AddComponent<TestExteriorTarget>();
            ComponentOverride asset =
                CreateOverride<TestExteriorTarget>(("speed", 2f), ("count", 3), ("label", "x"), ("flag", true));

            asset.ApplyTo(target);

            Assert.AreEqual(2f, target.speed);
            Assert.AreEqual(3, target.count);
            Assert.AreEqual("x", target.label);
            Assert.IsTrue(target.flag);
        }

        [Test]
        public void ApplyTo_DerivedComponent_SetsBaseMembers() {
            var target = CreateGameObject("Target").AddComponent<TestDerivedStateBehaviour>();
            ComponentOverride asset =
                CreateOverride<TestStateBehaviourBase>(("basePublic", 2f), ("_baseSerialized", 3f));

            asset.ApplyTo(target);

            Assert.AreEqual(2f, target.basePublic);
            Assert.AreEqual(3f, target.BaseSerialized);
        }

        [Test]
        public void ApplyTo_WrongComponentType_Throws() {
            TestExteriorTarget target = CreateGameObject("Target").AddComponent<TestExteriorTarget>();
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f));

            Assert.Throws<UnityException>(() => asset.ApplyTo(target));
        }

        [Test]
        public void ApplyTo_InvalidOverride_AppliesOthersAndWarnsOnce() {
            // Suspected bug #1: an override that no longer resolves threw and stopped the rest being applied.
            TestExteriorTarget target = CreateGameObject("Target").AddComponent<TestExteriorTarget>();
            ComponentOverride asset = CreateOverride<TestExteriorTarget>(("count", 3), ("speed", 2f));
            SetStoredPath(asset, asset.Overrides.First(o => o.Path == "count"), "oldCount");

            LogAssert.Expect(LogType.Warning, new Regex("oldCount"));
            Assert.DoesNotThrow(() => asset.ApplyTo(target));
            Assert.AreEqual(2f, target.speed);
            Assert.AreEqual(0, target.count);

            asset.ApplyTo(target);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ApplyTo_UnknownType_LogsErrorAndAppliesNothing() {
            // Suspected bug #22: threw NullReferenceException.
            TestExteriorTarget target = CreateGameObject("Target").AddComponent<TestExteriorTarget>();
            ComponentOverride asset = CreateOverride<TestExteriorTarget>(("speed", 2f));
            asset.TypeName = "Not.A.Real.Type";

            LogAssert.Expect(LogType.Error, new Regex("Not.A.Real.Type"));
            Assert.DoesNotThrow(() => asset.ApplyTo(target));
            Assert.AreEqual(0f, target.speed);
        }

        #endregion
    }
}
