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
using Infohazard.StateSystem.Tests.PlayMode;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Infohazard.StateSystem.Tests.Editor {
    /// <summary>
    /// Value storage, member path discovery and resolution, and getting/setting values through a path.
    /// </summary>
    public class UnitTest_SerializedValueOverride : StateSystemTestFixture {
        private ComponentOverride _asset;
        private TestStateBehaviour _behaviour;

        [SetUp]
        public void SetUp() {
            _asset = CreateOverride<TestStateBehaviour>();
            _behaviour = CreateGameObject("Behaviour").AddComponent<TestStateBehaviour>();
        }

        private SerializedValueOverride Add(string path) => AddOverride(_asset, path);

        #region GetSerializableType

        [TestCase(typeof(byte), SerializableType.Byte)]
        [TestCase(typeof(sbyte), SerializableType.SByte)]
        [TestCase(typeof(short), SerializableType.Short)]
        [TestCase(typeof(ushort), SerializableType.UShort)]
        [TestCase(typeof(int), SerializableType.Int)]
        [TestCase(typeof(uint), SerializableType.UInt)]
        [TestCase(typeof(long), SerializableType.Long)]
        [TestCase(typeof(ulong), SerializableType.ULong)]
        [TestCase(typeof(float), SerializableType.Float)]
        [TestCase(typeof(double), SerializableType.Double)]
        [TestCase(typeof(bool), SerializableType.Bool)]
        [TestCase(typeof(char), SerializableType.Char)]
        [TestCase(typeof(string), SerializableType.String)]
        [TestCase(typeof(TestEnum), SerializableType.Enum)]
        [TestCase(typeof(TestLongEnum), SerializableType.Enum)]
        [TestCase(typeof(Vector2), SerializableType.Vector2)]
        [TestCase(typeof(Vector3), SerializableType.Vector3)]
        [TestCase(typeof(Vector4), SerializableType.Vector4)]
        [TestCase(typeof(Quaternion), SerializableType.Quaternion)]
        [TestCase(typeof(Color), SerializableType.Color)]
        [TestCase(typeof(LayerMask), SerializableType.LayerMask)]
        [TestCase(typeof(Object), SerializableType.ObjectReference)]
        [TestCase(typeof(Material), SerializableType.ObjectReference)]
        [TestCase(typeof(GameObject), SerializableType.ObjectReference)]
        [TestCase(typeof(TestStruct), SerializableType.None)]
        [TestCase(typeof(TestNested), SerializableType.None)]
        [TestCase(typeof(decimal), SerializableType.None)]
        [TestCase(typeof(List<float>), SerializableType.None)]
        [TestCase(null, SerializableType.None)]
        public void GetSerializableType_MapsTypes(Type type, SerializableType expected) {
            Assert.AreEqual(expected, SerializedValueOverride.GetSerializableType(type));
        }

        #endregion

        #region Value

        private static IEnumerable<TestCaseData> RoundTripCases() {
            TestCaseData Case(string path, object value, string name) =>
                new TestCaseData(path, value).SetName($"Value_RoundTrips({path}, {name})");

            yield return Case("byteValue", (byte) 0, "0");
            yield return Case("byteValue", byte.MaxValue, "max");
            yield return Case("sbyteValue", sbyte.MinValue, "min");
            yield return Case("shortValue", short.MinValue, "min");
            yield return Case("ushortValue", ushort.MaxValue, "max");
            yield return Case("intValue", int.MinValue, "min");
            yield return Case("intValue", -7, "negative");
            yield return Case("uintValue", uint.MaxValue, "max");
            yield return Case("longValue", long.MinValue, "min");
            yield return Case("longValue", long.MaxValue, "max");
            yield return Case("ulongValue", 0UL, "0");
            yield return Case("ulongValue", 1UL << 63, "high bit");
            yield return Case("ulongValue", ulong.MaxValue, "max");
            yield return Case("floatValue", -0.5f, "negative");
            yield return Case("floatValue", float.PositiveInfinity, "infinity");
            yield return Case("doubleValue", Math.PI, "pi");
            yield return Case("doubleValue", double.NaN, "NaN");
            yield return Case("doubleValue", double.MinValue, "min");
            yield return Case("boolValue", true, "true");
            yield return Case("boolValue", false, "false");
            yield return Case("charValue", 'Z', "Z");
            yield return Case("charValue", '\0', "null char");
            yield return Case("charValue", 'é', "accented");
            yield return Case("stringValue", "héllo", "text");
            yield return Case("stringValue", "", "empty");
            yield return Case("stringValue", null, "null");
            yield return Case("enumValue", TestEnum.C, "C");
            yield return Case("enumValue", TestEnum.Negative, "negative");
            yield return Case("flagsValue", TestFlags.One | TestFlags.Four, "combined flags");
            yield return Case("vector2Value", new Vector2(1, -2), "vector");
            yield return Case("vector3Value", new Vector3(1, -2, 3.5f), "vector");
            yield return Case("vector4Value", new Vector4(1, -2, 3.5f, -4), "vector");
            yield return Case("quaternionValue", new Quaternion(1, 2, 3, 4), "not normalized");
            yield return Case("colorValue", new Color(2, 0.5f, 0, 0.25f), "HDR");
            yield return Case("layerMaskValue", (LayerMask) (-1), "all bits");
            yield return Case("layerMaskValue", (LayerMask) (1 << 5), "one bit");
        }

        [TestCaseSource(nameof(RoundTripCases))]
        public void Value_RoundTrips(string path, object value) {
            SerializedValueOverride stored = Add(path);

            SetStoredValue(stored, value);
            Assert.AreEqual(value, stored.Value);
            if (value != null) Assert.AreEqual(value.GetType(), stored.Value.GetType());

            stored.SetFieldValue(_behaviour);
            Assert.AreEqual(value, stored.GetFieldValue(_behaviour), "The value should reach the component.");
        }

        [Test]
        public void Value_ObjectReference_RoundTrips() {
            SerializedValueOverride stored = Add("objectValue");
            Object reference = Track(ScriptableObject.CreateInstance<ComponentOverride>());

            stored.Value = reference;
            Assert.AreSame(reference, stored.Value);
            stored.SetFieldValue(_behaviour);
            Assert.AreSame(reference, _behaviour.objectValue);

            stored.Value = null;
            Assert.IsNull(stored.Value);
            stored.SetFieldValue(_behaviour);
            Assert.IsNull(_behaviour.objectValue);
        }

        [Test]
        public void Value_SettingClearsPreviousStorage() {
            SerializedValueOverride stored = Add("stringValue");
            stored.Value = "text";
            stored.Value = null;
            Assert.IsNull(stored.Value);
        }

        [Test]
        public void Value_NoType_Throws() {
            var stored = new SerializedValueOverride();
            Assert.AreEqual(SerializableType.None, stored.Type);
            Assert.Throws<UnityException>(() => _ = stored.Value);
            Assert.Throws<UnityException>(() => stored.Value = 1L);
        }

        private static IEnumerable<TestCaseData> NaturalTypeCases() {
            TestCaseData Case(string path, object value) =>
                new TestCaseData(path, value).SetName($"Value_SetWithMemberType_Stores({path})");

            yield return Case("byteValue", (byte) 200);
            yield return Case("sbyteValue", (sbyte) -100);
            yield return Case("shortValue", (short) -1000);
            yield return Case("ushortValue", (ushort) 60000);
            yield return Case("intValue", 7);
            yield return Case("uintValue", 4000000000u);
            yield return Case("longValue", -5L);
            yield return Case("ulongValue", ulong.MaxValue);
            yield return Case("charValue", 'x');
        }

        [TestCaseSource(nameof(NaturalTypeCases))]
        public void Value_SetWithMemberType_Stores(string path, object value) {
            // Suspected bug #4: integer types only accepted a boxed long, so `Value = 5` threw.
            SerializedValueOverride stored = Add(path);

            Assert.DoesNotThrow(() => stored.Value = value);
            Assert.AreEqual(value, stored.Value);
        }

        [Test]
        public void Value_EnumBeyondIntRange_RoundTrips() {
            // Suspected bug #8: the enum setter used Convert.ToInt32, which overflows for these.
            SerializedValueOverride uintFlags = Add("uintFlagsValue");
            Assert.DoesNotThrow(() => uintFlags.Value = TestUIntFlags.All);
            Assert.AreEqual(TestUIntFlags.All, uintFlags.Value);

            uintFlags.Value = TestUIntFlags.High;
            Assert.AreEqual(TestUIntFlags.High, uintFlags.Value);

            SerializedValueOverride longEnum = Add("LongEnumValue");
            Assert.DoesNotThrow(() => longEnum.Value = TestLongEnum.Big);
            Assert.AreEqual(TestLongEnum.Big, longEnum.Value);
        }

        [Test]
        public void ToString_ShowsPathAndType() {
            Assert.AreEqual("floatValue (Float)", Add("floatValue").ToString());
        }

        #endregion

        #region GetValidPathsForType

        private static List<string> PathsFor<T>() => SerializedValueOverride.GetValidPathsForType(typeof(T));

        [Test]
        public void GetValidPaths_IncludesEveryOverridableMember() {
            List<string> paths = PathsFor<TestStateBehaviour>();

            CollectionAssert.IsSubsetOf(new[] {
                "byteValue", "sbyteValue", "shortValue", "ushortValue", "intValue", "uintValue", "longValue",
                "ulongValue", "floatValue", "doubleValue", "boolValue", "charValue", "stringValue", "enumValue",
                "flagsValue", "vector2Value", "vector3Value", "vector4Value", "quaternionValue", "colorValue",
                "layerMaskValue", "objectValue", "materialValue", "uintFlagsValue", "LongEnumValue",
                "_serializedPrivate", "FloatProperty", "SerializedPrivate",
            }, paths);
        }

        [Test]
        public void GetValidPaths_IncludesNestedLeavesButNotContainers() {
            List<string> paths = PathsFor<TestStateBehaviour>();

            CollectionAssert.IsSubsetOf(new[] {
                "structValue/x", "structValue/y", "nestedValue/value", "nestedValue/inner/x", "nestedValue/inner/y",
            }, paths);
            CollectionAssert.DoesNotContain(paths, "structValue");
            CollectionAssert.DoesNotContain(paths, "nestedValue");
            CollectionAssert.DoesNotContain(paths, "nestedValue/inner");
        }

        [TestCase("_plainPrivate")]
        [TestCase("readonlyValue")]
        [TestCase("nonSerializedValue")]
        [TestCase("noOverridesValue")]
        [TestCase("PrivateSetterProperty")]
        [TestCase("GetterOnlyProperty")]
        [TestCase("NoOverridesProperty")]
        [TestCase("InitializeDefaultValuesCount")]
        public void GetValidPaths_ExcludesMember(string member) {
            CollectionAssert.DoesNotContain(PathsFor<TestStateBehaviour>(), member);
        }

        [TestCase("abstractValue")]
        [TestCase("noDefaultConstructorValue")]
        public void GetValidPaths_DoesNotRecurseInto(string member) {
            Assert.IsFalse(PathsFor<TestStateBehaviour>().Any(p => p == member || p.StartsWith(member + "/")));
        }

        [Test]
        public void GetValidPaths_SelfReferencingType_StopsAtMaxDepth() {
            List<string> selfPaths = PathsFor<TestStateBehaviour>()
                                     .Where(p => p.StartsWith("selfReferencingValue/")).ToList();

            CollectionAssert.Contains(selfPaths, "selfReferencingValue/value");
            Assert.LessOrEqual(selfPaths.Max(p => p.Split('/').Length), SerializedValueOverride.MaxDepth);
            Assert.AreEqual(SerializedValueOverride.MaxDepth - 1, selfPaths.Count,
                            "One 'value' leaf per level below the root member.");
        }

        [Test]
        public void GetValidPaths_EveryPathCanBeReadAndWritten() {
            // Suspected bug #5: indexers (List<T>.Item) were listed, and reading them throws.
            var failures = new List<string>();
            foreach (string path in PathsFor<TestStateBehaviour>()) {
                var value = new SerializedValueOverride { Path = path };
                value.FirstTimeInitialize(typeof(TestStateBehaviour));
                try {
                    Assert.IsTrue(value.Valid, "Not valid after initializing.");
                    value.SetFieldValue(_behaviour, value.GetFieldValue(_behaviour));
                } catch (Exception e) {
                    failures.Add($"{path}: {e.GetType().Name}: {e.Message}");
                }
            }

            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void GetValidPaths_IncludesPrivateSerializedFieldsOfBaseClasses() {
            // Suspected bug #6: GetFields doesn't return private fields declared in a base class.
            List<string> paths = PathsFor<TestDerivedStateBehaviour>();

            CollectionAssert.IsSubsetOf(new[] { "derivedValue", "basePublic", "BaseSerialized" }, paths);
            CollectionAssert.Contains(paths, "_baseSerialized");
        }

        #endregion

        #region Initialize

        [Test]
        public void Initialize_Field_ResolvesChain() {
            SerializedValueOverride value = Add("floatValue");

            Assert.IsTrue(value.Valid);
            Assert.AreEqual(1, value.FieldChain.Count);
            Assert.AreEqual(0, value.AncestryLength);
            Assert.AreEqual("floatValue", value.FieldName);
            Assert.AreEqual(typeof(float), value.ValueType);
            Assert.AreEqual(SerializableType.Float, value.Type);
            Assert.IsFalse(value.IsProperty);
        }

        [Test]
        public void Initialize_Property_IsProperty() {
            SerializedValueOverride value = Add("FloatProperty");

            Assert.IsTrue(value.Valid);
            Assert.IsTrue(value.IsProperty);
        }

        [Test]
        public void Initialize_NestedPath_ResolvesEachPart() {
            SerializedValueOverride value = Add("nestedValue/inner/y");

            Assert.IsTrue(value.Valid);
            Assert.AreEqual(2, value.AncestryLength);
            CollectionAssert.AreEqual(new[] { "nestedValue", "inner", "y" }, value.FieldChain.Select(m => m.Name));
            Assert.AreEqual(typeof(int), value.ValueType);
        }

        [TestCase("notAField")]
        [TestCase("nestedValue/missing")]
        [TestCase("missing/value")]
        [TestCase("_plainPrivate")]
        [TestCase("noOverridesValue")]
        [TestCase("PrivateSetterProperty")]
        public void Initialize_UnresolvablePath_IsInvalid(string path) {
            SerializedValueOverride value = Add("floatValue");

            SetStoredPath(_asset, value, path);

            Assert.IsFalse(value.Valid);
            Assert.AreEqual(0, value.FieldChain.Count);
        }

        [Test]
        public void Initialize_NullTypeOrEmptyPath_IsInvalid() {
            var value = new SerializedValueOverride { Path = "floatValue" };
            value.Initialize(null);
            Assert.IsFalse(value.Valid);

            value = new SerializedValueOverride { Path = "" };
            value.Initialize(typeof(TestStateBehaviour));
            Assert.IsFalse(value.Valid);
        }

        [Test]
        public void Initialize_ReinitializingInvalidOverride_ClearsPreviousChain() {
            SerializedValueOverride value = Add("floatValue");
            Assert.AreEqual(1, value.FieldChain.Count);

            value.Initialize(null);

            Assert.IsFalse(value.Valid);
            Assert.AreEqual(0, value.FieldChain.Count);
        }

        [Test]
        public void Initialize_MemberTypeChanged_IsInvalid() {
            // Suspected bug #2: authored as a float, but the member is now an int.
            SerializedValueOverride value = Add("floatValue");
            value.Value = 1.5f;

            SetStoredPath(_asset, value, "intValue");

            Assert.AreEqual(SerializableType.Float, value.Type);
            Assert.IsFalse(value.Valid, "A stored float can't be applied to an int member.");
        }

        [Test]
        public void Initialize_StoredObjectNotAssignableToMember_IsInvalid() {
            // Suspected bug #2: the member's type narrowed (Object to Material), but the stored object isn't one.
            SerializedValueOverride value = Add("objectValue");
            value.Value = Track(ScriptableObject.CreateInstance<ComponentOverride>());

            SetStoredPath(_asset, value, "materialValue");

            Assert.IsFalse(value.Valid, "A ScriptableObject can't be applied to a Material member.");
        }

        [Test]
        public void Initialize_NullObjectWithNarrowerMember_IsValid() {
            SerializedValueOverride value = Add("objectValue");

            SetStoredPath(_asset, value, "materialValue");

            Assert.IsTrue(value.Valid);
        }

        [Test]
        public void Initialize_InheritedPrivateSerializedField_IsValid() {
            // Suspected bug #6.
            ComponentOverride asset = CreateOverride<TestDerivedStateBehaviour>(("basePublic", 1f));
            SerializedValueOverride value = asset.Overrides[0];

            SetStoredPath(asset, value, "_baseSerialized");

            Assert.IsTrue(value.Valid);
        }

        [Test]
        public void FirstTimeInitialize_FlagsEnum_IsEnumFlags() {
            // Suspected bug #7: IsEnumFlags was computed before the type was set.
            Assert.IsTrue(Add("flagsValue").IsEnumFlags);
            Assert.IsFalse(Add("enumValue").IsEnumFlags);
        }

        #endregion

        #region GetFieldValue / SetFieldValue

        [Test]
        public void SetFieldValue_Property_SetsProperty() {
            SerializedValueOverride value = Add("FloatProperty");
            value.Value = 3f;

            value.SetFieldValue(_behaviour);

            Assert.AreEqual(3f, _behaviour.FloatProperty);
            Assert.AreEqual(3f, value.GetFieldValue(_behaviour));
        }

        [Test]
        public void SetFieldValue_PrivateSerializedField_SetsField() {
            SerializedValueOverride value = Add("_serializedPrivate");
            value.Value = 4f;

            value.SetFieldValue(_behaviour);

            Assert.AreEqual(4f, _behaviour.SerializedPrivate);
        }

        [Test]
        public void SetFieldValue_NestedStruct_WritesCopyBack() {
            _behaviour.structValue = new TestStruct { x = 1, y = 2 };
            SerializedValueOverride value = Add("structValue/x");
            value.Value = 9f;

            value.SetFieldValue(_behaviour);

            Assert.AreEqual(9f, _behaviour.structValue.x);
            Assert.AreEqual(2, _behaviour.structValue.y, "Other struct fields should be kept.");
        }

        [Test]
        public void SetFieldValue_StructInsideClass_WritesCopyBack() {
            TestNested nested = _behaviour.nestedValue;
            SerializedValueOverride value = Add("nestedValue/inner/y");
            SetStoredValue(value, 6);

            value.SetFieldValue(_behaviour);

            Assert.AreSame(nested, _behaviour.nestedValue, "The existing class instance should be kept.");
            Assert.AreEqual(6, _behaviour.nestedValue.inner.y);
        }

        [Test]
        public void SetFieldValue_NullClassInPath_ConstructsIt() {
            _behaviour.nestedValue = null;
            SerializedValueOverride value = Add("nestedValue/value");
            value.Value = 2f;

            Assert.IsNull(value.GetFieldValue(_behaviour), "Reading through a null class gives null.");

            value.SetFieldValue(_behaviour);

            Assert.IsNotNull(_behaviour.nestedValue);
            Assert.AreEqual(2f, _behaviour.nestedValue.value);
        }

        [Test]
        public void SetFieldValue_ExplicitValue_UsesItInsteadOfStoredValue() {
            SerializedValueOverride value = Add("floatValue");
            value.Value = 1f;

            value.SetFieldValue(_behaviour, 5f);

            Assert.AreEqual(5f, _behaviour.floatValue);
            Assert.AreEqual(1f, value.Value);
        }

        [Test]
        public void SetFieldValue_PlainObject_Works() {
            var value = new SerializedValueOverride { Path = "inner/y" };
            value.FirstTimeInitialize(typeof(TestNested));
            SetStoredValue(value, 3);
            var target = new TestNested();

            value.SetFieldValue(target);

            Assert.AreEqual(3, target.inner.y);
        }

        [Test]
        public void SetFieldValue_InvalidOverride_DoesNothing() {
            // Suspected bug #1: an empty FieldChain threw ArgumentOutOfRangeException.
            SerializedValueOverride value = Add("floatValue");
            value.Value = 5f;
            SetStoredPath(_asset, value, "notAField");

            Assert.DoesNotThrow(() => value.SetFieldValue(_behaviour));
            Assert.DoesNotThrow(() => value.SetFieldValue(_behaviour, 5f));
            Assert.AreEqual(0f, _behaviour.floatValue);
        }

        [Test]
        public void GetFieldValue_InvalidOverride_ReturnsNull() {
            // Suspected bug #1: an empty FieldChain returned the object itself.
            SerializedValueOverride value = Add("floatValue");
            SetStoredPath(_asset, value, "notAField");

            Assert.IsNull(value.GetFieldValue(_behaviour));
        }

        #endregion
    }
}
