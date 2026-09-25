using System.Collections.Generic;
using UnityEngine;
using _Project.UI.Tutorial.View;

namespace _Project.UI.Tutorial.Implement
{
    /// <summary>维护当前场景教程目标 ID 到锚点的映射。</summary>
    public sealed class TutorialTargetRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, TutorialAnchor> _anchors = new();

        /// <summary>场景启用时收集所有包括非激活节点在内的目标锚点。</summary>
        private void Awake()
        {
            Rebuild();
        }

        /// <summary>根据配置 ID 查找当前场景的教程目标。</summary>
        /// <param name="anchorId">教程步骤中的目标 ID。</param>
        /// <param name="anchor">找到的目标锚点。</param>
        /// <returns>找到有效目标时返回 true。</returns>
        public bool TryGet(string anchorId, out TutorialAnchor anchor)
        {
            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchor = null;
                return false;
            }

            if (!_anchors.TryGetValue(anchorId, out anchor) ||
                anchor == null ||
                !anchor.isActiveAndEnabled ||
                anchor.RectTransform == null)
            {
                Rebuild();
            }

            if (!_anchors.TryGetValue(anchorId, out anchor) ||
                anchor == null ||
                !anchor.isActiveAndEnabled ||
                anchor.RectTransform == null)
            {
                anchor = null;
                return false;
            }

            return true;
        }

        /// <summary>重新扫描场景锚点，供动态启用目标后刷新映射。</summary>
        public void Rebuild()
        {
            _anchors.Clear();
            TutorialAnchor[] anchors = GetComponentsInChildren<TutorialAnchor>(true);
            for (int index = 0; index < anchors.Length; index++)
            {
                TutorialAnchor anchor = anchors[index];
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.AnchorId))
                {
                    continue;
                }

                if (!_anchors.TryAdd(anchor.AnchorId, anchor))
                {
                    Debug.LogWarning(
                        $"[TutorialTargetRegistry] 目标 ID 重复: {anchor.AnchorId}",
                        anchor);
                }
            }
        }
    }
}
