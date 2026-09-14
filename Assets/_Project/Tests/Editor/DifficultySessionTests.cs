using GameSystem;
using NUnit.Framework;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证切换难度标识不会重置本局已有数据。
    /// </summary>
    public sealed class DifficultySessionTests
    {
        [Test]
        public void ChangeDifficulty_PreservesRunData()
        {
            GameSession session = new();
            session.BeginRun("easy", 1234);
            session.RecordKill(25);
            session.AdvanceTime(12f);

            session.ChangeDifficulty("normal");

            Assert.That(session.DifficultyId, Is.EqualTo("normal"));
            Assert.That(session.RandomSeed, Is.EqualTo(1234));
            Assert.That(session.KillCount, Is.EqualTo(1));
            Assert.That(session.SessionPoints, Is.EqualTo(25));
            Assert.That(session.ElapsedTime, Is.EqualTo(12f));
        }
    }
}
