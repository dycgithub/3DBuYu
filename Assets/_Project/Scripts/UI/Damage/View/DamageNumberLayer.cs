using UnityEngine;

namespace _Project.UI.Damage
{
    /// <summary>伤害跳字使用的场景级 UGUI 层；不在运行时创建 Canvas。</summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberLayer : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform root;
        [SerializeField] private DamageNumberView viewPrefab;
        [SerializeField] private Camera worldCamera;

        /// <summary>承载所有跳字的 Canvas。</summary>
        internal Canvas Canvas => canvas != null ? canvas : GetComponentInParent<Canvas>();

        /// <summary>跳字的屏幕空间根节点。</summary>
        internal RectTransform Root => root != null ? root : transform as RectTransform;

        /// <summary>由 Inspector 注入的 TMP 跳字预制体。</summary>
        internal DamageNumberView ViewPrefab => viewPrefab;

        /// <summary>用于世界坐标投影的游戏相机。</summary>
        internal Camera WorldCamera => worldCamera != null ? worldCamera : Camera.main;
    }
}
