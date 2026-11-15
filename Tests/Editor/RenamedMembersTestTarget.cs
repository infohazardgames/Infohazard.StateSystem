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
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.StateSystem.Tests.Editor {
    /// <summary>
    /// Object whose members carry former names, standing in for a component whose members were
    /// renamed after override assets were authored against it.
    /// </summary>
    [Serializable]
    public class RenamedMembersTestTarget {
        [FormerlySerializedAs("_oldSpeed")]
        [SerializeField] private float _speed;

        [FormerlyOverriddenAs("_oldDistance")]
        [SerializeField] private float _distance;

        [FormerlySerializedAs("_oldSpring")]
        [SerializeField] private SpringSettings _spring = new SpringSettings();

        // Not renamed; checks that ordinary paths are untouched.
        [SerializeField] private int _count;

        [FormerlyOverriddenAs("OldIsOn")]
        public bool IsOn { get; set; }

        public float Speed => _speed;
        public float Distance => _distance;
        public SpringSettings Spring => _spring;
        public int Count => _count;

        [Serializable]
        public class SpringSettings {
            [FormerlySerializedAs("oldStiffness")]
            public float stiffness;
        }
    }
}
