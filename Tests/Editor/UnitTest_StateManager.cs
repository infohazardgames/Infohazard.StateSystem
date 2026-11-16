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

using Infohazard.StateSystem.Tests.PlayMode;
using NUnit.Framework;
using UnityEngine;

namespace Infohazard.StateSystem.Tests.Editor {
    /// <summary>
    /// Broadcasting states to child behaviours, and parent/child managers. Lifecycle methods are called by hand;
    /// UnitTest_StateManagerLifecycle (PlayMode) covers Unity's real call order.
    /// </summary>
    public class UnitTest_StateManager : StateSystemTestFixture {
        /// <summary>
        /// Destroy a manager's object as Unity would in play mode: OnDestroy (which edit mode skips), then destroy.
        /// </summary>
        private static void DestroyManager(StateManager manager) {
            InvokeLifecycle(manager, "OnDestroy");
            Object.DestroyImmediate(manager.gameObject);
        }

        #region Broadcasting

        [Test]
        public void SetStateActive_BroadcastsToChildBehaviours() {
            TestFieldsStateBehaviour first = null, inactive = null;
            StateManager manager = CreateManager("Manager", null, t => {
                first = AddManagedBehaviour(t, "First");
                inactive = AddManagedBehaviour(t, "Inactive", false);
            });

            manager.SetStateActive("A", true);

            Assert.IsTrue(first.IsStateActive("A"));
            Assert.AreEqual(1f, first.fieldA);
            Assert.IsTrue(inactive.IsStateActive("A"), "Inactive children are included.");

            manager.SetStateActive("A", false);
            Assert.IsFalse(first.IsStateActive("A"));
            Assert.AreEqual(0f, first.fieldA);
        }

        [Test]
        public void SetStateActive_ReturnsWhetherValueChanged() {
            StateManager manager = CreateManager("Manager", out _);

            Assert.IsTrue(manager.SetStateActive("A", true));
            Assert.IsFalse(manager.SetStateActive("A", true));
            Assert.IsTrue(manager.SetStateActive("A", false));
            Assert.IsFalse(manager.SetStateActive("A", false));
        }

        [Test]
        public void SetStateActive_UnknownState_IsHarmless() {
            StateManager manager = CreateManager("Manager", out TestFieldsStateBehaviour behaviour);

            Assert.DoesNotThrow(() => manager.SetStateActive("Missing", true));
            Assert.IsFalse(behaviour.IsStateActive("A"));
        }

        [Test]
        public void SetStateActive_BeforeAwake_InitializesAndBroadcasts() {
            GameObject obj = CreateGameObject("Manager");
            TestFieldsStateBehaviour behaviour = AddManagedBehaviour(obj.transform);
            var manager = obj.AddComponent<StateManager>();

            manager.SetStateActive("A", true);

            Assert.IsTrue(behaviour.IsStateActive("A"));
        }

        [Test]
        public void IsStateActiveOnAny_TrueIfAnyBehaviourHasItActive() {
            TestFieldsStateBehaviour first = null;
            StateManager manager = CreateManager("Manager", null, t => {
                first = AddManagedBehaviour(t, "First");
                AddManagedBehaviour(t, "Second");
            });
            Assert.IsFalse(manager.IsStateActiveOnAny("A"));

            manager.SetStateActive("A", true);
            first.SetStateActive("A", false);

            Assert.IsTrue(manager.IsStateActiveOnAny("A"));
            Assert.IsFalse(manager.IsStateActiveOnAll("A"));
        }

        [Test]
        public void IsStateActiveOnAll_IgnoresBehavioursWithoutTheState() {
            // Suspected bug #20: a behaviour that doesn't define the state made this false.
            StateManager manager = CreateManager("Manager", null, t => {
                AddManagedBehaviour(t, "HasA");
                AddManagedBehaviour(t, "NoA", true, "Other");
            });

            manager.SetStateActive("A", true);

            Assert.IsTrue(manager.IsStateActiveOnAll("A"));
        }

        [Test]
        public void IsStateActiveOnAll_NoBehaviourHasTheState_IsFalse() {
            // Suspected bug #20: with no behaviours, All() was vacuously true.
            StateManager empty = CreateManager("Empty");
            StateManager other = CreateManager("Other", null, t => AddManagedBehaviour(t, "NoA", true, "Other"));

            Assert.IsFalse(empty.IsStateActiveOnAll("A"));
            Assert.IsFalse(other.IsStateActiveOnAll("A"));
        }

        [Test]
        public void ForgetState_NextSetCountsAsAChange() {
            StateManager manager = CreateManager("Manager", out TestFieldsStateBehaviour behaviour);
            manager.SetStateActive("A", true);

            manager.ForgetState("A");

            Assert.IsTrue(behaviour.IsStateActive("A"), "Forgetting doesn't change the behaviours.");
            Assert.IsTrue(manager.SetStateActive("A", true));
        }

        [Test]
        public void ForgetState_IsNotAppliedToBehavioursFoundLater() {
            StateManager manager = CreateManager("Manager", out _);
            manager.SetStateActive("A", true);
            manager.ForgetState("A");

            TestFieldsStateBehaviour later = AddManagedBehaviour(manager.transform, "Later");
            manager.RefreshStateBehaviours();

            Assert.IsFalse(later.IsStateActive("A"));
        }

        [Test]
        public void RefreshStateBehaviours_AppliesCurrentStatesToNewBehaviours() {
            StateManager manager = CreateManager("Manager", out _);
            manager.SetStateActive("A", true);
            manager.SetStateActive("B", false);

            TestFieldsStateBehaviour later = AddManagedBehaviour(manager.transform, "Later");
            Assert.IsFalse(later.IsStateActive("A"), "Not found yet.");

            manager.RefreshStateBehaviours();

            Assert.IsTrue(later.IsStateActive("A"));
            Assert.IsFalse(later.IsStateActive("B"));
        }

        [Test]
        public void RefreshStateBehaviours_RemovedBehaviour_HasStatesTurnedOff() {
            // #12 (decided): a behaviour no longer in the hierarchy shouldn't keep the manager's states.
            StateManager manager = CreateManager("Manager", out TestFieldsStateBehaviour behaviour);
            manager.SetStateActive("A", true);

            behaviour.transform.SetParent(null);
            manager.RefreshStateBehaviours();

            Assert.IsFalse(behaviour.IsStateActive("A"));
            Assert.AreEqual(0f, behaviour.fieldA);
        }

        #endregion

        #region Parenting

        [Test]
        public void Child_FollowsParentStates() {
            StateManager parent = CreateManager("Parent", out TestFieldsStateBehaviour parentBehaviour);
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);
            child.Parent = parent;

            parent.SetStateActive("A", true);

            Assert.IsTrue(parentBehaviour.IsStateActive("A"));
            Assert.IsTrue(childBehaviour.IsStateActive("A"));
        }

        [Test]
        public void SettingParent_AppliesParentsCurrentStates() {
            StateManager parent = CreateManager("Parent", out _);
            parent.SetStateActive("A", true);
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);

            child.Parent = parent;

            Assert.IsTrue(childBehaviour.IsStateActive("A"));
            CollectionAssert.AreEqual(new[] { child }, GetChildren(parent));
        }

        [Test]
        public void Child_OwnStatesIgnoredWhileParented_AppliedWhenUnparented() {
            StateManager parent = CreateManager("Parent", out _);
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);
            child.Parent = parent;

            Assert.IsTrue(child.SetStateActive("C", true));
            Assert.IsFalse(childBehaviour.IsStateActive("C"), "The parent drives the child.");

            child.Parent = null;

            Assert.IsTrue(childBehaviour.IsStateActive("C"));
            CollectionAssert.IsEmpty(GetChildren(parent));
        }

        [Test]
        public void Reparenting_AppliesNewParentsStatesAndTurnsOffOldOnes() {
            // #12 (decided): states from the old parent that the new parent doesn't have are turned off.
            StateManager first = CreateManager("First", out _);
            first.SetStateActive("A", true);
            first.SetStateActive("B", true);
            StateManager second = CreateManager("Second", out _);
            second.SetStateActive("A", true);
            second.SetStateActive("C", true);
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);
            child.Parent = first;

            child.Parent = second;

            Assert.IsTrue(childBehaviour.IsStateActive("A"));
            Assert.IsFalse(childBehaviour.IsStateActive("B"), "Only the old parent had B.");
            Assert.IsTrue(childBehaviour.IsStateActive("C"));
            CollectionAssert.IsEmpty(GetChildren(first));
            CollectionAssert.AreEqual(new[] { child }, GetChildren(second));
        }

        [Test]
        public void Unparenting_TurnsOffParentStatesTheChildDoesNotHave() {
            // #12 (decided).
            StateManager parent = CreateManager("Parent", out _);
            parent.SetStateActive("A", true);
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);
            child.Parent = parent;
            child.SetStateActive("C", true);

            child.Parent = null;

            Assert.IsFalse(childBehaviour.IsStateActive("A"));
            Assert.IsTrue(childBehaviour.IsStateActive("C"));
        }

        [Test]
        public void ThreeLevels_GrandparentStatesReachGrandchild() {
            StateManager grandparent = CreateManager("Grandparent", out _);
            StateManager parent = CreateManager("Parent", out _);
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);
            parent.Parent = grandparent;
            child.Parent = parent;

            grandparent.SetStateActive("A", true);

            Assert.IsTrue(childBehaviour.IsStateActive("A"));
        }

        [Test]
        public void ThreeLevels_AttachingGrandchild_AppliesGrandparentStates() {
            // Suspected bug #9: the grandchild got the parent's own (ignored) dictionary instead.
            StateManager grandparent = CreateManager("Grandparent", out _);
            StateManager parent = CreateManager("Parent", out _);
            parent.Parent = grandparent;
            grandparent.SetStateActive("A", true);
            parent.SetStateActive("B", true); // Ignored, because the parent has a parent.
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);

            child.Parent = parent;

            Assert.IsTrue(childBehaviour.IsStateActive("A"), "The grandparent's state should reach the grandchild.");
            Assert.IsFalse(childBehaviour.IsStateActive("B"), "The parent's own states are ignored while parented.");
        }

        [Test]
        public void ThreeLevels_RefreshingMiddle_KeepsGrandparentStates() {
            // Suspected bug #9.
            StateManager grandparent = CreateManager("Grandparent", out _);
            StateManager parent = CreateManager("Parent", out _);
            parent.Parent = grandparent;
            grandparent.SetStateActive("A", true);

            TestFieldsStateBehaviour later = AddManagedBehaviour(parent.transform, "Later");
            parent.RefreshStateBehaviours();

            Assert.IsTrue(later.IsStateActive("A"));
        }

        [Test]
        public void ParentSetInCode_ThenStart_RegistersOnce() {
            // Suspected bug #10: Start re-added the manager to its parent's children.
            StateManager parent = CreateManager("Parent", out _);
            StateManager child = CreateManager("Child", out _);
            child.Parent = parent;

            InvokeLifecycle(child, "Start");

            CollectionAssert.AreEqual(new[] { child }, GetChildren(parent));
        }

        [Test]
        public void SerializedParent_IsHookedUpInStart() {
            StateManager parent = CreateManager("Parent", out _);
            parent.SetStateActive("A", true);
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);
            SetField(child, "_parent", parent);

            InvokeLifecycle(child, "Start");

            CollectionAssert.AreEqual(new[] { child }, GetChildren(parent));
            Assert.IsTrue(childBehaviour.IsStateActive("A"));
        }

        [Test]
        public void SettingParent_BeforeEitherAwake_DoesNotThrow() {
            // Suspected bug #11: _children and _behaviours were still null.
            GameObject parentObject = CreateGameObject("Parent");
            var parent = parentObject.AddComponent<StateManager>();
            GameObject childObject = CreateGameObject("Child");
            TestFieldsStateBehaviour childBehaviour = AddManagedBehaviour(childObject.transform);
            var child = childObject.AddComponent<StateManager>();

            Assert.DoesNotThrow(() => child.Parent = parent);
            parent.SetStateActive("A", true);

            Assert.IsTrue(childBehaviour.IsStateActive("A"));
        }

        [Test]
        public void DestroyingChild_RemovesItFromParent() {
            StateManager parent = CreateManager("Parent", out _);
            StateManager child = CreateManager("Child", out _);
            child.Parent = parent;

            DestroyManager(child);

            CollectionAssert.IsEmpty(GetChildren(parent));
            Assert.DoesNotThrow(() => parent.SetStateActive("A", true));
        }

        [Test]
        public void DestroyingParent_UnparentsChildren() {
            StateManager parent = CreateManager("Parent", out _);
            parent.SetStateActive("A", true);
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);
            child.Parent = parent;
            child.SetStateActive("C", true);

            DestroyManager(parent);

            Assert.IsTrue(child.Parent == null);
            Assert.IsTrue(childBehaviour.IsStateActive("C"), "The child's own states apply once unparented.");
            Assert.IsFalse(childBehaviour.IsStateActive("A"), "#12: the destroyed parent's states are turned off.");
        }

        #endregion
    }
}
