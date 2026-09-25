using System;

namespace GameSystem
{
    /// <summary>
    /// 游戏会话数据。永久 Points 由 ResourceManager 管理，本类只记录本局暂存结果。
    /// </summary>
    public class GameSession
    {
        /// <summary>本局已获得的积分(统计/结算展示用)。</summary>
        public int SessionPoints { get; private set; }

        /// <summary>当前等待结算的本局积分。</summary>
        public int PendingPoints => SessionPoints;

        /// <summary>目标时长前获得的本局积分。</summary>
        public int BasePoints { get; private set; }

        /// <summary>超时阶段获得的本局积分。</summary>
        public int OvertimePoints { get; private set; }

        /// <summary>本局击杀数。</summary>
        public int KillCount { get; private set; }

        /// <summary>本局已运行时间，单位为秒。</summary>
        public float ElapsedTime { get; private set; }

        /// <summary>本局使用的难度标识。</summary>
        public string DifficultyId { get; private set; } = string.Empty;

        /// <summary>本局使用的随机种子。</summary>
        public int RandomSeed { get; private set; }

        /// <summary>本局是否已经进入超时阶段。</summary>
        public bool IsOvertime { get; private set; }

        /// <summary>进入超时阶段后经过的时间，单位为秒。</summary>
        public float OvertimeElapsedTime { get; private set; }

        /// <summary>
        /// 创建一个处于初始状态的游戏会话。
        /// </summary>
        public GameSession() { }

        /// <summary>
        /// 重置会话到初始状态。
        /// </summary>
        public void Reset()
        {
            SessionPoints = 0;
            BasePoints = 0;
            OvertimePoints = 0;
            KillCount = 0;
            ElapsedTime = 0f;
            DifficultyId = string.Empty;
            RandomSeed = 0;
            IsOvertime = false;
            OvertimeElapsedTime = 0f;
        }

        /// <summary>
        /// 开始一局并记录其难度标识和随机种子。
        /// </summary>
        /// <param name="difficultyId">本局难度标识。</param>
        /// <param name="randomSeed">本局随机种子。</param>
        public void BeginRun(string difficultyId, int randomSeed)
        {
            Reset();
            DifficultyId = difficultyId ?? string.Empty;
            RandomSeed = randomSeed;
        }

        /// <summary>
        /// 更新本局当前难度标识，不重置积分、击杀数或计时。
        /// </summary>
        /// <param name="difficultyId">当前难度标识。</param>
        public void ChangeDifficulty(string difficultyId)
        {
            DifficultyId = difficultyId ?? string.Empty;
        }

        /// <summary>
        /// 推进本局时间。
        /// </summary>
        /// <param name="deltaTime">要推进的秒数。</param>
        public void AdvanceTime(float deltaTime)
        {
            if (deltaTime > 0f && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime))
            {
                ElapsedTime += deltaTime;

                if (IsOvertime)
                {
                    OvertimeElapsedTime += deltaTime;
                }
            }
        }

        /// <summary>
        /// 进入超时阶段并记录已经超过目标时长的部分。
        /// </summary>
        /// <param name="overtimeElapsedTime">进入阶段时已经经过的超时时间。</param>
        public void EnterOvertime(float overtimeElapsedTime)
        {
            if (IsOvertime)
            {
                return;
            }

            IsOvertime = true;
            OvertimeElapsedTime = IsFinite(overtimeElapsedTime)
                ? Math.Max(0f, overtimeElapsedTime)
                : 0f;
        }

        /// <summary>
        /// 记录一次击杀并增加本局积分。
        /// </summary>
        /// <param name="pointsAwarded">本次击杀获得的积分。</param>
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
            {
                SessionPoints += amount;

                if (IsOvertime)
                {
                    OvertimePoints += amount;
                }
                else
                {
                    BasePoints += amount;
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
