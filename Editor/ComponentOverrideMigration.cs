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
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Infohazard.StateSystem.Editor {
    /// <summary>
    /// Rewrites <see cref="ComponentOverride"/> assets whose paths refer to renamed members
    /// (marked with <see cref="UnityEngine.Serialization.FormerlySerializedAsAttribute"/> or
    /// <see cref="FormerlyOverriddenAsAttribute"/>), and reports overrides that no longer resolve.
    /// </summary>
    public static class ComponentOverrideMigration {
        public class Result {
            public readonly List<string> Renamed = new List<string>();
            public readonly List<string> Unresolved = new List<string>();
            public int ModifiedAssetCount;
            public int AssetCount;
        }

        [MenuItem("Tools/Infohazard/Migrate Component Overrides")]
        public static void MigrateAllMenuItem() {
            Result result = MigrateAll();
            Debug.Log(FormatResult(result));
        }

        public static Result MigrateAll() {
            var result = new Result();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ComponentOverride)}");

            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (ComponentOverride asset in AssetDatabase.LoadAllAssetsAtPath(path).OfType<ComponentOverride>()) {
                    result.AssetCount++;
                    Migrate(asset, path, result);
                }
            }

            if (result.ModifiedAssetCount > 0) {
                AssetDatabase.SaveAssets();
            }

            return result;
        }

        public static bool Migrate(ComponentOverride asset, string assetPath, Result result) {
            // Re-resolve against current types, in case the asset was loaded before a recompile.
            asset.NotifyChanged();

            if (asset.Type == null) {
                result?.Unresolved.Add($"{assetPath}: type '{asset.TypeName}' not found");
                return false;
            }

            var oldPaths = asset.Overrides.Where(o => o.HasRenamedPath).ToDictionary(o => o, o => o.Path);

            bool changed = asset.ApplyRenamedPaths();
            if (changed) {
                EditorUtility.SetDirty(asset);
                if (result != null) {
                    result.ModifiedAssetCount++;
                    foreach (KeyValuePair<SerializedValueOverride, string> pair in oldPaths) {
                        if (pair.Key.Path != pair.Value) {
                            result.Renamed.Add($"{assetPath}: {pair.Value} -> {pair.Key.Path}");
                        }
                    }
                }
            }

            if (result != null) {
                foreach (SerializedValueOverride value in asset.Overrides) {
                    if (!value.Valid) {
                        result.Unresolved.Add($"{assetPath}: path '{value.Path}' not found on {asset.Type.Name}");
                    } else if (value.HasRenamedPath) {
                        result.Unresolved.Add($"{assetPath}: path '{value.Path}' could not be renamed to " +
                                              $"'{value.RenamedPath}' (already overridden)");
                    }
                }
            }

            return changed;
        }

        public static string FormatResult(Result result) {
            var sb = new StringBuilder();
            sb.Append($"Migrated component overrides: {result.Renamed.Count} path(s) renamed in " +
                      $"{result.ModifiedAssetCount} of {result.AssetCount} asset(s).");

            if (result.Unresolved.Count > 0) {
                sb.Append($" {result.Unresolved.Count} problem(s) remain.");
            }

            foreach (string line in result.Renamed) {
                sb.Append("\n  Renamed ").Append(line);
            }

            foreach (string line in result.Unresolved) {
                sb.Append("\n  Problem: ").Append(line);
            }

            return sb.ToString();
        }
    }
}
