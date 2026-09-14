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
        MenuFusion,
        EnemyContact
    }

    public interface IEnergyService
    {
        ReadOnlyReactiveProperty<float> CurrentEnergy { get; }
        float MaximumEnergy { get; }
        bool IsDepleted { get; }

        /// <summary>能量从正数降到 0 时触发；同一次耗尽只触发一次。</summary>
        event Action EnergyDepleted;

        void Initialize(float initialEnergy, float maximumEnergy);
        bool TrySpend(float baseAmount, EnergySpendKind kind);
        float Drain(float baseAmount, EnergySpendKind kind);
        float AddEnergy(float amount);
        float Tick(float deltaTime, float baseDrainPerSecond);
    }
}
