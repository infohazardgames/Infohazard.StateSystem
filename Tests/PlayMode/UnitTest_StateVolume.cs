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

using System.Collections.Generic;
using System.Threading.Tasks;
using Infohazard.Core;
using NUnit.Framework;
using UnityEngine;

namespace Infohazard.StateSystem.Tests.PlayMode {
    /// <summary>
    /// <see cref="StateVolume"/> with real physics triggers. Occupants are tagged Player, which the
    /// <see cref="TriggerVolume"/> tag filter accepts by default.
    /// </summary>
    public class UnitTest_StateVolume : StateSystemTestFixture {
        private const string State = "A";
        private static readonly Vector3 Outside = new Vector3(50, 0, 0);

        private StateVolume CreateVolume(string name, Vector3 position, float size = 2) {
            GameObject obj = CreateGameObject(name, false);
            obj.transform.position = position;
            var box = obj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * size;
            var trigger = obj.AddComponent<TriggerVolume>();
            var volume = obj.AddComponent<StateVolume>();
            SetField(volume, "_stateName", State);
            SetField(volume, "_triggerVolume", trigger);
            obj.SetActive(true);
            return volume;
        }

        private static void AddBody(GameObject obj, float size = 0.5f) {
            obj.tag = "Player";
            obj.AddComponent<BoxCollider>().size = Vector3.one * size;
            obj.AddComponent<Rigidbody>().isKinematic = true;
        }

        /// <summary>
        /// An occupant with its collider on the same object as its <see cref="StateManager"/>.
        /// </summary>
        private StateManager CreateOccupant(Vector3 position, out TestFieldsStateBehaviour behaviour) {
            StateManager manager = CreateManager("Occupant", out behaviour);
            manager.transform.position = position;
            AddBody(manager.gameObject);
            return manager;
        }

        private static async Task WaitForPhysics() {
            await Awaitable.FixedUpdateAsync();
            await Awaitable.FixedUpdateAsync();
            await Awaitable.NextFrameAsync();
        }

        private static Dictionary<StateManager, int> Counts(StateVolume volume) =>
            GetField<Dictionary<StateManager, int>>(volume, "_occupantActiveCount");

        [Test]
        public async Task EnterAndExit_TurnStateOnAndOff() {
            CreateVolume("Volume", Vector3.zero);
            StateManager occupant = CreateOccupant(Outside, out TestFieldsStateBehaviour behaviour);
            await WaitForPhysics();
            Assert.IsFalse(behaviour.IsStateActive(State));

            occupant.transform.position = Vector3.zero;
            await WaitForPhysics();
            Assert.IsTrue(behaviour.IsStateActive(State));

            occupant.transform.position = Outside;
            await WaitForPhysics();
            Assert.IsFalse(behaviour.IsStateActive(State));
        }

        [Test]
        public async Task ObjectWithoutStateManager_IsIgnored() {
            CreateVolume("Volume", Vector3.zero);
            GameObject other = CreateGameObject("Other");
            AddBody(other);
            await WaitForPhysics();

            other.transform.position = Outside;
            await WaitForPhysics();
        }

        [Test]
        public async Task ColliderOnChildOfManager_IsDetected() {
            // Suspected bug #17: only the collider's own object was checked for a StateManager.
            CreateVolume("Volume", Vector3.zero);
            StateManager occupant = CreateManager("Occupant", out TestFieldsStateBehaviour behaviour);
            occupant.transform.position = Outside;
            AddBody(CreateGameObject("Body", true, occupant.transform));
            await WaitForPhysics();

            occupant.transform.position = Vector3.zero;
            await WaitForPhysics();

            Assert.IsTrue(behaviour.IsStateActive(State));
        }

        [Test]
        public async Task TwoBodiesOfOneManager_StateStaysOnUntilBothLeave() {
            // Suspected bug #17 (needed for this case): the per-manager count only matters with a parent lookup.
            CreateVolume("Volume", Vector3.zero, 4);
            StateManager occupant = CreateManager("Occupant", out TestFieldsStateBehaviour behaviour);
            GameObject first = CreateGameObject("First", true, occupant.transform);
            GameObject second = CreateGameObject("Second", true, occupant.transform);
            AddBody(first);
            AddBody(second);
            first.transform.localPosition = new Vector3(-1, 0, 0);
            second.transform.localPosition = new Vector3(1, 0, 0);
            await WaitForPhysics();
            Assert.IsTrue(behaviour.IsStateActive(State));

            first.transform.position = Outside;
            await WaitForPhysics();
            Assert.IsTrue(behaviour.IsStateActive(State), "The second body is still inside.");

            second.transform.position = Outside + Vector3.up * 5;
            await WaitForPhysics();
            Assert.IsFalse(behaviour.IsStateActive(State));
        }

        [Test]
        public async Task DisablingAndEnablingVolume_TurnsStateOffAndOn() {
            StateVolume volume = CreateVolume("Volume", Vector3.zero);
            CreateOccupant(Vector3.zero, out TestFieldsStateBehaviour behaviour);
            await WaitForPhysics();
            Assert.IsTrue(behaviour.IsStateActive(State));

            volume.enabled = false;
            Assert.IsFalse(behaviour.IsStateActive(State));

            volume.enabled = true;
            Assert.IsTrue(behaviour.IsStateActive(State), "Occupants already inside count on enable.");
        }

        [Test]
        public async Task OccupantDeactivatedInside_TurnsStateOff() {
            CreateVolume("Volume", Vector3.zero);
            StateManager occupant = CreateOccupant(Vector3.zero, out TestFieldsStateBehaviour behaviour);
            await WaitForPhysics();
            Assert.IsTrue(behaviour.IsStateActive(State));

            occupant.gameObject.SetActive(false);
            await WaitForPhysics();

            Assert.IsFalse(behaviour.IsStateActive(State));
        }

        [Test]
        public async Task OccupantDestroyedInside_IsForgotten() {
            StateVolume volume = CreateVolume("Volume", Vector3.zero);
            StateManager occupant = CreateOccupant(Vector3.zero, out _);
            await WaitForPhysics();
            Assert.AreEqual(1, Counts(volume).Count);

            Object.Destroy(occupant.gameObject);
            await WaitForPhysics();

            CollectionAssert.IsEmpty(Counts(volume), "A destroyed occupant shouldn't stay in the counts.");
        }

        [Test]
        public async Task OverlappingVolumes_LeavingOne_KeepsState() {
            // Suspected bug #16: leaving either volume turned the state off.
            CreateVolume("Left", Vector3.zero);
            CreateVolume("Right", new Vector3(1.5f, 0, 0));
            StateManager occupant = CreateOccupant(new Vector3(0.75f, 0, 0), out TestFieldsStateBehaviour behaviour);
            await WaitForPhysics();
            Assert.IsTrue(behaviour.IsStateActive(State));

            occupant.transform.position = new Vector3(2, 0, 0);
            await WaitForPhysics();
            Assert.IsTrue(behaviour.IsStateActive(State), "Still inside the right volume.");

            occupant.transform.position = Outside;
            await WaitForPhysics();
            Assert.IsFalse(behaviour.IsStateActive(State));
        }

        [Test]
        public async Task EnterAndExit_DoNotLog() {
            // Suspected bug #18: every enter and exit was logged.
            var messages = new List<string>();
            void OnLog(string message, string stackTrace, LogType type) => messages.Add(message);

            CreateVolume("Volume", Vector3.zero);
            StateManager occupant = CreateOccupant(Outside, out _);
            await WaitForPhysics();

            Application.logMessageReceived += OnLog;
            try {
                occupant.transform.position = Vector3.zero;
                await WaitForPhysics();
                occupant.transform.position = Outside;
                await WaitForPhysics();
            } finally {
                Application.logMessageReceived -= OnLog;
            }

            CollectionAssert.IsEmpty(messages);
        }
    }
}
