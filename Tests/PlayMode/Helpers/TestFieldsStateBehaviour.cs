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

using UnityEngine.Serialization;

namespace Infohazard.StateSystem.Tests.PlayMode {
    /// <summary>
    /// A state behaviour with a few plain float fields, for testing how states combine.
    /// </summary>
    public class TestFieldsStateBehaviour : StateBehaviour {
        public const int FieldCount = 4;

        public float fieldA;
        public float fieldB;
        public float fieldC;
        public float fieldD;

        /// <summary>
        /// Renamed from oldFieldE, so overrides can refer to it by either name. Not one of the
        /// <see cref="FieldCount"/> indexed fields.
        /// </summary>
        [FormerlySerializedAs("oldFieldE")]
        public float fieldE;

        public static readonly string[] FieldNames = { nameof(fieldA), nameof(fieldB), nameof(fieldC), nameof(fieldD) };

        public float GetField(int index) => index switch {
            0 => fieldA,
            1 => fieldB,
            2 => fieldC,
            _ => fieldD,
        };

        public void SetField(int index, float value) {
            switch (index) {
                case 0:
                    fieldA = value;
                    break;
                case 1:
                    fieldB = value;
                    break;
                case 2:
                    fieldC = value;
                    break;
                default:
                    fieldD = value;
                    break;
            }
        }
    }
}
