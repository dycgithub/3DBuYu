using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证积分飞行效果的透明度控制和装饰光点均由预制体提供。
    /// </summary>
    public sealed class PointsFlyEffectPrefabTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/PointsFlyEffect.prefab";

        [Test]
        public void Prefab_ContainsCanvasGroupAndLightSpots()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Image>(true), Has.Length.GreaterThanOrEqualTo(2));
        }
    }
}
