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
using UnityEngine;
using Object = UnityEngine.Object;

namespace Infohazard.StateSystem.Tests.PlayMode {
    /// <summary>
    /// A state behaviour with a member of every overridable type, plus members that override path discovery
    /// should include or skip.
    /// </summary>
    /// <remarks>
    /// Test-only diagnostics are get-only or private-set properties, so they aren't overridable paths themselves.
    /// </remarks>
    public class TestStateBehaviour : StateBehaviour {
        // One public field of each SerializableType.
        public byte byteValue;
        public sbyte sbyteValue;
        public short shortValue;
        public ushort ushortValue;
        public int intValue;
        public uint uintValue;
        public long longValue;
        public ulong ulongValue;
        public float floatValue;
        public double doubleValue;
        public bool boolValue;
        public char charValue;
        public string stringValue;
        public TestEnum enumValue;
        public TestFlags flagsValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Vector4 vector4Value;
        public Quaternion quaternionValue;
        public Color colorValue;
        public LayerMask layerMaskValue;
        public Object objectValue;

        // A narrower object reference type, for type-mismatch checks.
        public Material materialValue;

        // Enums with unusual underlying types.
        public TestUIntFlags uintFlagsValue;
        // A property, not a field: Unity can't serialize long-backed enums and logs an error (failing builds and
        // Instantiate) for such a field. Properties with a public getter and setter are still overridable.
        public TestLongEnum LongEnumValue { get; set; }

        // Nested paths.
        public TestStruct structValue;
        public TestNested nestedValue = new TestNested();

        // Included: private with [SerializeField].
        [SerializeField] private float _serializedPrivate;

        // Excluded fields.
        private float _plainPrivate;
        public readonly float readonlyValue = 0;
        [NonSerialized] public float nonSerializedValue;
        [NoOverrides] public float noOverridesValue;

        // Included: public get and set.
        public float FloatProperty { get; set; }

        // Excluded properties.
        public float PrivateSetterProperty { get; private set; }
        public float GetterOnlyProperty => _plainPrivate;
        [NoOverrides] public float NoOverridesProperty { get; set; }

        // Types path discovery shouldn't recurse into.
        public TestAbstractThing abstractValue;
        public TestNoDefaultConstructor noDefaultConstructorValue;
        public TestSelfReferencing selfReferencingValue;

        // Has an indexer (List<T>.Item).
        public List<float> listValue = new List<float>();

        public float SerializedPrivate {
            get => _serializedPrivate;
            set => _serializedPrivate = value;
        }

        /// <summary>
        /// How many times <see cref="InitializeDefaultValues"/> ran.
        /// </summary>
        public int InitializeDefaultValuesCount { get; private set; }

        /// <summary>
        /// The value of <see cref="floatValue"/> when <see cref="InitializeDefaultValues"/> last ran.
        /// </summary>
        public float FloatValueAtInitializeDefaults { get; private set; }

        protected override void InitializeDefaultValues() {
            InitializeDefaultValuesCount++;
            FloatValueAtInitializeDefaults = floatValue;
        }
    }
}
