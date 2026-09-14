using System;

namespace Services
{
    /// <summary>延后当前同步动作的能量耗尽通知，保证伤害、消费和结果先完成。</summary>
    public interface IEnergyActionScope
    {
        IDisposable BeginAction();
    }
}
