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
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Infohazard.StateSystem.Tests.Editor {
    /// <summary>
    /// A hidden EditorWindow that runs an IMGUI callback inside a real OnGUI pass, so inspectors and property drawers
    /// can be exercised from tests.
    /// </summary>
    public class ImGuiHarness : EditorWindow {
        private Action _draw;
        private Exception _error;
        private int _passes;

        private void OnGUI() {
            if (_draw == null) return;

            try {
                _draw();
            } catch (ExitGUIException) {
                // Normal IMGUI control flow.
            } catch (Exception e) {
                _error ??= e;
            }

            _passes++;
        }

        /// <summary>
        /// Run <paramref name="draw"/> inside OnGUI for a layout pass and a repaint pass.
        /// Marks the test inconclusive if IMGUI can't run (for example, without a graphics device).
        /// </summary>
        public static void Run(Action draw) {
            MethodInfo repaintImmediately =
                typeof(EditorWindow).GetMethod("RepaintImmediately", BindingFlags.NonPublic | BindingFlags.Instance);
            if (repaintImmediately == null) Assert.Inconclusive("EditorWindow.RepaintImmediately is unavailable.");

            ImGuiHarness window = CreateInstance<ImGuiHarness>();
            try {
                window._draw = draw;
                window.position = new Rect(0, 0, 500, 1200);
                window.ShowUtility();
                repaintImmediately!.Invoke(window, null);
                repaintImmediately.Invoke(window, null);

                if (window._passes == 0) Assert.Inconclusive("OnGUI never ran, so IMGUI isn't available here.");
                if (window._error != null) {
                    throw new AssertionException($"IMGUI drawing threw: {window._error}");
                }
            } finally {
                window._draw = null;
                window.Close();
            }
        }
    }
}
