namespace CombatSystem
{
    /// <summary>为指定发射器端口提供瞄准命令。</summary>
    public interface IAttackCommandSource
    {
        /// <summary>尝试读取指定端口的当前瞄准命令。</summary>
        /// <param name="transmitterPortIndex">发射器端口索引。</param>
        /// <param name="command">成功时输出当前端口的瞄准命令。</param>
        /// <returns>存在可用瞄准命令时返回 <c>true</c>。</returns>
        bool TryGetAimCommand(int transmitterPortIndex, out AimCommand command);
    }
}
