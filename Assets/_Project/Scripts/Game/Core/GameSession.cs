namespace GameSystem
{
    /// <summary>
    /// 游戏会话数据。永久 Points 由 ResourceManager 管理，本类只记录本局暂存结果。
    /// </summary>
    public class GameSession
    {
        /// <summary>本局已获得的积分(统计/结算展示用)。</summary>
        public int SessionPoints { get; private set; }

        public int PendingPoints => SessionPoints;
        public int KillCount { get; private set; }
        public float ElapsedTime { get; private set; }
        public int OvertimeTier { get; private set; }
        public string DifficultyId { get; private set; } = string.Empty;
        public int RandomSeed { get; private set; }

        public float OvertimeMultiplier => 1f + OvertimeTier;

        public GameSession() { }

        /// <summary>
        /// 重置会话到初始状态。
        /// </summary>
        public void Reset()
        {
            SessionPoints = 0;
            KillCount = 0;
            ElapsedTime = 0f;
            OvertimeTier = 0;
            DifficultyId = string.Empty;
            RandomSeed = 0;
        }

        public void BeginRun(string difficultyId, int randomSeed)
        {
            Reset();
            DifficultyId = difficultyId ?? string.Empty;
            RandomSeed = randomSeed;
        }

        public void AdvanceTime(float deltaTime, float targetDuration)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return;

            ElapsedTime += deltaTime;
            OvertimeTier = RunRuleMath.GetOvertimeTier(ElapsedTime, targetDuration);
        }

        public void RecordKill(int pointsAwarded)
        {
            KillCount++;
            AddPoints(pointsAwarded);
        }

        /// <summary>
        /// 增加本局积分。
        /// </summary>
        public void AddPoints(int amount)
        {
            if (amount > 0)
                SessionPoints += amount;
        }
    }
}
