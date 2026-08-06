using R3;

namespace Services
{
    /// <summary>
    /// 波次事件/控制服务接口。
    /// 由 <c>EnemySpawnManager</c> 实现,注册到 VContainer。
    /// UI、GameManager 等通过此接口订阅波次状态变更,避免直接持有 MonoBehaviour 引用。
    /// </summary>
    public interface IWaveEventService
    {
        // === 状态属性 ===

        /// <summary>当前波次编号(从 1 开始,0=未开始)。</summary>
        int CurrentWaveNumber { get; }

        /// <summary>配置的总波次数。</summary>
        int TotalWaveCount { get; }

        /// <summary>当前存活的敌人数。</summary>
        int EnemiesAlive { get; }

        /// <summary>当前波次是否处于活动状态(已开启且未结束)。</summary>
        bool IsWaveActive { get; }

        /// <summary>当前是否正在生成敌人。</summary>
        bool IsSpawning { get; }

        // === 事件(R3 Observable) ===

        /// <summary>波次开始(参数:波次编号)。</summary>
        Observable<int> OnWaveStarted { get; }

        /// <summary>波次结束(参数:波次编号)。</summary>
        Observable<int> OnWaveEnded { get; }

        /// <summary>波次切换(参数:当前波次编号,总波次数)。</summary>
        Observable<WaveChangedEvent> OnWaveChanged { get; }

        /// <summary>存活敌人数变化(参数:当前存活数)。</summary>
        Observable<int> OnEnemiesAliveChanged { get; }

        /// <summary>所有波次已完成(一轮游戏结束信号)。</summary>
        Observable<Unit> OnAllWavesCompleted { get; }

        // === 控制方法 ===

        /// <summary>开始下一波(由 GameManager.StartLevel 或 Manual 触发器调用)。</summary>
        void StartNextWave();

        /// <summary>跳过当前波次(调试用,瞬间消灭所有当前敌人)。</summary>
        void SkipCurrentWave();

        /// <summary>暂停波次(响应 GameState.Paused)。</summary>
        void PauseWaves();

        /// <summary>恢复波次(响应 GameState.Playing)。</summary>
        void ResumeWaves();

        /// <summary>停止波次(响应 GameState.Settled/Failed/ReturnToMenu)。</summary>
        void StopWaves();

        /// <summary>清除所有存活敌人(不触发事件,仅清理)。</summary>
        void ClearAllEnemies();
    }
}
