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
using System.Text;
using System.Text.RegularExpressions;
using Infohazard.StateSystem.Tests.PlayMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infohazard.StateSystem.Tests.Editor {
    /// <summary>
    /// How states apply and restore values, which state wins a shared field, and blocking.
    /// </summary>
    public class UnitTest_StateList : StateSystemTestFixture {
        private const float DefaultA = 7;
        private const float DefaultB = 8;

        private TestFieldsStateBehaviour _owner;

        [SetUp]
        public void SetUp() {
            _owner = CreateGameObject("Owner").AddComponent<TestFieldsStateBehaviour>();
            _owner.fieldA = DefaultA;
            _owner.fieldB = DefaultB;
        }

        /// <summary>
        /// An override of the owner's fields, by index (0 = fieldA).
        /// </summary>
        private ComponentOverride Ov(params (int field, float value)[] values) {
            return CreateOverride<TestFieldsStateBehaviour>(
                values.Select(v => (TestFieldsStateBehaviour.FieldNames[v.field], (object) v.value)).ToArray());
        }

        private StateList Init(params StateSpec[] specs) {
            StateList list = CreateStateList(specs);
            list.Initialize(_owner);
            return list;
        }

        #region Initialize

        [Test]
        public void Initialize_InitiallyActiveState_IsApplied() {
            StateList list = Init(new StateSpec("A", Ov((0, 1))) { Active = true });

            Assert.IsTrue(list.IsStateActive("A"));
            Assert.AreEqual(1f, _owner.fieldA);
        }

        [Test]
        public void Initialize_InitiallyInactiveState_IsNotApplied() {
            StateList list = Init(new StateSpec("A", Ov((0, 1))));

            Assert.IsFalse(list.IsStateActive("A"));
            Assert.AreEqual(DefaultA, _owner.fieldA);
        }

        [Test]
        public void Initialize_InitiallyActiveButBlocked_IsActiveButNotApplied() {
            StateList list = Init(new StateSpec("A", Ov((0, 1))) { Active = true, BlockedBy = { "B" } },
                                  new StateSpec("B", Ov((1, 2))) { Active = true });

            Assert.IsTrue(list.IsStateActive("A"));
            Assert.AreEqual(DefaultA, _owner.fieldA);
            Assert.AreEqual(2f, _owner.fieldB);
        }

        [Test]
        public void Initialize_NoStates_DoesNotThrow() {
            // Suspected bug #22: a StateList created without states threw in Initialize.
            var list = new StateList();
            Assert.DoesNotThrow(() => list.Initialize(_owner));
            Assert.IsFalse(list.HasState("A"));
            Assert.DoesNotThrow(() => list.SetStateActive("A", true));
        }

        [Test]
        public void Initialize_EmptyStates_DoesNotThrow() {
            Assert.DoesNotThrow(() => Init());
        }

        #endregion

        #region Basics

        [Test]
        public void Activate_AppliesAndDeactivate_Restores() {
            StateList list = Init(new StateSpec("A", Ov((0, 1), (1, 2))));

            list.SetStateActive("A", true);
            Assert.AreEqual(1f, _owner.fieldA);
            Assert.AreEqual(2f, _owner.fieldB);

            list.SetStateActive("A", false);
            Assert.AreEqual(DefaultA, _owner.fieldA);
            Assert.AreEqual(DefaultB, _owner.fieldB);
        }

        [Test]
        public void SetStateActive_SameValueTwice_IsIgnored() {
            StateList list = Init(new StateSpec("A", Ov((0, 1))));

            list.SetStateActive("A", true);
            list.SetStateActive("A", true);
            list.SetStateActive("A", false);
            Assert.AreEqual(DefaultA, _owner.fieldA);

            list.SetStateActive("A", false);
            Assert.AreEqual(DefaultA, _owner.fieldA);
        }

        [Test]
        public void SetStateActive_ByIndex_Works() {
            StateList list = Init(new StateSpec("A", null), new StateSpec("B", Ov((0, 1))));

            list.SetStateActive(1, true);

            Assert.IsTrue(list.IsStateActive(1));
            Assert.IsTrue(list.IsStateActive("B"));
            Assert.AreEqual(1f, _owner.fieldA);
        }

        [Test]
        public void UnknownState_IsIgnored() {
            StateList list = Init(new StateSpec("A", Ov((0, 1))));

            Assert.IsFalse(list.HasState("Missing"));
            Assert.IsFalse(list.IsStateActive("Missing"));
            Assert.DoesNotThrow(() => list.SetStateActive("Missing", true));
            Assert.AreEqual(DefaultA, _owner.fieldA);
        }

        [Test]
        public void StateWithNoValues_CanBeToggled() {
            StateList list = Init(new StateSpec("Empty", null), new StateSpec("A", Ov((0, 1))));

            Assert.IsTrue(list.HasState("Empty"));
            list.SetStateActive("Empty", true);
            Assert.IsTrue(list.IsStateActive("Empty"));
            list.SetStateActive("Empty", false);
            Assert.IsFalse(list.IsStateActive("Empty"));
        }

        [Test]
        public void StatesOnDifferentFields_DoNotInterfere() {
            StateList list = Init(new StateSpec("A", Ov((0, 1))), new StateSpec("B", Ov((1, 2))));

            list.SetStateActive("A", true);
            list.SetStateActive("B", true);
            list.SetStateActive("A", false);

            Assert.AreEqual(DefaultA, _owner.fieldA);
            Assert.AreEqual(2f, _owner.fieldB);
        }

        [Test]
        public void ValueChangedWhileUncontrolled_IsTheNewDefault() {
            StateList list = Init(new StateSpec("A", Ov((0, 1))));

            _owner.fieldA = 5;
            list.SetStateActive("A", true);
            list.SetStateActive("A", false);
            Assert.AreEqual(5f, _owner.fieldA);

            _owner.fieldA = 9;
            list.SetStateActive("A", true);
            list.SetStateActive("A", false);
            Assert.AreEqual(9f, _owner.fieldA);
        }

        #endregion

        #region Priority

        [TestCase("A", "B")]
        [TestCase("B", "A")]
        public void SharedField_LowerIndexWins(string first, string second) {
            StateList list = Init(new StateSpec("A", Ov((0, 1))), new StateSpec("B", Ov((0, 2))));

            list.SetStateActive(first, true);
            list.SetStateActive(second, true);

            Assert.AreEqual(1f, _owner.fieldA);
        }

        [TestCase("A", "B")]
        [TestCase("B", "A")]
        public void SharedField_DeactivatingWinner_AppliesNextState(string first, string second) {
            StateList list = Init(new StateSpec("A", Ov((0, 1))), new StateSpec("B", Ov((0, 2))));
            list.SetStateActive(first, true);
            list.SetStateActive(second, true);

            list.SetStateActive("A", false);

            Assert.AreEqual(2f, _owner.fieldA);
        }

        [TestCase("A", "B")]
        [TestCase("B", "A")]
        public void SharedField_DeactivatingLoser_KeepsWinner(string first, string second) {
            StateList list = Init(new StateSpec("A", Ov((0, 1))), new StateSpec("B", Ov((0, 2))));
            list.SetStateActive(first, true);
            list.SetStateActive(second, true);

            list.SetStateActive("B", false);

            Assert.AreEqual(1f, _owner.fieldA);
        }

        [TestCase("A", "B")]
        [TestCase("B", "A")]
        public void SharedField_DeactivatingAll_RestoresDefault(string first, string second) {
            StateList list = Init(new StateSpec("A", Ov((0, 1))), new StateSpec("B", Ov((0, 2))));
            list.SetStateActive("A", true);
            list.SetStateActive("B", true);

            list.SetStateActive(first, false);
            list.SetStateActive(second, false);

            Assert.AreEqual(DefaultA, _owner.fieldA);
        }

        [Test]
        public void SharedField_ThreeStates_MiddleRemoved_KeepsWinner() {
            StateList list = Init(new StateSpec("A", Ov((0, 1))),
                                  new StateSpec("B", Ov((0, 2))),
                                  new StateSpec("C", Ov((0, 3))));
            list.SetStateActive("C", true);
            list.SetStateActive("A", true);
            list.SetStateActive("B", true);

            list.SetStateActive("B", false);
            Assert.AreEqual(1f, _owner.fieldA);

            list.SetStateActive("A", false);
            Assert.AreEqual(3f, _owner.fieldA);

            list.SetStateActive("C", false);
            Assert.AreEqual(DefaultA, _owner.fieldA);
        }

        #endregion

        #region Blocking

        [Test]
        public void Blocking_BlockerActiveFirst_BlockedStateIsActiveButNotApplied() {
            StateList list = Init(new StateSpec("A", Ov((1, 1))),
                                  new StateSpec("B", Ov((0, 2))) { BlockedBy = { "A" } });

            list.SetStateActive("A", true);
            list.SetStateActive("B", true);

            Assert.IsTrue(list.IsStateActive("B"));
            Assert.AreEqual(DefaultA, _owner.fieldA);
        }

        [Test]
        public void Blocking_BlockerActivatedLater_UnappliesBlockedState() {
            StateList list = Init(new StateSpec("A", Ov((1, 1))),
                                  new StateSpec("B", Ov((0, 2))) { BlockedBy = { "A" } });

            list.SetStateActive("B", true);
            Assert.AreEqual(2f, _owner.fieldA);

            list.SetStateActive("A", true);
            Assert.AreEqual(DefaultA, _owner.fieldA);
            Assert.IsTrue(list.IsStateActive("B"));
        }

        [Test]
        public void Blocking_BlockerDeactivated_AppliesBlockedState() {
            StateList list = Init(new StateSpec("A", Ov((1, 1))),
                                  new StateSpec("B", Ov((0, 2))) { BlockedBy = { "A" } });
            list.SetStateActive("A", true);
            list.SetStateActive("B", true);

            list.SetStateActive("A", false);

            Assert.AreEqual(2f, _owner.fieldA);
        }

        [Test]
        public void Blocking_BlockedStateDeactivatedWhileBlocked_StaysUnapplied() {
            StateList list = Init(new StateSpec("A", Ov((1, 1))),
                                  new StateSpec("B", Ov((0, 2))) { BlockedBy = { "A" } });
            list.SetStateActive("A", true);
            list.SetStateActive("B", true);
            list.SetStateActive("B", false);

            list.SetStateActive("A", false);

            Assert.AreEqual(DefaultA, _owner.fieldA);
            Assert.AreEqual(DefaultB, _owner.fieldB);
        }

        [Test]
        public void Blocking_HigherPriorityStateBlocked_LowerPriorityValueApplies() {
            // A would win fieldA by index, but it's blocked by B.
            StateList list = Init(new StateSpec("A", Ov((0, 1))) { BlockedBy = { "B" } },
                                  new StateSpec("B", Ov((0, 2))));
            list.SetStateActive("A", true);
            list.SetStateActive("B", true);
            Assert.AreEqual(2f, _owner.fieldA);

            list.SetStateActive("B", false);
            Assert.AreEqual(1f, _owner.fieldA);

            list.SetStateActive("A", false);
            Assert.AreEqual(DefaultA, _owner.fieldA);
        }

        [Test]
        public void Blocking_Chain_BlockedBlockerDoesNotBlock() {
            // A blocks B, B blocks C. With all three active, B is blocked, so C applies.
            StateList list = Init(new StateSpec("A", null),
                                  new StateSpec("B", Ov((0, 2))) { BlockedBy = { "A" } },
                                  new StateSpec("C", Ov((1, 3))) { BlockedBy = { "B" } });
            list.SetStateActive("C", true);
            list.SetStateActive("B", true);
            Assert.AreEqual(2f, _owner.fieldA);
            Assert.AreEqual(DefaultB, _owner.fieldB, "B blocks C.");

            list.SetStateActive("A", true);
            Assert.AreEqual(DefaultA, _owner.fieldA, "A blocks B.");
            Assert.AreEqual(3f, _owner.fieldB, "B is blocked, so C applies again.");

            list.SetStateActive("A", false);
            Assert.AreEqual(2f, _owner.fieldA);
            Assert.AreEqual(DefaultB, _owner.fieldB);
        }

        [Test]
        public void Blocking_Mutual_FirstActiveWins() {
            // Tests today's order-dependent behaviour: whichever is active first blocks the other.
            StateList list = Init(new StateSpec("A", Ov((0, 1))) { BlockedBy = { "B" } },
                                  new StateSpec("B", Ov((1, 2))) { BlockedBy = { "A" } });
            list.SetStateActive("A", true);
            list.SetStateActive("B", true);
            Assert.AreEqual(1f, _owner.fieldA);
            Assert.AreEqual(DefaultB, _owner.fieldB);

            list.SetStateActive("A", false);
            Assert.AreEqual(DefaultA, _owner.fieldA);
            Assert.AreEqual(2f, _owner.fieldB);
        }

        [Test]
        public void Blocking_OwnBit_IsIgnored() {
            StateList list = Init(new StateSpec("A", Ov((0, 1))) { ExtraBlockedByMask = 1 });

            list.SetStateActive("A", true);

            Assert.AreEqual(1f, _owner.fieldA);
        }

        [Test]
        public void Blocking_InactiveBlocker_DoesNotBlock() {
            StateList list = Init(new StateSpec("A", Ov((1, 1))),
                                  new StateSpec("B", Ov((0, 2))) { BlockedBy = { "A" } });

            list.SetStateActive("B", true);

            Assert.AreEqual(2f, _owner.fieldA);
        }

        #endregion

        #region Model-based

        /// <summary>
        /// Random sequences of state changes over five states with overlapping fields and a block graph without
        /// cycles, checked after every change against a simple reference model: a state applies if it is active and
        /// no applying state blocks it, and each field takes the value of the lowest-index applying state that
        /// overrides it (otherwise its original value).
        /// </summary>
        [Test]
        public void Model_RandomSequences_MatchReference([NUnit.Framework.Range(1, 30)] int seed) {
            const int stateCount = 5;
            const int fieldCount = TestFieldsStateBehaviour.FieldCount;
            const int steps = 150;
            var rng = new System.Random(seed);

            float[] defaults = new float[fieldCount];
            for (int f = 0; f < fieldCount; f++) {
                defaults[f] = 100 + f;
                _owner.SetField(f, defaults[f]);
            }

            // A random ranking; a state can only be blocked by states ranked before it, so there are no cycles.
            int[] rank = Enumerable.Range(0, stateCount).OrderBy(_ => rng.Next()).ToArray();
            string[] names = Enumerable.Range(0, stateCount).Select(i => $"S{i}").ToArray();
            float?[,] values = new float?[stateCount, fieldCount];
            var blockers = new List<int>[stateCount];
            bool[] active = new bool[stateCount];
            var specs = new StateSpec[stateCount];
            var description = new StringBuilder($"Seed {seed}.\n");

            for (int s = 0; s < stateCount; s++) {
                var overrides = new List<(int, float)>();
                for (int f = 0; f < fieldCount; f++) {
                    if (rng.NextDouble() < 0.5) {
                        values[s, f] = (s + 1) * 10 + f;
                        overrides.Add((f, values[s, f].Value));
                    }
                }

                blockers[s] = Enumerable.Range(0, stateCount)
                                        .Where(j => rank[j] < rank[s] && rng.NextDouble() < 0.35).ToList();
                active[s] = rng.NextDouble() < 0.3;

                specs[s] = new StateSpec(names[s], Ov(overrides.ToArray())) { Active = active[s] };
                specs[s].BlockedBy.AddRange(blockers[s].Select(j => names[j]));
                description.Append($"{names[s]}: fields [{string.Join(", ", overrides.Select(o => o.Item1))}], " +
                                   $"blocked by [{string.Join(", ", specs[s].BlockedBy)}], " +
                                   $"initially {(active[s] ? "on" : "off")}.\n");
            }

            StateList list = CreateStateList(specs);
            list.Initialize(_owner);

            void Check(string when) {
                bool[] applies = new bool[stateCount];
                foreach (int s in Enumerable.Range(0, stateCount).OrderBy(i => rank[i])) {
                    applies[s] = active[s] && !blockers[s].Any(j => applies[j]);
                }

                for (int s = 0; s < stateCount; s++) {
                    Assert.AreEqual(active[s], list.IsStateActive(names[s]), $"{description}{when}: {names[s]} active");
                }

                for (int f = 0; f < fieldCount; f++) {
                    float expected = defaults[f];
                    for (int s = 0; s < stateCount; s++) {
                        if (applies[s] && values[s, f].HasValue) {
                            expected = values[s, f].Value;
                            break;
                        }
                    }

                    Assert.AreEqual(expected, _owner.GetField(f), $"{description}{when}: field {f}");
                }
            }

            Check("After Initialize");
            for (int step = 0; step < steps; step++) {
                int s = rng.Next(stateCount);
                bool on = rng.Next(2) == 0;
                list.SetStateActive(names[s], on);
                active[s] = on;
                description.Append($"Step {step}: {names[s]} {(on ? "on" : "off")}.\n");
                Check($"After step {step}");
            }
        }

        #endregion

        #region Stale and mismatched overrides

        [Test]
        public void RenamedAndCurrentPaths_ShareOneField() {
            // Suspected bug #3: the unmigrated path "oldFieldE" and the current "fieldE" got separate field IDs.
            _owner.fieldE = 5;
            ComponentOverride oldPath = CreateOverride<TestFieldsStateBehaviour>(("fieldE", 1f));
            SetStoredPath(oldPath, oldPath.Overrides[0], "oldFieldE");
            Assert.IsTrue(oldPath.Overrides[0].Valid);
            ComponentOverride newPath = CreateOverride<TestFieldsStateBehaviour>(("fieldE", 2f));
            StateList list = Init(new StateSpec("Old", oldPath), new StateSpec("New", newPath));

            list.SetStateActive("Old", true);
            list.SetStateActive("New", true);
            Assert.AreEqual(1f, _owner.fieldE, "Old has the lower index, so it wins.");

            list.SetStateActive("Old", false);
            Assert.AreEqual(2f, _owner.fieldE);

            list.SetStateActive("New", false);
            Assert.AreEqual(5f, _owner.fieldE);
        }

        [Test]
        public void InvalidOverride_IsSkippedWithWarning() {
            // Suspected bug #1: one stale override threw and stopped the state list initializing.
            ComponentOverride values = Ov((0, 1), (1, 2));
            SetStoredPath(values, values.Overrides[1], "removedField");

            LogAssert.Expect(LogType.Warning, new Regex("removedField"));
            StateList list = null;
            Assert.DoesNotThrow(() => list = Init(new StateSpec("A", values) { Active = true }));

            Assert.AreEqual(1f, _owner.fieldA);
            Assert.DoesNotThrow(() => list.SetStateActive("A", false));
            Assert.AreEqual(DefaultA, _owner.fieldA);
            Assert.AreEqual(DefaultB, _owner.fieldB);
        }

        [Test]
        public void OverrideForWrongType_LogsErrorAndIsSkipped() {
            // Suspected bug #1: an override authored for another type threw when applied.
            ComponentOverride wrongType = CreateOverride<TestExteriorTarget>(("speed", 1f));

            LogAssert.Expect(LogType.Error, new Regex(nameof(TestExteriorTarget)));
            StateList list = null;
            Assert.DoesNotThrow(() => list = Init(new StateSpec("Wrong", wrongType) { Active = true },
                                                  new StateSpec("A", Ov((0, 1)))));

            Assert.IsTrue(list.IsStateActive("Wrong"));
            Assert.DoesNotThrow(() => list.SetStateActive("Wrong", false));
            list.SetStateActive("A", true);
            Assert.AreEqual(1f, _owner.fieldA);
        }

        [Test]
        public void OverrideWithUnknownType_LogsErrorAndIsSkipped() {
            ComponentOverride unknown = Ov((0, 1));
            unknown.TypeName = "Not.A.Real.Type";

            LogAssert.Expect(LogType.Error, new Regex("Not.A.Real.Type"));
            StateList list = null;
            Assert.DoesNotThrow(() => list = Init(new StateSpec("A", unknown) { Active = true }));

            Assert.AreEqual(DefaultA, _owner.fieldA);
            Assert.DoesNotThrow(() => list.SetStateActive("A", false));
        }

        [Test]
        public void MoreThan32States_LogsErrorAndHighStatesDoNotBlock() {
            // Suspected bug #21: BlockedBy is a 32-bit mask, so 1 << 32 wrapped around to bit 0.
            var specs = new List<StateSpec> {
                new StateSpec("S0", null),
                new StateSpec("S1", Ov((0, 1))) { BlockedBy = { "S0" } },
            };
            for (int i = 2; i <= 32; i++) specs.Add(new StateSpec($"S{i}", null));

            LogAssert.Expect(LogType.Error, new Regex("32"));
            StateList list = Init(specs.ToArray());

            list.SetStateActive("S32", true);
            list.SetStateActive("S1", true);

            Assert.AreEqual(1f, _owner.fieldA, "S32 isn't S0, so it shouldn't block S1.");
        }

        #endregion
    }
}
