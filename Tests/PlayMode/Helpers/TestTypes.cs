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

namespace Infohazard.StateSystem.Tests.PlayMode {
    public enum TestEnum {
        A,
        B,
        C = 5,
        Negative = -3,
    }

    [Flags]
    public enum TestFlags {
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
    }

    [Flags]
    public enum TestUIntFlags : uint {
        None = 0,
        One = 1,
        High = 0x80000000,
        All = 0xFFFFFFFF,
    }

    public enum TestLongEnum : long {
        Small = 1,
        Big = long.MaxValue,
    }

    [Serializable]
    public struct TestStruct {
        public float x;
        public int y;
    }

    [Serializable]
    public class TestNested {
        public float value;
        public TestStruct inner;
    }

    /// <summary>
    /// Abstract, so override path discovery shouldn't recurse into it.
    /// </summary>
    public abstract class TestAbstractThing {
        public float value;
    }

    /// <summary>
    /// Has no parameterless constructor, so override path discovery shouldn't recurse into it.
    /// </summary>
    public class TestNoDefaultConstructor {
        public float value;

        public TestNoDefaultConstructor(float value) {
            this.value = value;
        }
    }

    /// <summary>
    /// Refers to itself, so override path discovery must stop at its maximum depth.
    /// Not [Serializable], so Unity doesn't try to serialize the cycle.
    /// </summary>
    public class TestSelfReferencing {
        public TestSelfReferencing next;
        public float value;
    }
}
