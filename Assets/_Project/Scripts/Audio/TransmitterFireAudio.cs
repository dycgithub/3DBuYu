using System;
using CombatSystem;
using VContainer.Unity;

namespace GameSystem
{
    /// <summary>
    /// 监听发射器攻击成功事件，并通过全局音频管理器播放对应端口音效。
    /// </summary>
    public sealed class TransmitterFireAudio : IStartable, IDisposable
    {
        private readonly ITransmitterAttackService _attackService;
        private readonly AudioManager _audioManager;

        /// <summary>创建发射器音效表现监听器。</summary>
        /// <param name="attackService">发射器攻击服务。</param>
        /// <param name="audioManager">全局音频管理器。</param>
        public TransmitterFireAudio(
            ITransmitterAttackService attackService,
            AudioManager audioManager)
        {
            _attackService = attackService;
            _audioManager = audioManager;
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

        /// <summary>在攻击成功后播放本次端口指定的音效。</summary>
        /// <param name="input">成功提交的攻击输入。</param>
        private void HandleAttackSucceeded(TransmitterAttackInput input)
        {
            _audioManager.PlaySFXAtPosition(input.FireSound, input.OriginPosition);
        }
    }
}
