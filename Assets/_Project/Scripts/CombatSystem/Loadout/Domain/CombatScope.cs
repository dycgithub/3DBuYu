namespace CombatSystem
{
    /// <summary>
    /// 表示物品战斗配置可以作用的装备区域。
    /// </summary>
    public enum CombatScope
    {
        /// <summary>同时作用于中心和发射器。</summary>
        CentralAndTransmitter = 0,

        /// <summary>仅作用于中心。</summary>
        Central = 1,

        /// <summary>仅作用于发射器。</summary>
        Transmitter = 2,

        /// <summary>没有任何战斗作用域。</summary>
        None = 3
    }

    /// <summary>
    /// 解析最终物品 ID 的战斗作用域。
    /// 未列入最终表的 ID 不产生战斗权限，避免未配置物品继承共享回退效果。
    /// </summary>
    public static class ItemCombatScopePolicy
    {
        /// <summary>
        /// 将物品 ID 解析为战斗作用域。
        /// </summary>
        /// <param name="itemId">最终物品 ID，可为三位数字字符串。</param>
        /// <param name="scope">解析出的战斗作用域。</param>
        /// <returns>ID 在 001-025 范围内时返回 <c>true</c>。</returns>
        public static bool TryResolve(string itemId, out CombatScope scope)
        {
            if (!int.TryParse(itemId, out int numericId))
            {
                scope = CombatScope.None;
                return false;
            }

            scope = numericId switch
            {
                1 => CombatScope.CentralAndTransmitter,
                2 => CombatScope.Transmitter,
                3 => CombatScope.Transmitter,
                4 => CombatScope.Transmitter,
                5 => CombatScope.Transmitter,
                6 => CombatScope.Transmitter,
                7 => CombatScope.Transmitter,
                8 => CombatScope.Transmitter,
                9 => CombatScope.Transmitter,
                10 => CombatScope.CentralAndTransmitter,
                11 => CombatScope.Transmitter,
                12 => CombatScope.Central,
                13 => CombatScope.CentralAndTransmitter,
                14 => CombatScope.Transmitter,
                15 => CombatScope.Central,
                16 => CombatScope.Transmitter,
                17 => CombatScope.Transmitter,
                18 => CombatScope.Transmitter,
                19 => CombatScope.Transmitter,
                20 => CombatScope.Transmitter,
                21 => CombatScope.Transmitter,
                22 => CombatScope.Central,
                23 => CombatScope.Central,
                24 => CombatScope.CentralAndTransmitter,
                25 => CombatScope.CentralAndTransmitter,
                _ => CombatScope.None
            };

            return scope != CombatScope.None;
        }
    }
}
