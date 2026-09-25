using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 保存中央主动技能提交的发射器全局效果，供发射器攻击系统读取。
    /// 不修改发射器配置或攻击构建规则；效果持续到来源或装备状态被清除。
    /// </summary>
    public sealed class TransmitterGlobalEffectService :
        ITransmitterGlobalEffectService,
        ITransmitterGlobalEffectResetService
    {
        /// <summary>当前全局发射器射速倍率。</summary>
        private float _attackSpeedMultiplier = 1f;
        private float _attackSpeedExpiresAt = float.PositiveInfinity;

        /// <summary>未过期时返回当前全局射速倍率。</summary>
        public float AttackSpeedMultiplier
        {
            get
            {
                if (Time.time >= _attackSpeedExpiresAt)
                {
                    ClearAttackSpeed();
                }

                return _attackSpeedMultiplier;
            }
        }

        /// <summary>当前效果来源物品实例；没有效果时为零。</summary>
        public int SourceInstanceId { get; private set; }
        private IDamageable _focusedTarget;
        private int _focusSourceInstanceId;
        private float _focusExpiresAt;

        /// <summary>保存指定来源的全局射速倍率。</summary>
        public void SetAttackSpeedMultiplier(
            float multiplier,
            int sourceInstanceId,
            float durationSeconds)
        {
            _attackSpeedMultiplier = Mathf.Max(0f, multiplier);
            SourceInstanceId = sourceInstanceId;
            _attackSpeedExpiresAt = durationSeconds > 0f
                ? Time.time + durationSeconds
                : float.PositiveInfinity;
        }

        /// <summary>保存限时集火状态，并用本局时间直接校验到期。</summary>
        public void SetFocusedTarget(
            IDamageable target,
            float durationSeconds,
            int sourceInstanceId)
        {
            _focusedTarget = target;
            _focusSourceInstanceId = sourceInstanceId;
            _focusExpiresAt = Time.time + Mathf.Max(0f, durationSeconds);
        }

        /// <summary>返回仍有效的集火目标；死亡或超时后自动清空。</summary>
        public IDamageable GetFocusedTarget()
        {
            if (_focusedTarget == null || !_focusedTarget.IsAlive || Time.time >= _focusExpiresAt)
            {
                ClearFocusedTarget();
                return null;
            }

            return _focusedTarget;
        }

        /// <summary>只清除仍由指定来源持有的效果。</summary>
        public void ClearSource(int sourceInstanceId)
        {
            if (SourceInstanceId != sourceInstanceId && _focusSourceInstanceId != sourceInstanceId)
            {
                return;
            }

            if (SourceInstanceId == sourceInstanceId)
            {
                ClearAttackSpeed();
            }

            if (_focusSourceInstanceId == sourceInstanceId)
            {
                ClearFocusedTarget();
            }
        }

        /// <summary>清除当前全局装备效果并恢复默认倍率。</summary>
        public void ClearAll()
        {
            ClearAttackSpeed();
            ClearFocusedTarget();
        }

        /// <summary>清除当前射速倍率并恢复默认状态。</summary>
        private void ClearAttackSpeed()
        {
            _attackSpeedMultiplier = 1f;
            _attackSpeedExpiresAt = float.PositiveInfinity;
            SourceInstanceId = 0;
        }

        /// <summary>清除到期、死亡或来源已移除的集火目标。</summary>
        private void ClearFocusedTarget()
        {
            _focusedTarget = null;
            _focusSourceInstanceId = 0;
            _focusExpiresAt = 0f;
        }
    }
}
