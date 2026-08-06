namespace GameSystem
{
    /// <summary>
    /// 游戏会话数据 — 纯数据，无 MonoBehaviour 依赖。
    /// 纯生存制下仅记录本局击杀积分(统计用),胜负由时间判定,与积分无关。
    /// </summary>
    public class GameSession
    {
        /// <summary>本局已获得的积分(统计/结算展示用)。</summary>
        public int SessionPoints { get; private set; }

        public GameSession() { }

        /// <summary>
        /// 重置会话到初始状态。
        /// </summary>
        public void Reset()
        {
            SessionPoints = 0;
        }

        /// <summary>
        /// 增加本局积分。
        /// </summary>
        public void AddPoints(int amount) => SessionPoints += amount;
    }
}
