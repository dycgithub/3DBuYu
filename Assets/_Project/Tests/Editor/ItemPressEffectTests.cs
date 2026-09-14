using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证物品按压动效只作用于视觉节点，不改变网格命中区域和定位节点。
    /// </summary>
    public sealed class ItemPressEffectTests
    {
        private readonly List<Object> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            DOTween.Init();
            DOTween.KillAll();
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            for (int index = _createdObjects.Count - 1; index >= 0; index--)
            {
                if (_createdObjects[index] != null)
                    Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void PressScalesAndSinksVisual()
        {
            CreateItem(out ItemPressEffect effect, out RectTransform hitArea, out RectTransform visualRoot);
            Vector3 hitScale = hitArea.localScale;
            Vector2 hitPosition = hitArea.anchoredPosition;
            Vector3 baseScale = visualRoot.localScale;
            Vector2 basePosition = visualRoot.anchoredPosition;

            effect.Press();
            DOTween.CompleteAll();

            Assert.That(effect.IsPressed, Is.True);
            Assert.That(visualRoot.localScale.x, Is.LessThan(baseScale.x));
            Assert.That(visualRoot.localScale.y, Is.LessThan(baseScale.y));
            Assert.That(visualRoot.anchoredPosition.y, Is.LessThan(basePosition.y));
            Assert.That(hitArea.localScale, Is.EqualTo(hitScale));
            Assert.That(hitArea.anchoredPosition, Is.EqualTo(hitPosition));
        }

        [Test]
        public void ReleasePopsAndReturns()
        {
            CreateItem(out ItemPressEffect effect, out RectTransform hitArea, out RectTransform visualRoot);
            Vector3 hitScale = hitArea.localScale;
            Vector2 hitPosition = hitArea.anchoredPosition;
            Vector3 baseScale = visualRoot.localScale;
            Vector2 basePosition = visualRoot.anchoredPosition;

            effect.Press();
            DOTween.CompleteAll();
            effect.Release();
            DOTween.ManualUpdate(0.09f, 0.09f);

            Assert.That(effect.IsPressed, Is.False);
            Assert.That(visualRoot.localScale.x, Is.GreaterThan(baseScale.x));
            Assert.That(visualRoot.localScale.y, Is.GreaterThan(baseScale.y));
            Assert.That(visualRoot.anchoredPosition.y, Is.GreaterThan(basePosition.y));
            Assert.That(hitArea.localScale, Is.EqualTo(hitScale));
            Assert.That(hitArea.anchoredPosition, Is.EqualTo(hitPosition));

            DOTween.CompleteAll();

            Assert.That(visualRoot.localScale, Is.EqualTo(baseScale));
            Assert.That(visualRoot.anchoredPosition, Is.EqualTo(basePosition));
        }

        [Test]
        public void CancelRestoresVisual()
        {
            CreateItem(out ItemPressEffect effect, out _, out RectTransform visualRoot);
            Vector3 baseScale = visualRoot.localScale;
            Vector2 basePosition = visualRoot.anchoredPosition;

            effect.Press();
            effect.Cancel();

            Assert.That(effect.IsPressed, Is.False);
            Assert.That(visualRoot.localScale, Is.EqualTo(baseScale));
            Assert.That(visualRoot.anchoredPosition, Is.EqualTo(basePosition));
        }

        [Test]
        public void DisableRestoresVisual()
        {
            CreateItem(out ItemPressEffect effect, out _, out RectTransform visualRoot);
            GameObject itemObject = visualRoot.parent.gameObject;

            effect.Press();
            itemObject.SetActive(false);

            Assert.That(effect.IsPressed, Is.False);
            Assert.That(visualRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(visualRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ItemViewForwardsPress()
        {
            CreateItem(out ItemPressEffect effect, out _, out _);
            ItemView itemView = effect.gameObject.AddComponent<ItemView>();

            Assert.That(itemView, Is.AssignableTo<IPointerDownHandler>());
            Assert.That(itemView, Is.AssignableTo<IPointerUpHandler>());

            ((IPointerDownHandler)itemView).OnPointerDown(null);
            DOTween.CompleteAll();
            Assert.That(effect.IsPressed, Is.True);

            ((IPointerUpHandler)itemView).OnPointerUp(null);
            Assert.That(effect.IsPressed, Is.False);
        }

        [Test]
        public void PrefabHasVisualRoot()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/ItemPrefab.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<ItemPressEffect>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("VisualRoot"), Is.Not.Null);
            Assert.That(prefab.GetComponent<ItemPressEffect>().VisualRoot, Is.Not.Null);
        }

        private void CreateItem(
            out ItemPressEffect effect,
            out RectTransform hitArea,
            out RectTransform visualRoot)
        {
            GameObject itemObject = new("Item", typeof(RectTransform), typeof(Image));
            _createdObjects.Add(itemObject);
            hitArea = itemObject.GetComponent<RectTransform>();
            hitArea.anchoredPosition = new Vector2(60f, -80f);
            hitArea.localScale = new Vector3(1.2f, 0.9f, 1f);

            GameObject visualObject = new("VisualRoot", typeof(RectTransform));
            _createdObjects.Add(visualObject);
            visualObject.transform.SetParent(itemObject.transform, false);
            visualRoot = visualObject.GetComponent<RectTransform>();
            visualRoot.localScale = Vector3.one;
            visualRoot.anchoredPosition = Vector2.zero;

            effect = itemObject.AddComponent<ItemPressEffect>();
        }
    }
}
