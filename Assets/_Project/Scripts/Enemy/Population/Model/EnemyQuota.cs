using System;

namespace GameSystem
{
    /// <summary>七档难度按从易到难排列，序号同时决定初始轨道半径。</summary>
    public enum DifficultyLevel { Easy, Normal, Advanced, Expert, Nightmare, Extreme, Impossible }
}

namespace EnemySystem
{
    /// <summary>生成配额分类，与普通、快速、坦克的群游行为类型独立。</summary>
    public enum EnemyRank { Normal, Elite, Boss }

    /// <summary>每档难度允许同时存活的三类怪物数量。</summary>
    public readonly struct EnemyQuota
    {
        public int Normal { get; }
        public int Elite { get; }
        public int Boss { get; }
        public int Total => Normal + Elite + Boss;

        private EnemyQuota(int normal, int elite, int boss)
        {
            Normal = normal;
            Elite = elite;
            Boss = boss;
        }

        /// <summary>读取固定玩法配额，不通过乘数四舍五入改变精英或 Boss 数量。</summary>
        public static EnemyQuota ForDifficulty(GameSystem.DifficultyLevel level) => level switch
        {
            GameSystem.DifficultyLevel.Easy => new(500, 0, 0),
            GameSystem.DifficultyLevel.Normal => new(800, 1, 0),
            GameSystem.DifficultyLevel.Advanced => new(1000, 1, 0),
            GameSystem.DifficultyLevel.Expert => new(1200, 10, 0),
            GameSystem.DifficultyLevel.Nightmare => new(1500, 100, 1),
            GameSystem.DifficultyLevel.Extreme => new(1800, 200, 3),
            GameSystem.DifficultyLevel.Impossible => new(1900, 95, 5),
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };

        /// <summary>取得指定生成分类的独立上限。</summary>
        public int GetLimit(EnemyRank rank) => rank switch
        {
            EnemyRank.Normal => Normal,
            EnemyRank.Elite => Elite,
            EnemyRank.Boss => Boss,
            _ => throw new ArgumentOutOfRangeException(nameof(rank))
        };
    }
}
