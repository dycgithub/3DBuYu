namespace CombatSystem
{
    /// <summary>
    /// 输入来源抽象。当前端口数字键仍由 IInputService 提供，鼠标分组可后续接入。
    /// </summary>
    public interface IAttackCommandSource
    {
        bool TryGetAimCommand(int portIndex, out AimCommand command);
    }
}
