using System.Collections.Generic;
using Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using VContainer;
using VContainer.Unity;

namespace Play.TargetMark
{
    /// <summary>为目标队列租借世界空间头顶标记，并维护目标序号。</summary>
    [ExecuteAlways]
    public sealed class MarkedTargetMarkerView : MonoBehaviour
    {
        /// <summary>保存单个目标的标记实例、图形和当前队列序号。</summary>
        private sealed class Marker
        {
            public readonly GameObject Instance;
            public readonly Image Image;
            public readonly TMP_Text Number;
            public int QueueIndex;

            /// <summary>记录池化实例对应的图标与序号文本。</summary>
            public Marker(GameObject instance, Image image, TMP_Text number)
            {
                Instance = instance;
                Image = image;
                Number = number;
            }
        }

        private const int MaximumRetainedMarkerCount = 128;
        private static readonly PoolSettings MarkerPoolSettings = new(0, MaximumRetainedMarkerCount);

        [SerializeField] private RectTransform root;
        [SerializeField] private GameObject imagePrefab;

        [Inject] private MarkedTargetQueueService _queueService;
        [Inject] private IGameObjectPool _pool;
        [Inject] private IObjectResolver _resolver;
        [Inject] private Camera _camera;

        private readonly Dictionary<IDamageable, Marker> _markers = new();
        private readonly List<IDamageable> _releaseBuffer = new();
        private readonly HashSet<IDamageable> _queuedTargets = new();
        private bool _subscribed;

        /// <summary>依赖注入后订阅队列变化并同步已有目标。</summary>
        private void Start()
        {
            SubscribeAndSync();
        }

        /// <summary>跟随队列与目标状态更新标记位置和面向方向。</summary>
        private void LateUpdate()
        {
            SynchronizeMarkers();
            if (!CanShowMarkers())
            {
                return;
            }

            foreach (KeyValuePair<IDamageable, Marker> entry in _markers)
            {
                ILockable target = (ILockable)entry.Key;
                // Enemy 当前用 root.position + Vector3.up * 2f 提供锚点；模型根节点对齐仍需场景验证。
                Vector3 anchorPoint = target.LockAnchorPoint;
                entry.Value.Instance.transform.SetPositionAndRotation(
                    anchorPoint,
                    GetBillboardRotation(anchorPoint));
            }
        }

        /// <summary>管理宿主停用时退订、归还标记并清理专属对象池。</summary>
        private void OnDisable()
        {
            Unsubscribe();
            ReleaseAll();
            ClearPool();
        }

        /// <summary>销毁时归还实例并清理此视图使用的对象池。</summary>
        private void OnDestroy()
        {
            Unsubscribe();
            ReleaseAll();
            ClearPool();
        }

        /// <summary>重新启用宿主时恢复队列订阅和标记。</summary>
        private void OnEnable()
        {
            SubscribeAndSync();
        }

        /// <summary>响应队列变化，立即租还目标标记并更新序号。</summary>
        private void HandleTargetsChanged()
        {
            SynchronizeMarkers();
        }

        /// <summary>订阅队列事件并同步当前队列内容。</summary>
        private void SubscribeAndSync()
        {
            if (_queueService == null)
            {
                return;
            }

            if (!_subscribed)
            {
                _queueService.TargetsChanged += HandleTargetsChanged;
                _subscribed = true;
            }

            if (CanShowMarkers())
            {
                WorldCanvas.worldCamera = _camera;
            }

            SynchronizeMarkers();
        }

        /// <summary>解除队列事件订阅。</summary>
        private void Unsubscribe()
        {
            if (!_subscribed || _queueService == null)
            {
                return;
            }

            _queueService.TargetsChanged -= HandleTargetsChanged;
            _subscribed = false;
        }

        /// <summary>按当前存活目标队列租还标记并分配连续序号。</summary>
        private void SynchronizeMarkers()
        {
            if (!CanShowMarkers())
            {
                ReleaseAll();
                return;
            }

            IReadOnlyList<IDamageable> targets = _queueService.Targets;
            _queuedTargets.Clear();
            for (int index = 0; index < targets.Count; index++)
            {
                _queuedTargets.Add(targets[index]);
            }

            _releaseBuffer.Clear();
            foreach (KeyValuePair<IDamageable, Marker> marker in _markers)
            {
                if (!_queuedTargets.Contains(marker.Key) || !TryGetLockable(marker.Key, out _, out _))
                {
                    _releaseBuffer.Add(marker.Key);
                }
            }

            for (int index = 0; index < _releaseBuffer.Count; index++)
            {
                ReleaseMarker(_releaseBuffer[index]);
            }

            _releaseBuffer.Clear();
            int queueIndex = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable target = targets[index];
                if (!TryGetLockable(target, out _, out _))
                {
                    continue;
                }

                queueIndex++;
                if (!_markers.TryGetValue(target, out Marker marker))
                {
                    marker = RentMarker();
                    if (marker == null)
                    {
                        continue;
                    }

                    _markers.Add(target, marker);
                }

                SetQueueIndex(marker, queueIndex);
            }
        }

        /// <summary>租借并校验一个共享 Canvas 下的标记 widget。</summary>
        private Marker RentMarker()
        {
            GameObject instance = _pool.Rent(
                imagePrefab,
                MarkerPoolSettings,
                MarkerRoot,
                true,
                InstantiateMarker);
            if (instance == null)
            {
                return null;
            }

            RectTransform rectTransform = instance.GetComponent<RectTransform>();
            Image image = instance.GetComponent<Image>();
            TMP_Text number = instance.GetComponentInChildren<TMP_Text>(true);
            if (rectTransform == null || instance.GetComponentInChildren<Canvas>(true) != null ||
                image == null || number == null)
            {
                _pool.Return(instance);
                return null;
            }

            image.enabled = true;
            image.raycastTarget = false;
            number.enabled = true;
            number.raycastTarget = false;
            return new Marker(instance, image, number);
        }

        /// <summary>通过场景容器实例化并注入标记 Prefab。</summary>
        private GameObject InstantiateMarker(GameObject prefab)
        {
            return _resolver.Instantiate(prefab);
        }

        /// <summary>释放队列中的单个目标标记。</summary>
        private void ReleaseMarker(IDamageable target)
        {
            if (!_markers.Remove(target, out Marker marker))
            {
                return;
            }

            if (marker.Image != null)
            {
                marker.Image.enabled = false;
            }

            _pool?.Return(marker.Instance);
        }

        /// <summary>释放当前视图持有的全部标记。</summary>
        private void ReleaseAll()
        {
            _releaseBuffer.Clear();
            foreach (IDamageable target in _markers.Keys)
            {
                _releaseBuffer.Add(target);
            }

            for (int index = 0; index < _releaseBuffer.Count; index++)
            {
                ReleaseMarker(_releaseBuffer[index]);
            }

            _releaseBuffer.Clear();
        }

        /// <summary>清理仅由当前视图使用的标记预制体对象池。</summary>
        private void ClearPool()
        {
            if (_pool != null && imagePrefab != null)
            {
                _pool.Clear(imagePrefab);
            }
        }

        /// <summary>检查目标仍存活、可锁定且带有有效 Transform。</summary>
        private static bool TryGetLockable(
            IDamageable target,
            out ILockable lockable,
            out Transform targetTransform)
        {
            lockable = target as ILockable;
            targetTransform = null;
            if (!target.IsAliveAndValid() || lockable == null || !lockable.IsLockable)
            {
                return false;
            }

            targetTransform = target.Transform;
            return targetTransform != null;
        }

        /// <summary>只在队列位置变化时更新 TMP 序号。</summary>
        private static void SetQueueIndex(Marker marker, int queueIndex)
        {
            if (marker.QueueIndex == queueIndex)
            {
                return;
            }

            marker.QueueIndex = queueIndex;
            marker.Number.text = queueIndex.ToString();
        }

        /// <summary>以相机位置和上方向计算正面朝向相机的 billboard 旋转。</summary>
        private Quaternion GetBillboardRotation(Vector3 position)
        {
            Vector3 towardCamera = _camera.transform.position - position;
            if (towardCamera.sqrMagnitude <= Mathf.Epsilon)
            {
                return Quaternion.LookRotation(-_camera.transform.forward, _camera.transform.up);
            }

            return Quaternion.LookRotation(towardCamera, _camera.transform.up);
        }

        /// <summary>检查依赖、widget Prefab 与共享 World Space Canvas 是否可用。</summary>
        private bool CanShowMarkers()
        {
            RectTransform host = MarkerRoot;
            Canvas canvas = WorldCanvas;
            return _queueService != null &&
                _pool != null &&
                _resolver != null &&
                _camera != null &&
                imagePrefab != null &&
                host != null &&
                canvas != null &&
                canvas.renderMode == RenderMode.WorldSpace &&
                host.gameObject.activeInHierarchy;
        }

        /// <summary>读取由场景注入的共享 World Space Canvas 根节点。</summary>
        private RectTransform MarkerRoot => root;

        /// <summary>读取管理根节点所属的共享 Canvas。</summary>
        private Canvas WorldCanvas => root != null ? root.GetComponentInParent<Canvas>() : null;
    }
}
