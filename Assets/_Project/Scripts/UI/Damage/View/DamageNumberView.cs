using TMPro;
using UnityEngine;

namespace _Project.UI.Damage
{
    /// <summary>单个伤害跳字的 UGUI 视图；实例由集中对象池管理。</summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rectTransform;

        /// <summary>视图的屏幕空间 RectTransform。</summary>
        internal RectTransform RectTransform
        {
            get
            {
                rectTransform ??= transform as RectTransform;
                return rectTransform;
            }
        }

        /// <summary>获取并校验视图所需的 TMP 和 CanvasGroup。</summary>
        internal bool IsReady
        {
            get
            {
                label ??= GetComponentInChildren<TextMeshProUGUI>(true);
                canvasGroup ??= GetComponent<CanvasGroup>();
                return label != null && canvasGroup != null && RectTransform != null;
            }
        }

        /// <summary>写入伤害数值和暴击视觉尺寸。</summary>
        internal void SetValue(float damage, DamageNumberPriority priority)
        {
            if (!IsReady)
            {
                return;
            }

            label.SetText("{0:0}", Mathf.Max(0f, damage));
            RectTransform.localScale = priority == DamageNumberPriority.Critical
                ? Vector3.one * 1.25f
                : Vector3.one;
        }

        /// <summary>更新跳字的位置和透明度。</summary>
        internal void SetPose(Vector2 localPosition, float alpha)
        {
            if (!IsReady)
            {
                return;
            }

            RectTransform.anchoredPosition = localPosition;
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>清除对象池归还前的视图状态。</summary>
        internal void ResetView()
        {
            if (!IsReady)
            {
                return;
            }

            label.SetText(string.Empty);
            canvasGroup.alpha = 0f;
            RectTransform.localScale = Vector3.one;
        }
    }
}
