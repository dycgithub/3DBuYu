using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证战斗清理后的源码、序列化资产和场景不再携带旧目标指针链路或 Wave 术语。
    /// </summary>
    public sealed class LegacyCombatCleanupTests
    {
        private const string RecoveryScenePath = "Assets/_Recovery/0.unity";

        private static readonly string[] LegacyTargetMarkers =
        {
            "TargetingInputSurface",
            "CardTargetingController",
            "CardTargetArrowView",
            "EnemyTargetMarkerView",
            "CardTargetingRoot",
            "SkillTargetSelectionAdapter",
            "SkillTargetSelectionState",
            "SkillTargetPointer",
            "SkillTargetPointerView",
            "SkillTargetMode"
        };

        private static readonly string[] TextFileExtensions =
        {
            ".anim",
            ".asset",
            ".asmdef",
            ".asmref",
            ".cginc",
            ".controller",
            ".cs",
            ".giparams",
            ".hlsl",
            ".inputactions",
            ".json",
            ".lighting",
            ".mat",
            ".meta",
            ".overridecontroller",
            ".physicsmaterial2d",
            ".physicmaterial",
            ".playable",
            ".prefab",
            ".preset",
            ".shader",
            ".shadergraph",
            ".shadervariants",
            ".spriteatlas",
            ".terrainlayer",
            ".txt",
            ".unity",
            ".uss",
            ".uxml",
            ".vfx"
        };

        [Test]
        public void LegacyTargetingTypes_AreAbsentFromProjectSourcesAndAssets()
        {
            AssertProjectDoesNotContain(LegacyTargetMarkers);
        }

        [Test]
        public void WaveIdentifiers_AreAbsentFromProjectSourcesAndAssets()
        {
            AssertProjectDoesNotContain(new[] { "wave" });
        }

        [Test]
        public void RecoveryScene_IsAbsent()
        {
            Assert.That(
                File.Exists(ToAbsolutePath(RecoveryScenePath)),
                Is.False,
                RecoveryScenePath);
        }

        [Test]
        public void AutomaticTargetResolver_RemainsAvailable()
        {
            Type resolverType = Type.GetType("CombatSystem.SkillTargetResolver, Assembly-CSharp");
            Assert.That(resolverType, Is.Not.Null, "自动目标解析器必须保留。");

            MethodInfo resolveMethod = resolverType.GetMethod(
                "TryResolve",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(resolveMethod, Is.Not.Null, "自动目标解析器必须提供 TryResolve。");
        }

        private static void AssertProjectDoesNotContain(IReadOnlyList<string> markers)
        {
            List<string> violations = new();
            foreach (string filePath in EnumerateProjectFiles())
            {
                string relativePath = ToProjectRelativePath(filePath);
                AddMarkerViolations(relativePath, markers, violations);

                if (IsTextFile(filePath))
                {
                    AddMarkerViolations(File.ReadAllText(filePath), markers, violations, relativePath);
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "发现禁止的旧标识:\n" + string.Join(Environment.NewLine, violations));
        }

        private static IEnumerable<string> EnumerateProjectFiles()
        {
            string assetsPath = Application.dataPath;
            foreach (string filePath in Directory.EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = ToProjectRelativePath(filePath);
                if (!IsIgnoredPath(relativePath))
                {
                    yield return filePath;
                }
            }
        }

        private static void AddMarkerViolations(
            string value,
            IReadOnlyList<string> markers,
            ICollection<string> violations,
            string sourcePath = null)
        {
            for (int index = 0; index < markers.Count; index++)
            {
                if (!value.Contains(markers[index], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string violation = sourcePath == null
                    ? value
                    : sourcePath + " contains " + markers[index];
                if (!violations.Contains(violation))
                {
                    violations.Add(violation);
                }
            }
        }

        private static bool IsIgnoredPath(string relativePath)
        {
            return relativePath.StartsWith("Assets/Learn/", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith("Assets/_Project/Tests/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            for (int index = 0; index < TextFileExtensions.Length; index++)
            {
                if (extension.Equals(TextFileExtensions[index], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            string projectRoot = NormalizePath(Directory.GetParent(Application.dataPath).FullName);
            string normalizedPath = NormalizePath(absolutePath);
            return normalizedPath.Substring(projectRoot.Length + 1);
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string NormalizePath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }
}
