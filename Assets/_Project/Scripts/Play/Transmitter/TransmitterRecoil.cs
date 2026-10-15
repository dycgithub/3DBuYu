using UnityEngine;

namespace Play
{
    /// <summary>通过模型 Prefab 的可调参数呈现局部后坐位移，不改变 FirePoint。</summary>
    public sealed class TransmitterRecoil : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.1f)]
        [Tooltip("后退距离占模型长度的比例；0.015 表示 1.5%。")]
        private float _modelLengthFraction = 0.015f;

        [SerializeField, Min(0.001f)]
        [Tooltip("后退到最大偏移所需的秒数。")]
        private float _kickDuration = 0.02f;

        [SerializeField, Min(0.001f)]
        [Tooltip("从后坐偏移复原所需的秒数。")]
        private float _returnDuration = 0.05f;

        private Transform _model;
        private Vector3 _basePosition;
        private Vector3 _startOffset;
        private Vector3 _offset;
        private Vector3 _targetOffset;
        private float _elapsed;
        private float _duration;
        private Renderer[] _renderers;
        private bool _kicking;
        private bool _active;

        /// <summary>缓存模型渲染器；长度比例按发射方向上的世界空间模型跨度解释。</summary>
        private void Awake()
        {
            _model = transform;
            _basePosition = _model.localPosition;
            _renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>预挂组件的 Awake 早于端口归位，首次更新前重新记录完成归位后的模型位置。</summary>
        private void Start()
        {
            _basePosition = _model.localPosition;
        }

        /// <summary>让模型沿发射方向的反方向回弹；重复触发时偏移保持有界。</summary>
        /// <param name="launchDirection">本次攻击的世界发射方向。</param>
        public void Kick(Vector3 launchDirection)
        {
            if (launchDirection.sqrMagnitude <= Mathf.Epsilon || _renderers.Length == 0)
            {
                return;
            }

            float distance = GetModelLength(launchDirection.normalized) * _modelLengthFraction;
            Vector3 localRecoil = _model.parent != null
                ? _model.parent.InverseTransformVector(-launchDirection.normalized * distance)
                : -launchDirection.normalized * distance;
            _startOffset = _offset;
            _targetOffset = ClampWorldOffset(_offset + localRecoil, distance);
            _elapsed = 0f;
            _duration = _kickDuration;
            _kicking = true;
            _active = true;
        }

        /// <summary>按缩放帧时间推进后坐并平滑回到原位。</summary>
        private void Update()
        {
            if (!_active || Time.timeScale <= 0f)
            {
                return;
            }

            Step(Time.deltaTime);
        }

        /// <summary>按指定帧时长推进一次后坐状态。</summary>
        /// <param name="deltaTime">缩放后的帧时长。</param>
        private void Step(float deltaTime)
        {
            _elapsed += deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);

            if (_kicking)
            {
                _offset = Vector3.Lerp(_startOffset, _targetOffset, progress);
                _model.localPosition = _basePosition + _offset;
                if (progress >= 1f)
                {
                    _startOffset = _offset;
                    _targetOffset = Vector3.zero;
                    _elapsed = 0f;
                    _duration = _returnDuration;
                    _kicking = false;
                }

                return;
            }

            _offset = Vector3.Lerp(_startOffset, Vector3.zero, progress);
            _model.localPosition = _basePosition + _offset;
            if (progress >= 1f)
            {
                _offset = Vector3.zero;
                _startOffset = Vector3.zero;
                _model.localPosition = _basePosition;
                _active = false;
            }
        }

        /// <summary>将偏移限制在模型长度比例对应的世界空间半径内。</summary>
        /// <param name="localOffset">候选局部位移。</param>
        /// <param name="maxWorldDistance">允许的最大世界位移。</param>
        /// <returns>不超过世界空间上限的局部位移。</returns>
        private Vector3 ClampWorldOffset(Vector3 localOffset, float maxWorldDistance)
        {
            Vector3 worldOffset = _model.parent != null
                ? _model.parent.TransformVector(localOffset)
                : localOffset;
            if (worldOffset.magnitude > maxWorldDistance)
            {
                worldOffset = worldOffset.normalized * maxWorldDistance;
                return _model.parent != null
                    ? _model.parent.InverseTransformVector(worldOffset)
                    : worldOffset;
            }

            return localOffset;
        }

        /// <summary>计算所有模型渲染边界沿指定世界方向的投影跨度。</summary>
        /// <param name="worldDirection">单位世界方向。</param>
        /// <returns>模型沿该方向的世界空间跨度。</returns>
        private float GetModelLength(Vector3 worldDirection)
        {
            Bounds modelBounds = _renderers[0].bounds;
            for (int index = 1; index < _renderers.Length; index++)
            {
                modelBounds.Encapsulate(_renderers[index].bounds);
            }

            Vector3 absoluteDirection = new Vector3(
                Mathf.Abs(worldDirection.x),
                Mathf.Abs(worldDirection.y),
                Mathf.Abs(worldDirection.z));
            return Vector3.Dot(modelBounds.extents, absoluteDirection) * 2f;
        }

        /// <summary>恢复模型基准位置，避免禁用后保留偏移。</summary>
        private void OnDisable()
        {
            if (_model != null)
            {
                _model.localPosition = _basePosition;
            }

            _offset = Vector3.zero;
            _startOffset = Vector3.zero;
            _targetOffset = Vector3.zero;
            _elapsed = 0f;
            _kicking = false;
            _active = false;
        }
    }
}
