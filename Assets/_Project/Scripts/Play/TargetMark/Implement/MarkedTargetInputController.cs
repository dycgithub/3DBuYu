using System;
using CombatSystem;
using EnemySystem;
using Interfaces;
using Services;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Play.TargetMark
{
    /// <summary>在标记攻击模式下把鼠标输入转换为队列目标追加。</summary>
    public sealed class MarkedTargetInputController : MonoBehaviour
    {
        [Inject] private IInputService _input;
        [Inject] private ITransmitterAttackModeProvider _attackModeProvider;
        [Inject] private MarkedTargetQueueService _queueService;
        [Inject] private PhysicsCentralDetector _detector;
        [Inject] private CentralCore _centralCore;
        [Inject] private Camera _camera;
        private bool _inputSubscribed;

        /// <summary>注入就绪后监听鼠标开火按下事件。</summary>
        private void Start()
        {
            SubscribeInput();
        }

        /// <summary>启用时恢复鼠标输入订阅。</summary>
        private void OnEnable()
        {
            SubscribeInput();
        }

        /// <summary>停用时解除输入订阅并释放当前标记队列。</summary>
        private void OnDisable()
        {
            UnsubscribeInput();
            _queueService?.Clear();
        }

        /// <summary>销毁时确保输入订阅和标记队列均已释放。</summary>
        private void OnDestroy()
        {
            UnsubscribeInput();
            _queueService?.Clear();
        }

        /// <summary>订阅单次鼠标按下事件。</summary>
        private void SubscribeInput()
        {
            if (_inputSubscribed || _input == null)
            {
                return;
            }

            _input.MouseFirePressed += HandleMouseFirePressed;
            _inputSubscribed = true;
        }

        /// <summary>退订单次鼠标按下事件。</summary>
        private void UnsubscribeInput()
        {
            if (!_inputSubscribed || _input == null)
            {
                return;
            }

            _input.MouseFirePressed -= HandleMouseFirePressed;
            _inputSubscribed = false;
        }

        /// <summary>切出标记模式时由队列服务清除本次标记。</summary>
        private void Update()
        {
            if (_attackModeProvider?.AttackMode != TransmitterAttackMode.MarkedTarget)
            {
                _queueService?.Clear();
            }
        }

        /// <summary>仅在标记模式、攻击输入允许且鼠标未命中 UI 时追加目标。</summary>
        private void HandleMouseFirePressed()
        {
            if (_attackModeProvider?.AttackMode != TransmitterAttackMode.MarkedTarget ||
                _input == null ||
                _input.CurrentAttackInputState != AttackInputState.Allowed ||
                _queueService == null ||
                _camera == null ||
                IsPointerOverUi())
            {
                return;
            }

            Enemy enemy = FindEnemyUnderPointer();
            if (enemy != null)
            {
                _queueService.TryAppend(enemy);
            }
        }

        /// <summary>沿鼠标射线从近到远查找首个有效敌人，实体障碍物会阻断射线。</summary>
        private Enemy FindEnemyUnderPointer()
        {
            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                _camera.farClipPlane,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, CompareHitDistance);

            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null || IsPlayerCollider(collider))
                {
                    continue;
                }

                Enemy enemy = collider.GetComponentInParent<Enemy>();
                if (enemy != null &&
                    IsEnemyLayer(collider, enemy) &&
                    enemy.isActiveAndEnabled &&
                    !enemy.IsDead &&
                    ((IDamageable)enemy).IsAliveAndValid())
                {
                    return enemy;
                }

                if (!collider.isTrigger)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>检查鼠标位置是否覆盖可交互 UGUI Graphic。</summary>
        private bool IsPointerOverUi()
        {
            Graphic[] graphics = FindObjectsByType<Graphic>(FindObjectsSortMode.None);
            Vector2 pointer = _input.PointerPosition;
            for (int index = 0; index < graphics.Length; index++)
            {
                if (IsGraphicUnderPointer(graphics[index], pointer))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>按 Canvas 射线规则检查单个 Graphic 是否位于指针下。</summary>
        private static bool IsGraphicUnderPointer(Graphic graphic, Vector2 pointer)
        {
            if (graphic == null || !graphic.isActiveAndEnabled || !graphic.raycastTarget)
            {
                return false;
            }

            Canvas canvas = graphic.canvas;
            GraphicRaycaster raycaster = canvas != null
                ? canvas.GetComponent<GraphicRaycaster>()
                : null;
            if (raycaster == null || !raycaster.isActiveAndEnabled)
            {
                return false;
            }

            Camera eventCamera = raycaster.eventCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(
                    graphic.rectTransform,
                    pointer,
                    eventCamera,
                    graphic.raycastPadding) &&
                graphic.Raycast(pointer, eventCamera);
        }

        /// <summary>排除玩家中央核心自身层级的碰撞体。</summary>
        private bool IsPlayerCollider(Collider collider)
        {
            Transform coreTransform = _centralCore != null ? _centralCore.transform : null;
            return coreTransform != null &&
                (collider.transform == coreTransform || collider.transform.IsChildOf(coreTransform));
        }

        /// <summary>确认敌人或其命中子物体位于探测器的敌人层掩码内。</summary>
        private bool IsEnemyLayer(Collider collider, Enemy enemy)
        {
            int layerMask = _detector != null ? _detector.enemyLayerMask.value : -1;
            return ContainsLayer(layerMask, enemy.gameObject.layer) ||
                ContainsLayer(layerMask, collider.gameObject.layer);
        }

        /// <summary>判断层掩码是否包含指定层。</summary>
        private static bool ContainsLayer(int layerMask, int layer)
        {
            return (layerMask & (1 << layer)) != 0;
        }

        /// <summary>按射线距离从近到远排列命中结果。</summary>
        private static int CompareHitDistance(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }

    }
}
