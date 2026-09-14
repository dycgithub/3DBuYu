using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Utils;

namespace _Project.UI.Common
{
    /// <summary>
    /// 将一个积分数值从世界坐标飞向积分栏，结束后交还共享对象池。
    /// <para>轨迹、淡出和预制体提供的光点均由 DOTween 驱动，不在运行时创建 UI。</para>
    /// </summary>
    public sealed class PointsFlyEffectView : MonoBehaviour, IPooledObject
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private CanvasGroup _group;
        [SerializeField, Min(0.01f)] private float _duration = 0.55f;
        [SerializeField, Min(0f)] private float _arcHeight = 48f;

        private RectTransform _rectTransform;
        private CanvasGroup[] _lightGroups = Array.Empty<CanvasGroup>();
        private RectTransform _effectRoot;
        private RectTransform _target;
        private Camera _worldCamera;
        private Camera _canvasCamera;
        private Vector2 _startPosition;
        private Vector2 _targetPosition;
        private float _progress;
        private Sequence _sequence;
        private Action _completed;

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
            _label ??= GetComponentInChildren<TextMeshProUGUI>(true);
            _group ??= GetComponent<CanvasGroup>();

            CanvasGroup[] groups = GetComponentsInChildren<CanvasGroup>(true);
            _lightGroups = new CanvasGroup[Mathf.Max(0, groups.Length - (_group != null ? 1 : 0))];
            int lightIndex = 0;
            for (int index = 0; index < groups.Length; index++)
            {
                if (groups[index] != _group)
                {
                    _lightGroups[lightIndex++] = groups[index];
                }
            }
        }

        /// <summary>
        /// 初始化一次飞行并将世界坐标转换到 UI 根节点的局部坐标。
        /// </summary>
        internal void Play(
            int points,
            Vector3 worldPosition,
            RectTransform effectRoot,
            RectTransform target,
            Camera worldCamera,
            Camera canvasCamera,
            Action completed)
        {
            _effectRoot = effectRoot;
            _target = target;
            _worldCamera = worldCamera;
            _canvasCamera = canvasCamera;
            _completed = completed;
            _progress = 0f;
            KillSequence();

            if (_label != null)
            {
                _label.text = $"+{points}";
            }

            if (_rectTransform == null || _effectRoot == null || _target == null)
            {
                Finish();
                return;
            }

            Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(
                _worldCamera,
                worldPosition);
            Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(
                _canvasCamera,
                _target.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _effectRoot,
                    startScreen,
                    _canvasCamera,
                    out _startPosition) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _effectRoot,
                    targetScreen,
                    _canvasCamera,
                    out _targetPosition))
            {
                Finish();
                return;
            }

            _rectTransform.anchoredPosition = _startPosition;
            _rectTransform.localScale = Vector3.one * 0.8f;
            if (_group != null)
            {
                _group.alpha = 1f;
            }

            for (int index = 0; index < _lightGroups.Length; index++)
            {
                _lightGroups[index].alpha = 0f;
                _lightGroups[index].transform.localScale = Vector3.one * 0.5f;
            }

            CreateSequence();
        }

        /// <summary>从对象池租出时清理上一轮动画状态。</summary>
        public void OnRentFromPool()
        {
            KillSequence();
            _completed = null;
        }

        /// <summary>归还对象池时终止动画，避免保留外部回调。</summary>
        public void OnReturnToPool()
        {
            KillSequence();
            _completed = null;
        }

        private void CreateSequence()
        {
            float duration = Mathf.Max(0.01f, _duration);
            _sequence = DOTween.Sequence().SetUpdate(true);
            _sequence.Join(DOTween.To(
                () => _progress,
                progress =>
                {
                    _progress = progress;
                    _rectTransform.anchoredPosition = PointsFlyEffectMath.CalculatePosition(
                        _startPosition,
                        _targetPosition,
                        progress,
                        _arcHeight);
                },
                1f,
                duration).SetEase(Ease.OutCubic));
            _sequence.Join(_rectTransform.DOScale(Vector3.one, duration).SetEase(Ease.OutBack));

            if (_group != null)
            {
                _sequence.Insert(
                    duration * 0.68f,
                    _group.DOFade(0f, duration * 0.32f));
            }

            for (int index = 0; index < _lightGroups.Length; index++)
            {
                float delay = duration * (0.08f + index * 0.08f);
                _sequence.Insert(delay, _lightGroups[index].DOFade(0.85f, duration * 0.22f));
                _sequence.Insert(
                    delay,
                    _lightGroups[index].transform.DOScale(Vector3.one, duration * 0.35f)
                        .SetEase(Ease.OutBack));
                _sequence.Insert(
                    delay + duration * 0.45f,
                    _lightGroups[index].DOFade(0f, duration * 0.35f));
            }

            _sequence.OnComplete(Finish);
        }

        private void Finish()
        {
            KillSequence();
            Action completed = _completed;
            _completed = null;
            completed?.Invoke();
        }

        private void KillSequence()
        {
            _sequence?.Kill();
            _sequence = null;
        }
    }
}
