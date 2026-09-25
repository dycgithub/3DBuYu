using GameSystem;
using R3;

namespace Services
{
    /// <summary>普通人口和特殊敌人时间表的生命周期入口。</summary>
    public interface IEnemyPopulationService
    {
        int EnemiesAlive { get; }
        int MaximumPopulation { get; }
        DifficultyLevel Difficulty { get; }
        Observable<int> OnEnemiesAliveChanged { get; }
        Observable<Unit> OnPopulationStarted { get; }
        void StartPopulation(DifficultyConfig difficulty);

        /// <summary>
        /// 在不清空既有敌人的前提下切换普通人口配置。
        /// </summary>
        /// <param name="difficulty">目标难度配置。</param>
        /// <returns>切换成功时返回 true。</returns>
        bool TryChangeDifficulty(DifficultyConfig difficulty);

        /// <summary>按配置 ID 触发一次特殊敌人生成，供未来事件系统调用。</summary>
        bool TriggerSpecialSpawn(string ruleId);

        void PausePopulation();
        void ResumePopulation();
        void StopPopulation();
    }
}
