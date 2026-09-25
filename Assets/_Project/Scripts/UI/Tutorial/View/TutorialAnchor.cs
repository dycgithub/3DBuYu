using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _Project.UI.Tutorial.View
{
    /// <summary>为教程提供一个场景内可定位、可点击的 UI 目标。</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialAnchor : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private string _anchorId;

        /// <summary>教程配置使用的目标标识。</summary>
        public string AnchorId => _anchorId;

        /// <summary>目标区域的 RectTransform。</summary>
        public RectTransform RectTransform => transform as RectTransform;

        /// <summary>目标被点击时触发。</summary>
        public event Action Clicked;

        /// <summary>仅转发左键或主指针点击，不改变目标原有 Button 行为。</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke();
            }
        }
    }
}
