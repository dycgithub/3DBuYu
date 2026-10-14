using CombatSystem;
using Interfaces;
using UnityEngine;

namespace Play
{
    /// <summary>解析轨迹预览与各发射器模式一致的瞄准目标。</summary>
    public partial class TransmitterTrajectoryDisplay
    {
        /// <summary>按当前攻击模式选择轨迹预览目标。</summary>
        /// <returns>当前发射请求使用的追踪目标；无目标时返回 null。</returns>
        private IDamageable ResolveAimTarget()
        {
            if (options != null && options.AttackMode == TransmitterAttackMode.MarkedTarget)
            {
                return _markedTarget?.Target;
            }

            if (options != null && options.AttackMode == TransmitterAttackMode.Mouse)
            {
                return ResolveMouseTarget();
            }

            IDamageable focusedTarget = _globalEffects?.GetFocusedTarget();
            if (focusedTarget.IsAliveAndValid())
            {
                return focusedTarget;
            }

            return _detector.GetTarget(portIndex);
        }

        /// <summary>沿当前鼠标射线解析与玩家发射器相同的目标。</summary>
        /// <returns>射线命中的有效目标；未命中时返回 null。</returns>
        private IDamageable ResolveMouseTarget()
        {
            if (_input == null || _camera == null || _input.MouseFireHeld == null ||
                !_input.MouseFireHeld.CurrentValue)
            {
                return null;
            }

            int targetMask = _detector != null
                ? _detector.enemyLayerMask.value
                : LayerMask.GetMask("Enemy");
            if (targetMask == 0)
            {
                return null;
            }

            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    _camera.farClipPlane,
                    targetMask,
                    QueryTriggerInteraction.Collide))
            {
                return null;
            }

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            return target.IsAliveAndValid() ? target : null;
        }
    }
}
