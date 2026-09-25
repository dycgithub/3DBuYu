using System;
using CombatSystem;
using Services;
using VContainer.Unity;

namespace EffectSystem
{
    /// <summary>
    /// 监听发射器攻击成功事件，并播放端口配置的开火特效。
    /// </summary>
    public sealed class TransmitterFireEffect : IStartable, IDisposable
    {
        private readonly ITransmitterAttackService _attackService;
        private readonly IEffectService _effectService;

        /// <summary>创建发射器开火特效监听器。</summary>
        /// <param name="attackService">发射器攻击服务。</param>
        /// <param name="effectService">战斗特效服务。</param>
        public TransmitterFireEffect(
            ITransmitterAttackService attackService,
            IEffectService effectService)
        {
            _attackService = attackService;
            _effectService = effectService;
        }

        /// <summary>开始监听成功提交的发射器攻击。</summary>
        public void Start()
        {
            _attackService.AttackSucceeded += HandleAttackSucceeded;
        }

        /// <summary>解除攻击事件监听。</summary>
        public void Dispose()
        {
            _attackService.AttackSucceeded -= HandleAttackSucceeded;
        }

        /// <summary>在攻击成功后播放端口指定的开火特效。</summary>
        /// <param name="input">成功提交的攻击输入。</param>
        private void HandleAttackSucceeded(TransmitterAttackInput input)
        {
            if (input.FireEffectId != EffectId.None)
            {
                _effectService.Play(
                    input.FireEffectId,
                    input.OriginPosition,
                    input.OriginTransform);
            }
        }
    }
}
