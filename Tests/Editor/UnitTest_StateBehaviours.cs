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

using System.Text.RegularExpressions;
using Infohazard.StateSystem.Tests.PlayMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infohazard.StateSystem.Tests.Editor {
    /// <summary>
    /// The components that own a state list: <see cref="StateBehaviour"/> (overrides itself) and
    /// <see cref="ExteriorStateBehaviour"/> (overrides another component).
    /// </summary>
    public class UnitTest_StateBehaviours : StateSystemTestFixture {
        #region StateBehaviour

        private TestStateBehaviour CreateBehaviour(bool initiallyActive = false) {
            ComponentOverride values = CreateOverride<TestStateBehaviour>(("floatValue", 5f));
            var behaviour = CreateStateBehaviour<TestStateBehaviour>(
                new StateSpec("A", values) { Active = initiallyActive },
                new StateSpec("Empty", null));
            behaviour.floatValue = 2;
            return behaviour;
        }

        [Test]
        public void Awake_AppliesInitiallyActiveStates() {
            TestStateBehaviour behaviour = CreateBehaviour(true);

            InvokeLifecycleInEditMode(behaviour);

            Assert.AreEqual(5f, behaviour.floatValue);
            Assert.IsTrue(behaviour.IsStateActive("A"));
        }

        [Test]
        public void Awake_InitializeDefaultValuesRunsBeforeStatesApply() {
            TestStateBehaviour behaviour = CreateBehaviour(true);

            InvokeLifecycleInEditMode(behaviour);

            Assert.AreEqual(1, behaviour.InitializeDefaultValuesCount);
            Assert.AreEqual(2f, behaviour.FloatValueAtInitializeDefaults);
        }

        [Test]
        public void SetStateActive_BeforeAwake_InitializesOnce() {
            TestStateBehaviour behaviour = CreateBehaviour();

            behaviour.SetStateActive("A", true);
            Assert.AreEqual(5f, behaviour.floatValue);
            Assert.AreEqual(1, behaviour.InitializeDefaultValuesCount);

            InvokeLifecycleInEditMode(behaviour);

            Assert.AreEqual(1, behaviour.InitializeDefaultValuesCount, "Awake shouldn't initialize again.");
            Assert.IsTrue(behaviour.IsStateActive("A"), "Awake shouldn't reset the state.");
            Assert.AreEqual(5f, behaviour.floatValue);

            behaviour.SetStateActive("A", false);
            Assert.AreEqual(2f, behaviour.floatValue);
        }

        [Test]
        public void IsStateActiveAndHasState_BeforeInitializing_ReportSerializedStates() {
            TestStateBehaviour behaviour = CreateBehaviour(true);

            Assert.IsTrue(behaviour.HasState("A"));
            Assert.IsTrue(behaviour.HasState("Empty"));
            Assert.IsFalse(behaviour.HasState("Missing"));
            Assert.IsTrue(behaviour.IsStateActive("A"));
            Assert.IsFalse(behaviour.IsStateActive("Empty"));
            Assert.AreEqual(0, behaviour.InitializeDefaultValuesCount, "Querying shouldn't initialize.");
        }

        [Test]
        public void Type_IsConcreteType() {
            Assert.AreEqual(typeof(TestStateBehaviour), CreateBehaviour().Type);
        }

        [Test]
        public void OverrideForBaseType_AppliesToDerivedBehaviour() {
            ComponentOverride values = CreateOverride<TestStateBehaviourBase>(("_baseSerialized", 4f));
            var behaviour = CreateStateBehaviour<TestDerivedStateBehaviour>(new StateSpec("A", values));
            InvokeLifecycleInEditMode(behaviour);

            behaviour.SetStateActive("A", true);
            Assert.AreEqual(4f, behaviour.BaseSerialized);

            behaviour.SetStateActive("A", false);
            Assert.AreEqual(0f, behaviour.BaseSerialized);
        }

        [Test]
        public void OverrideForOtherType_LogsErrorAndIsSkipped() {
            ComponentOverride values = CreateOverride<TestExteriorTarget>(("speed", 4f));
            var behaviour = CreateStateBehaviour<TestFieldsStateBehaviour>(new StateSpec("A", values));

            LogAssert.Expect(LogType.Error, new Regex(nameof(TestExteriorTarget)));
            Assert.DoesNotThrow(() => InvokeLifecycleInEditMode(behaviour));
            Assert.DoesNotThrow(() => behaviour.SetStateActive("A", true));
            Assert.IsTrue(behaviour.IsStateActive("A"));
        }

        [Test]
        public void Clone_OfInitializedBehaviour_KeepsOverriddenValues() {
            // Known limitation (#23, documented on StateList): a clone of a live object captures the overridden
            // values as its defaults. This checks today's behaviour, so a change to it is noticed.
            // TestFieldsStateBehaviour rather than TestStateBehaviour: Unity logs an error when it serializes the
            // latter's long-backed enum field during Instantiate.
            ComponentOverride values = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 5f));
            var original = CreateStateBehaviour<TestFieldsStateBehaviour>(new StateSpec("A", values));
            original.fieldA = 2;
            InvokeLifecycleInEditMode(original);
            original.SetStateActive("A", true);

            GameObject cloneObject = Track(Object.Instantiate(original.gameObject));
            var clone = cloneObject.GetComponent<TestFieldsStateBehaviour>();
            InvokeLifecycleInEditMode(clone);
            Assert.IsTrue(clone.IsStateActive("A"));
            Assert.AreEqual(5f, clone.fieldA);

            clone.SetStateActive("A", false);

            Assert.AreEqual(5f, clone.fieldA, "The clone can't restore the original value.");

            original.SetStateActive("A", false);
            Assert.AreEqual(2f, original.fieldA, "The original is unaffected.");
        }

        #endregion

        #region ExteriorStateBehaviour

        private TestExteriorTarget CreateTarget() {
            TestExteriorTarget target = CreateGameObject("Target").AddComponent<TestExteriorTarget>();
            target.speed = 1;
            target.label = "default";
            return target;
        }

        [Test]
        public void Exterior_OverridesTargetComponent() {
            TestExteriorTarget target = CreateTarget();
            ComponentOverride values = CreateOverride<TestExteriorTarget>(("speed", 3f), ("label", "fast"));
            ExteriorStateBehaviour exterior = AddExteriorStateBehaviour(target, new StateSpec("Fast", values));
            InvokeLifecycleInEditMode(exterior);

            exterior.SetStateActive("Fast", true);
            Assert.AreEqual(3f, target.speed);
            Assert.AreEqual("fast", target.label);

            exterior.SetStateActive("Fast", false);
            Assert.AreEqual(1f, target.speed);
            Assert.AreEqual("default", target.label);
        }

        [Test]
        public void Exterior_SetStateActiveBeforeAwake_Initializes() {
            TestExteriorTarget target = CreateTarget();
            ComponentOverride values = CreateOverride<TestExteriorTarget>(("speed", 3f));
            ExteriorStateBehaviour exterior = AddExteriorStateBehaviour(target, new StateSpec("Fast", values));

            exterior.SetStateActive("Fast", true);
            InvokeLifecycleInEditMode(exterior);

            Assert.IsTrue(exterior.IsStateActive("Fast"));
            Assert.AreEqual(3f, target.speed);
            exterior.SetStateActive("Fast", false);
            Assert.AreEqual(1f, target.speed);
        }

        [Test]
        public void Exterior_Type_FollowsComponent() {
            TestExteriorTarget target = CreateTarget();
            ExteriorStateBehaviour exterior = AddExteriorStateBehaviour(target);
            Assert.AreEqual(typeof(TestExteriorTarget), exterior.Type);

            SetField(exterior, "_component", null);
            Assert.AreEqual(typeof(Component), exterior.Type);
        }

        [Test]
        public void Exterior_NoComponent_LogsErrorAndDoesNotThrow() {
            // Suspected bug #1: with no component, applying a state threw.
            TestExteriorTarget target = CreateTarget();
            ComponentOverride values = CreateOverride<TestExteriorTarget>(("speed", 3f));
            ExteriorStateBehaviour exterior = AddExteriorStateBehaviour(target, new StateSpec("Fast", values));
            SetField(exterior, "_component", null);

            LogAssert.Expect(LogType.Error, new Regex("no object"));
            Assert.DoesNotThrow(() => InvokeLifecycleInEditMode(exterior));
            Assert.DoesNotThrow(() => exterior.SetStateActive("Fast", true));
            Assert.IsTrue(exterior.IsStateActive("Fast"));
            Assert.AreEqual(1f, target.speed);
        }

        [Test]
        public void Exterior_OverrideForOtherType_LogsErrorAndIsSkipped() {
            TestExteriorTarget target = CreateTarget();
            ComponentOverride values = CreateOverride<TestFieldsStateBehaviour>(("fieldA", 3f));
            ExteriorStateBehaviour exterior = AddExteriorStateBehaviour(target, new StateSpec("A", values));

            LogAssert.Expect(LogType.Error, new Regex(nameof(TestFieldsStateBehaviour)));
            Assert.DoesNotThrow(() => InvokeLifecycleInEditMode(exterior));
            Assert.DoesNotThrow(() => exterior.SetStateActive("A", true));
        }

        #endregion
    }
}
