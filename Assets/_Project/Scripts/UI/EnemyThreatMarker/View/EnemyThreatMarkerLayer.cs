using UnityEngine;

namespace _Project.UI.EnemyThreatMarker
{
    /// <summary>提供敌人威胁标记使用的场景 UGUI 层与可替换图片预制体。</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyThreatMarkerLayer : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform root;
        [SerializeField] private GameObject imagePrefab;
        [SerializeField] private Camera worldCamera;
        [SerializeField, Min(0f)] private float enterDistance = 4f;
        [SerializeField, Min(0f)] private float exitDistance = 4.5f;
        [SerializeField, Min(1)] private int maximumRetainedImages = 128;

        /// <summary>标记图片所在的场景 Canvas。</summary>
        internal Canvas Canvas => canvas != null ? canvas : GetComponentInParent<Canvas>();

        /// <summary>标记图片使用的 RectTransform 根节点。</summary>
        internal RectTransform Root => root != null ? root : transform as RectTransform;

        /// <summary>可替换的标记 Image 预制体。</summary>
        internal GameObject ImagePrefab => imagePrefab;

        /// <summary>将世界坐标转换为 Canvas 本地坐标并更新图片可见性。</summary>
        internal void SetImagePosition(UnityEngine.UI.Image image, Vector3 worldPosition)
        {
            Canvas currentCanvas = Canvas;
            RectTransform currentRoot = Root;
            Camera currentCamera = WorldCamera;
            if (image == null || currentCanvas == null || currentRoot == null || currentCamera == null)
            {
                if (image != null)
                {
                    image.enabled = false;
                }

                return;
            }

            Vector3 screenPosition = currentCamera.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0f || !currentCamera.pixelRect.Contains(screenPosition))
            {
                image.enabled = false;
                return;
            }

            Camera canvasCamera = currentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : currentCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    currentRoot,
                    screenPosition,
                    canvasCamera,
                    out Vector2 localPosition))
            {
                image.enabled = false;
                return;
            }

            RectTransform imageRect = image.rectTransform;
            Vector2 anchorPosition = currentRoot.rect.min +
                Vector2.Scale(currentRoot.rect.size, imageRect.anchorMin);
            imageRect.anchoredPosition = localPosition - anchorPosition;
            image.enabled = true;
        }

        /// <summary>读取标记进入半径。</summary>
        internal float EnterDistance => Mathf.Max(0f, enterDistance);

        /// <summary>读取不小于进入半径的标记退出半径。</summary>
        internal float ExitDistance => Mathf.Max(EnterDistance, exitDistance);

        /// <summary>读取对象池保留的图片上限。</summary>
        internal int MaximumRetainedImages => Mathf.Max(1, maximumRetainedImages);

        /// <summary>读取用于三维投影的场景相机。</summary>
        private Camera WorldCamera => worldCamera != null ? worldCamera : Camera.main;

        /// <summary>编辑器修改范围时保持滞回阈值顺序有效。</summary>
        private void OnValidate()
        {
            enterDistance = Mathf.Max(0f, enterDistance);
            exitDistance = Mathf.Max(enterDistance, exitDistance);
            maximumRetainedImages = Mathf.Max(1, maximumRetainedImages);
        }
    }
}
