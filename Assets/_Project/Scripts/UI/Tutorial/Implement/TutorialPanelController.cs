using DG.Tweening;
using UnityEngine;
using _Project.UI.Tutorial.Model;
using _Project.UI.Tutorial.View;

namespace _Project.UI.Tutorial.Implement
{
    /// <summary>编排教程流程、目标点击、遮罩和可拖动弹窗。</summary>
    public sealed class TutorialPanelController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private RectTransform _tutorialRoot;
        [SerializeField] private TutorialTargetRegistry _targetRegistry;
        [SerializeField] private TutorialOverlayView _overlay;
        [SerializeField] private TutorialDialogView _dialog;

        [Header("关闭")]
        [SerializeField, Min(0f)] private float _closeDuration = 0.35f;

        [Header("目标重试")]
        [SerializeField, Min(0.05f)] private float _targetRetryInterval = 0.1f;

        private readonly TutorialFlow _flow = new();
        private TutorialAnchor _activeAnchor;
        private Tween _deactivateTween;
        private int _activeStepCount;
        private float _nextTargetRetryTime;
        private bool _isOpen;

        /// <summary>当前是否存在正在显示的教程。</summary>
        public bool IsOpen => _isOpen;

        /// <summary>绑定场景子节点和流程事件。</summary>
        private void Awake()
        {
            _tutorialRoot ??= transform.Find("TutorialRoot") as RectTransform;
            _targetRegistry ??= GetComponent<TutorialTargetRegistry>();
            _overlay ??= GetComponentInChildren<TutorialOverlayView>(true);
            _dialog ??= GetComponentInChildren<TutorialDialogView>(true);

            _flow.StepChanged += HandleStepChanged;
            _flow.Completed += Close;
            if (_dialog != null)
            {
                _dialog.NextPressed += HandleNextPressed;
                _dialog.ClosePressed += Close;
            }
        }

        /// <summary>场景销毁时清理事件和动画。</summary>
        private void OnDestroy()
        {
            _flow.StepChanged -= HandleStepChanged;
            _flow.Completed -= Close;
            if (_dialog != null)
            {
                _dialog.NextPressed -= HandleNextPressed;
                _dialog.ClosePressed -= Close;
            }
            UnsubscribeAnchor();
            _deactivateTween?.Kill();
        }

        /// <summary>打开指定教程资产并从第一步开始。</summary>
        /// <param name="sequence">可配置的教程步骤集合。</param>
        public void Open(TutorialSequenceSO sequence)
        {
            if (sequence == null || sequence.Steps == null || sequence.Steps.Count == 0)
            {
                Debug.LogWarning("[TutorialPanelController] 教程为空，无法打开。", this);
                return;
            }

            Close();
            _deactivateTween?.Kill();
            if (_tutorialRoot == null)
            {
                Debug.LogError("[TutorialPanelController] 未绑定 TutorialRoot。", this);
                return;
            }

            _tutorialRoot.gameObject.SetActive(true);
            _dialog?.ResetPosition();
            _activeStepCount = sequence.Steps.Count;
            _nextTargetRetryTime = 0f;
            _isOpen = _flow.Open(sequence.Steps);
        }

        /// <summary>关闭当前教程并在动画结束后隐藏根节点。</summary>
        public void Close()
        {
            if (!_isOpen && _flow.State == TutorialFlowState.Closed)
            {
                return;
            }

            _isOpen = false;
            UnsubscribeAnchor();
            _flow.Close();
            _activeStepCount = 0;
            _dialog?.ClearForbiddenRect();
            _dialog?.Hide();
            _overlay?.Hide();
            _deactivateTween?.Kill();

            if (_tutorialRoot == null || !_tutorialRoot.gameObject.activeSelf)
            {
                return;
            }

            _deactivateTween = DOVirtual.DelayedCall(
                    Mathf.Max(0f, _closeDuration),
                    DeactivateRoot)
                .SetUpdate(true);
        }

        /// <summary>等待运行时出现或重新启用当前教程目标。</summary>
        private void Update()
        {
            if (!_isOpen || _flow.CurrentStep == null ||
                _flow.CurrentStep.FocusMode != TutorialFocusMode.Target)
            {
                return;
            }

            if (_activeAnchor != null && _activeAnchor.isActiveAndEnabled)
            {
                if (_overlay != null && _overlay.Refresh(_flow.CurrentStep, _activeAnchor))
                {
                    _dialog?.Place(_flow.CurrentStep.PanelPlacement, _overlay.FocusRect,
                        TutorialFocusMode.Target);
                    _dialog?.SetForbiddenRect(_overlay.FocusRect);
                }
                return;
            }

            if (Time.unscaledTime < _nextTargetRetryTime)
            {
                return;
            }

            _nextTargetRetryTime = Time.unscaledTime + Mathf.Max(0.05f, _targetRetryInterval);
            bool targetAvailable = _targetRegistry != null &&
                                   _targetRegistry.TryGet(_flow.CurrentStep.TargetId, out _);
            if (targetAvailable || (_overlay != null && _overlay.HasFocus))
            {
                HandleStepChanged(_flow.CurrentStep, _flow.CurrentStepIndex);
            }
        }

        /// <summary>将当前步骤绑定到目标并更新两个表现视图。</summary>
        private void HandleStepChanged(TutorialStepData step, int index)
        {
            UnsubscribeAnchor();
            TutorialAnchor target = ResolveTarget(step);
            if (target != null)
            {
                _activeAnchor = target;
                _activeAnchor.Clicked += HandleTargetClicked;
            }

            _overlay?.Show(step, target);
            _dialog?.ShowStep(step, index, _flowStepCount);
            _dialog?.Place(step.PanelPlacement, _overlay != null ? _overlay.FocusRect : new Rect(),
                _overlay != null && _overlay.HasFocus
                    ? TutorialFocusMode.Target
                    : TutorialFocusMode.FullScreen);
            if (_overlay != null && _overlay.HasFocus)
            {
                _dialog?.SetForbiddenRect(_overlay.FocusRect);
            }
            else
            {
                _dialog?.ClearForbiddenRect();
            }
        }

        /// <summary>获取当前教程步骤总数。</summary>
        private int _flowStepCount => _activeStepCount;

        /// <summary>处理下一步按钮。</summary>
        private void HandleNextPressed()
        {
            _flow.Next();
        }

        /// <summary>处理高亮目标点击。</summary>
        private void HandleTargetClicked()
        {
            if (_overlay != null && !_overlay.IsReady)
            {
                return;
            }

            _flow.CompleteTarget();
        }

        /// <summary>按步骤目标 ID 查找当前场景锚点。</summary>
        private TutorialAnchor ResolveTarget(TutorialStepData step)
        {
            if (step == null || step.FocusMode != TutorialFocusMode.Target)
            {
                return null;
            }

            if (_targetRegistry == null || !_targetRegistry.TryGet(step.TargetId, out TutorialAnchor target))
            {
                Debug.LogWarning(
                    $"[TutorialPanelController] 找不到教程目标: {step.TargetId}",
                    this);
                return null;
            }

            return target;
        }

        /// <summary>取消上一步目标监听，避免步骤切换后重复推进。</summary>
        private void UnsubscribeAnchor()
        {
            if (_activeAnchor == null)
            {
                return;
            }

            _activeAnchor.Clicked -= HandleTargetClicked;
            _activeAnchor = null;
        }

        /// <summary>隐藏教程根节点并清理关闭动画引用。</summary>
        private void DeactivateRoot()
        {
            _deactivateTween = null;
            if (!_isOpen && _tutorialRoot != null)
            {
                _tutorialRoot.gameObject.SetActive(false);
            }
        }
    }
}
