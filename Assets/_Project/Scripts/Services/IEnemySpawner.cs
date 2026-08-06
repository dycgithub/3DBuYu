using EnemySystem;
using R3;

namespace Services
{
    public interface IEnemySpawner
    {
        int ActiveEnemyCount { get; }
        Observable<int> OnActiveEnemyCountChanged { get; }
        void SpawnEnemy(WaveEnemyInfo info, WaveData currentWave);
        void ClearAll();

        /// <summary>消灭场上所有存活敌人(清屏技能)。走完整死亡链路:积分/加时/特效。</summary>
        void KillAllEnemies();
    }
}