using System;
using Services;

namespace CombatSystem
{
    /// <summary>协调端口输入、攻击参数构建和攻击执行。</summary>
    public sealed class TransmitterAttackService : ITransmitterAttackService
    {
        private readonly ICombatPhaseService _phase;
        private readonly TransmitterAttackBuildService _buildService;
        private readonly AttackDataBuilder _attackBuilder;
        private readonly AttackExecutor _attackExecutor;

        /// <summary>一次攻击成功提交后触发，并携带本次攻击的表现上下文。</summary>
        public event Action<TransmitterAttackInput> AttackSucceeded;

        /// <summary>创建发射器攻击服务。</summary>
        /// <param name="phase">提供战斗阶段门禁的服务。</param>
        /// <param name="attackBuilder">构建最终攻击数据的构建器。</param>
        /// <param name="attackExecutor">校验并提交攻击的执行器。</param>
        /// <param name="modifierSource">提供端口装备攻击修正的来源。</param>
        /// <param name="globalEffects">提供中央主动技能产生的全局效果，可为空。</param>
        public TransmitterAttackService(
            ICombatPhaseService phase,
            AttackDataBuilder attackBuilder,
            AttackExecutor attackExecutor,
            ITransmitterAttackModifierSource modifierSource = null,
            ITransmitterGlobalEffectService globalEffects = null)
        {
            _phase = phase;
            _buildService = new TransmitterAttackBuildService(modifierSource, globalEffects);
            _attackBuilder = attackBuilder;
            _attackExecutor = attackExecutor;
        }

        /// <summary>尝试执行指定端口的一次攻击。</summary>
        /// <param name="input">发射器端口的原始攻击输入。</param>
        /// <returns>攻击成功提交时返回 <c>true</c>。</returns>
        public bool TryExecuteAttack(in TransmitterAttackInput input)
        {
            if (_phase != null && !_phase.CanPerformCombatActions)
            {
                return false;
            }

            if (_attackBuilder == null || _attackExecutor == null)
            {
                return false;
            }

            TransmitterAttackBuild build = _buildService.BuildAttackParameters(in input);
            if (!_attackBuilder.TryBuildAttackData(in input, in build, out AttackData attackData))
            {
                return false;
            }

            bool succeeded = _attackExecutor.TryExecuteAttack(ref attackData, build.AttacksPerSecond);
            if (succeeded)
            {
                AttackSucceeded?.Invoke(input);
            }

            return succeeded;
        }
    }
}
