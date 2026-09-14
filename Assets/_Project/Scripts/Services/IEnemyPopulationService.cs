using GameSystem;
using R3;

namespace Services
{
    /// <summary>数量调度的生命周期入口及事实事件，不再提供逐波推进。</summary>
    public interface IEnemyPopulationService
    {
        int EnemiesAlive { get; }
        int MaximumPopulation { get; }
        DifficultyLevel Difficulty { get; }
        Observable<int> OnEnemiesAliveChanged { get; }
        Observable<Unit> OnPopulationStarted { get; }
        Observable<Unit> OnInitialCompleted { get; }
        Observable<Unit> OnRefillStarted { get; }
        Observable<Unit> OnRefillCompleted { get; }
        void StartPopulation(DifficultyConfig difficulty);

        /// <summary>
        /// 在当前调度不清空既有敌人的前提下切换后续生成难度。
        /// </summary>
        /// <param name="difficulty">目标难度配置。</param>
        /// <returns>切换成功时返回 true。</returns>
        bool TryChangeDifficulty(DifficultyConfig difficulty);

        void PausePopulation();
        void ResumePopulation();
        void StopPopulation();
    }
}
