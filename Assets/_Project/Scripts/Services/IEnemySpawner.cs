using EnemySystem;
using R3;
using UnityEngine;

namespace Services
{
    /// <summary>
    /// 敌人生成和场上敌人生命周期管理接口。
    /// </summary>
    public interface IEnemySpawner
    {
        /// <summary>当前场上存活敌人数。</summary>
        int ActiveEnemyCount { get; }

        /// <summary>场上存活敌人数变化通知。</summary>
        Observable<int> OnActiveEnemyCountChanged { get; }

        /// <summary>
        /// 创建一个敌人并加入当前存活集合。
        /// </summary>
        /// <param name="prefab">怪物预制体。</param>
        /// <param name="category">生成分类。</param>
        /// <param name="healthMultiplier">生命倍率。</param>
        /// <param name="speedMultiplier">速度倍率。</param>
        /// <param name="initialRotation">本次生成使用的世界初始旋转。</param>
        /// <returns>实际生成成功时返回 true，失败不消费配额。</returns>
        bool TrySpawnEnemy(
            Enemy prefab,
            EnemySpawnCategory category,
            float healthMultiplier,
            float speedMultiplier,
            Quaternion initialRotation);

        /// <summary>读取指定生成分类的实际存活数。</summary>
        int CountCategory(EnemySpawnCategory category);

        /// <summary>清除所有当前存活敌人。</summary>
        void ClearAll();

        /// <summary>消灭场上所有存活敌人(清屏技能)。走完整死亡链路:积分/特效。</summary>
        void KillAllEnemies();
    }
}
