using EnemySystem;
using UnityEngine;

namespace Services
{
    /// <summary>提供不占用主波次配额的子敌人生成入口。</summary>
    public interface IEnemyChildSpawner
    {
        /// <summary>在指定位置生成一个归属于母体的子敌人。</summary>
        Enemy TrySpawnChild(
            Enemy parent,
            Enemy prefab,
            Vector3 position,
            float healthMultiplier,
            float speedMultiplier);
    }
}
