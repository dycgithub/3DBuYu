namespace Supply
{
    /// <summary>
    /// 定义 SupplyScene 支持的抽奖入口。
    /// 该类型只描述场景入口，不负责执行抽奖或结算奖励。
    /// </summary>
    public enum SupplyMethod
    {
        /// <summary>宝箱开箱入口，对应 CubeCam。</summary>
        Chest,

        /// <summary>老虎机入口，对应 DNACam。</summary>
        SlotMachine,

        /// <summary>莫比乌斯奖池入口，对应 MCam。</summary>
        Mobius
    }
}
