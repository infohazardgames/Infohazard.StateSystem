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
using Infohazard.StateSystem.Tests.PlayMode;
using NUnit.Framework;
using UnityEngine;

namespace Infohazard.StateSystem.Tests.Editor {
    /// <summary>
    /// Checks the shared test helpers themselves, so later failures point at the package rather than the fixture.
    /// </summary>
    public class UnitTest_TestInfrastructure : StateSystemTestFixture {
        [Test]
        public void CreateOverride_SetsTypeAndValues() {
            ComponentOverride asset = CreateOverride<TestStateBehaviour>(("floatValue", 2.5f),
                                                                        ("intValue", 7),
                                                                        ("ulongValue", ulong.MaxValue),
                                                                        ("enumValue", TestEnum.C));

            Assert.AreEqual(typeof(TestStateBehaviour), asset.Type);
            Assert.AreEqual(4, asset.Overrides.Length);
            foreach (SerializedValueOverride value in asset.Overrides) {
                Assert.IsTrue(value.Valid, value.ToString());
            }

            Assert.AreEqual(2.5f, Array.Find(asset.Overrides, o => o.Path == "floatValue").Value);
            Assert.AreEqual(7, Array.Find(asset.Overrides, o => o.Path == "intValue").Value);
            Assert.AreEqual(ulong.MaxValue, Array.Find(asset.Overrides, o => o.Path == "ulongValue").Value);
            Assert.AreEqual(TestEnum.C, Array.Find(asset.Overrides, o => o.Path == "enumValue").Value);
        }

        [Test]
        public void AddOverride_InvalidPath_FailsTheTest() {
            ComponentOverride asset = CreateOverride<TestStateBehaviour>();
            Assert.Throws<AssertionException>(() => AddOverride(asset, "notAField"));
        }

        [Test]
        public void SetStoredPath_StoresPathWithoutValidating() {
            ComponentOverride asset = CreateOverride<TestStateBehaviour>(("floatValue", 1f));
            SerializedValueOverride value = asset.Overrides[0];

            SetStoredPath(asset, value, "notAField");

            Assert.AreEqual("notAField", value.Path);
            Assert.IsFalse(value.Valid);
        }

        [Test]
        public void SetStates_BuildsStatesWithNamesMasksAndFlags() {
            ComponentOverride values = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 1f));
            TestFieldsStateBehaviour behaviour = CreateStateBehaviour<TestFieldsStateBehaviour>(
                new StateSpec("A", values),
                new StateSpec("B", null) { Active = true, BlockedBy = { "A" } },
                new StateSpec("C", null) { BlockedBy = { "A", "B" }, ExtraBlockedByMask = 1 << 5 });

            State[] states = GetField<State[]>(GetField<StateList>(behaviour, "_states"), "_states");

            Assert.AreEqual(3, states.Length);
            Assert.AreEqual("A", states[0].Name);
            Assert.AreSame(values, states[0].Values);
            Assert.AreEqual(0, states[0].BlockedBy);
            Assert.IsFalse(states[0].IsActive);
            Assert.AreEqual(0b1, states[1].BlockedBy);
            Assert.IsTrue(states[1].IsActive);
            Assert.AreEqual(0b100011, states[2].BlockedBy);
            Assert.IsTrue(behaviour.HasState("C"));
        }

        [Test]
        public void TestComponents_CanBeAddedToGameObjects() {
            // Unity can't add components defined in an Editor-only assembly, which is why these live in PlayMode.
            GameObject obj = CreateGameObject("Components");
            Assert.IsNotNull(obj.AddComponent<TestStateBehaviour>());
            Assert.IsNotNull(obj.AddComponent<TestFieldsStateBehaviour>());
            Assert.IsNotNull(obj.AddComponent<TestDerivedStateBehaviour>());
            Assert.IsNotNull(obj.AddComponent<TestExteriorTarget>());
        }

        [Test]
        public void InvokeLifecycle_Awake_InitializesStates() {
            ComponentOverride values = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 3f));
            TestFieldsStateBehaviour behaviour =
                CreateStateBehaviour<TestFieldsStateBehaviour>(new StateSpec("On", values) { Active = true });
            Assert.AreEqual(0f, behaviour.fieldA, "Edit mode shouldn't have run Awake yet.");

            InvokeLifecycleInEditMode(behaviour);

            Assert.AreEqual(3f, behaviour.fieldA);
            Assert.IsTrue(behaviour.IsStateActive("On"));
        }

        [Test]
        public void AddExteriorStateBehaviour_OverridesTarget() {
            TestExteriorTarget target = CreateGameObject("Target").AddComponent<TestExteriorTarget>();
            ComponentOverride values = CreateOverride<TestExteriorTarget>(("speed", 4f));
            ExteriorStateBehaviour exterior =
                AddExteriorStateBehaviour(target, new StateSpec("Fast", values) { Active = true });

            InvokeLifecycleInEditMode(exterior);

            Assert.AreEqual(4f, target.speed);
            Assert.AreEqual(typeof(TestExteriorTarget), exterior.Type);
        }

        [Test]
        public void InvokeLifecycle_MissingMethod_Throws() {
            TestExteriorTarget target = CreateGameObject("Target").AddComponent<TestExteriorTarget>();
            Assert.Throws<ArgumentException>(() => InvokeLifecycle(target, "Awake"));
        }

        [Test]
        public void DestroyTracked_DestroysCreatedObjects() {
            GameObject obj = CreateGameObject("Tracked");
            ComponentOverride asset = CreateOverride<TestFieldsStateBehaviour>();

            DestroyTracked();

            Assert.IsTrue(obj == null);
            Assert.IsTrue(asset == null);
        }
    }
}
