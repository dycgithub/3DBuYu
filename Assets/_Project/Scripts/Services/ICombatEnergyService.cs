using System;
using R3;

namespace Services
{
    public enum EnergySpendKind
    {
        TimeFlow,
        EquipmentUpkeep,
        Shot,
        Skill,
        CollisionPenalty,
        MenuFusion
    }

    public interface ICombatEnergyService
    {
        ReadOnlyReactiveProperty<float> CurrentEnergy { get; }
        float MaximumEnergy { get; }
        float CostMultiplier { get; }
        bool IsDepleted { get; }

        /// <summary>能量从正数降到 0 时触发；同一次耗尽只触发一次。</summary>
        event Action EnergyDepleted;

        void Initialize(float initialEnergy, float maximumEnergy);
        void SetCostMultiplier(float multiplier);
        bool TrySpend(float baseAmount, EnergySpendKind kind);
        float Drain(float baseAmount, EnergySpendKind kind);
        float AddEnergy(float amount);
        float Tick(float deltaTime, float baseDrainPerSecond);
    }
}
