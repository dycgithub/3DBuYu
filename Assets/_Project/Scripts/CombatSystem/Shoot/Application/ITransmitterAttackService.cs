using System;

namespace CombatSystem
{
    /// <summary>提交发射器端口攻击请求的服务接口。</summary>
    public interface ITransmitterAttackService
    {
        /// <summary>一次攻击成功提交后触发。</summary>
        event Action AttackSucceeded;

        /// <summary>尝试执行一次发射器攻击。</summary>
        /// <param name="input">发射器端口的原始攻击输入。</param>
        /// <returns>攻击成功提交时返回 <c>true</c>。</returns>
        bool TryExecuteAttack(in TransmitterAttackInput input);
    }
}
