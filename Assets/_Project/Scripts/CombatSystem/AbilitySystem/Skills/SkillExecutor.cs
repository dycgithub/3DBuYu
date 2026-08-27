using System.Collections.Generic;
using Services;
using UnityEngine;
using VContainer.Unity;

namespace CombatSystem
{
    /// <summary>检查技能状态和资源，并按顺序执行技能动作。</summary>
    public sealed class SkillExecutor : ITickable
    {
        private readonly ICombatEnergyService _energy;
        private readonly ICombatPhaseService _phase;
        private readonly SkillExecutionContext _executionContext;
        private readonly Dictionary<int, SkillRuntime> _runtimes = new();

        public SkillExecutor(
            IProjectileSpawner projectileSpawner,
            ICombatEnergyService energy,
            ICombatPhaseService phase)
        {
            _energy = energy;
            _phase = phase;
            _executionContext = new SkillExecutionContext(projectileSpawner);
        }

        public bool TryActivate(SkillInfo info)
        {
            SkillDefinition definition = info.Definition;
            if (definition == null)
                return false;

            if (_phase != null && !_phase.CanPerformCombatActions)
                return false;

            int key = definition.GetInstanceID();
            if (!_runtimes.TryGetValue(key, out SkillRuntime runtime))
            {
                runtime = new SkillRuntime(definition);
                _runtimes.Add(key, runtime);
            }

            if (!runtime.IsReady)
                return false;

            if (_energy != null)
            {
                if (!_energy.TrySpend(definition.EnergyCost, EnergySpendKind.Skill))
                    return false;

                // 精确扣到 0 时，耗尽事件会同步结束本局，不能继续执行技能动作。
                if (_phase != null && !_phase.CanPerformCombatActions)
                    return false;
            }

            bool executed = false;
            if (definition.Actions != null)
            {
                for (int i = 0; i < definition.Actions.Length; i++)
                {
                    SkillActionDefinition action = definition.Actions[i];
                    if (action != null)
                        executed |= action.Execute(info, _executionContext);
                }
            }

            if (!executed)
            {
                if (_energy != null && definition.EnergyCost > 0f)
                    _energy.AddEnergy(definition.EnergyCost * _energy.CostMultiplier);
                return false;
            }

            runtime.StartCooldown();
            return true;
        }

        public void Tick(float deltaTime)
        {
            foreach (SkillRuntime runtime in _runtimes.Values)
                runtime.Tick(deltaTime);
        }

        void ITickable.Tick()
        {
            Tick(Time.deltaTime);
        }
    }
}
