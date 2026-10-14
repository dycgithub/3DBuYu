using UnityEngine;

namespace CombatSystem
{
    /// <summary>只执行一次射线查询并立即处理命中的轨迹。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Hitscan")]
    public sealed class HitscanTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>指示模拟服务应直接发起一次射线查询。</summary>
        public override bool IsHitscan => true;
    }
}
