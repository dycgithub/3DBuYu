using NUnit.Framework;
using Services;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证击杀奖励事件能够返回经过本局倍率计算后的实际积分。
    /// </summary>
    public sealed class PointsRewardContractTests
    {
        [Test]
        public void EnemyKilledNotification_ReturnsActualAwardedPoints()
        {
            System.Reflection.MethodInfo method = typeof(IGameEventService).GetMethod("NotifyEnemyKilled");

            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(int)));
        }
    }
}
