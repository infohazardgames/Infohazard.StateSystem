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

namespace Infohazard.StateSystem {
    /// <summary>
    /// Marks a field or property as having previously been named <see cref="OldName"/>,
    /// so that <see cref="ComponentOverride"/> assets referring to the old name still resolve.
    /// </summary>
    /// <remarks>
    /// Fields can use Unity's <see cref="UnityEngine.Serialization.FormerlySerializedAsAttribute"/> instead,
    /// which is also recognized. This attribute exists mainly for properties, which that attribute cannot target.
    /// Assets are rewritten to the new name when inspected, or via Tools/Infohazard/Migrate Component Overrides.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public class FormerlyOverriddenAsAttribute : Attribute {
        public string OldName { get; }

        public FormerlyOverriddenAsAttribute(string oldName) {
            OldName = oldName;
        }
    }
}
