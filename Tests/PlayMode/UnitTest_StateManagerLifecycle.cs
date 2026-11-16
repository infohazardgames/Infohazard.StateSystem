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

using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Infohazard.StateSystem.Tests.PlayMode {
    /// <summary>
    /// <see cref="StateManager"/> with Unity's real lifecycle order: Awake on activation, Start on the next frame,
    /// and OnDestroy at the end of the frame.
    /// </summary>
    public class UnitTest_StateManagerLifecycle : StateSystemTestFixture {
        [Test]
        public async Task SerializedParent_IsHookedUpOnStart() {
            StateManager parent = CreateManager("Parent", out _);
            parent.SetStateActive("A", true);

            GameObject childObject = CreateGameObject("Child", false);
            TestFieldsStateBehaviour childBehaviour = AddManagedBehaviour(childObject.transform);
            var child = childObject.AddComponent<StateManager>();
            SetField(child, "_parent", parent);
            childObject.SetActive(true);

            await Awaitable.NextFrameAsync();

            CollectionAssert.AreEqual(new[] { child }, GetChildren(parent));
            Assert.IsTrue(childBehaviour.IsStateActive("A"));

            parent.SetStateActive("B", true);
            Assert.IsTrue(childBehaviour.IsStateActive("B"));
        }

        [Test]
        public async Task ParentSetBeforeStart_RegistersOnce() {
            // Suspected bug #10: Start re-added the manager to its parent's children.
            StateManager parent = CreateManager("Parent", out _);
            StateManager child = CreateManager("Child", out _);
            child.Parent = parent;

            await Awaitable.NextFrameAsync();

            CollectionAssert.AreEqual(new[] { child }, GetChildren(parent));
        }

        [Test]
        public async Task ParentSetBeforeStart_ThenDestroyed_LeavesNoChild() {
            // Suspected bug #10: the duplicate entry outlived the destroyed child.
            StateManager parent = CreateManager("Parent", out _);
            StateManager child = CreateManager("Child", out _);
            child.Parent = parent;
            await Awaitable.NextFrameAsync();

            Object.Destroy(child.gameObject);
            await Awaitable.NextFrameAsync();

            CollectionAssert.IsEmpty(GetChildren(parent));
            Assert.DoesNotThrow(() => parent.SetStateActive("A", true));
        }

        [Test]
        public async Task DestroyingParent_UnparentsChildren() {
            StateManager parent = CreateManager("Parent", out _);
            StateManager child = CreateManager("Child", out TestFieldsStateBehaviour childBehaviour);
            await Awaitable.NextFrameAsync();
            child.Parent = parent;
            parent.SetStateActive("A", true);
            Assert.IsTrue(childBehaviour.IsStateActive("A"));

            Object.Destroy(parent.gameObject);
            await Awaitable.NextFrameAsync();

            Assert.IsTrue(child.Parent == null);
            Assert.IsFalse(childBehaviour.IsStateActive("A"), "#12: the destroyed parent's states are turned off.");
        }

        [Test]
        public async Task ChildObjectOfAnotherManager_IsDrivenByBothWithoutErrors() {
            // A held item's behaviours are transform children of the character, and the item may have its own
            // manager parented to the character's. Both reach the same behaviour; that should be harmless.
            StateManager character = CreateManager("Character", out _);
            StateManager item = CreateManager("Item", out TestFieldsStateBehaviour itemBehaviour, character.transform);
            item.Parent = character;
            character.RefreshStateBehaviours();
            await Awaitable.NextFrameAsync();

            character.SetStateActive("A", true);
            Assert.IsTrue(itemBehaviour.IsStateActive("A"));
            Assert.AreEqual(1f, itemBehaviour.fieldA);

            character.SetStateActive("A", false);
            Assert.IsFalse(itemBehaviour.IsStateActive("A"));
            Assert.AreEqual(0f, itemBehaviour.fieldA);
        }
    }
}
