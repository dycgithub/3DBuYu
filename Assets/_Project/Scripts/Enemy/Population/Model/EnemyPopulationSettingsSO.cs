using UnityEngine;

namespace EnemySystem
{
    /// <summary>保存各类敌人共享的场景存活容量。</summary>
    [CreateAssetMenu(fileName = "EnemyPopulationSettings", menuName = "Enemy/Population Settings")]
    public sealed class EnemyPopulationSettingsSO : ScriptableObject
    {
        private const int MinimumAllowedAlive = 1700;
        private const int MaximumAllowedAlive = 1900;
        private const int DefaultMaximumAlive = 1800;

        [Tooltip("普通、精英、Boss 与子敌人共同使用的同时存活上限。")]
        [SerializeField, Range(MinimumAllowedAlive, MaximumAllowedAlive)]
        private int maximumAlive = DefaultMaximumAlive;

        /// <summary>当前场景允许同时存活的敌人数。</summary>
        public int MaximumAlive => Mathf.Clamp(
            maximumAlive,
            MinimumAllowedAlive,
            MaximumAllowedAlive);

        /// <summary>判断指定存活数是否仍有生成容量。</summary>
        /// <param name="activeEnemyCount">普通、精英、Boss 和子敌人的总存活数。</param>
        /// <returns>尚未达到全局上限时返回 true。</returns>
        public bool HasCapacity(int activeEnemyCount)
        {
            return activeEnemyCount < MaximumAlive;
        }

        /// <summary>将 Inspector 数值限制在允许的全局容量范围内。</summary>
        private void OnValidate()
        {
            maximumAlive = Mathf.Clamp(
                maximumAlive,
                MinimumAllowedAlive,
                MaximumAllowedAlive);
        }
    }
}
