using Services;
using Cinemachine;
using UnityEngine;
using Utils;
using VContainer;

namespace _Project.UI.Common
{
    /// <summary>
    /// 订阅敌人击杀积分事件，并通过共享对象池创建积分飞行提示。
    /// </summary>
    public sealed class PointsFlyEffectPresenter : MonoBehaviour
    {
        [SerializeField] private PointsFlyEffectView _effectPrefab;
        [SerializeField] private RectTransform _effectRoot;
        [SerializeField] private RectTransform _pointsTarget;
        [SerializeField] private Camera _worldCamera;
        [SerializeField, Min(0)] private int _initialCapacity = 4;
        [SerializeField, Min(1)] private int _maximumRetained = 12;

        [Inject] private IPointsFlyEffectService _pointsFlyEffect;
        [Inject] private IGameObjectPool _pool;
        private bool _subscribed;

        private void Start()
        {
            if (_pointsFlyEffect == null || _pool == null || _effectPrefab == null ||
                _effectRoot == null || _pointsTarget == null)
            {
                Debug.LogWarning("[PointsFlyEffectPresenter] 积分飞行表现未完整配置。", this);
                return;
            }

            _pointsFlyEffect.PointsAwarded += HandleEnemyKilled;
            _subscribed = true;
        }

        private void OnDestroy()
        {
            if (_subscribed && _pointsFlyEffect != null)
            {
                _pointsFlyEffect.PointsAwarded -= HandleEnemyKilled;
            }
        }

        private void HandleEnemyKilled(int points, Vector3 worldPosition)
        {
            if (points <= 0)
            {
                return;
            }

            GameObject instance = _pool.Rent(
                _effectPrefab.gameObject,
                new PoolSettings(_initialCapacity, Mathf.Max(_initialCapacity, _maximumRetained)),
                _effectRoot);
            if (instance == null)
            {
                return;
            }

            PointsFlyEffectView view = instance.GetComponent<PointsFlyEffectView>();
            if (view == null)
            {
                _pool.Return(instance);
                return;
            }

            view.Play(
                points,
                worldPosition,
                _effectRoot,
                _pointsTarget,
                ResolveWorldCamera(),
                _effectRoot.GetComponentInParent<Canvas>()?.worldCamera,
                () => _pool.Return(instance));
        }

        private Camera ResolveWorldCamera()
        {
            if (_worldCamera != null)
            {
                return _worldCamera;
            }

            CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
            return brain != null ? brain.GetComponent<Camera>() : Camera.main;
        }
    }
}
