namespace CombatSystem
{
    /// <summary>沿固定方向移动的普通投射物轨迹。</summary>
    [UnityEngine.CreateAssetMenu(menuName = "Combat/Attack Trajectory/Projectile")]
    public sealed class ProjectileTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>普通投射物使用逐帧路径扫掠。</summary>
        public override bool IsHitscan => false;
    }
}
