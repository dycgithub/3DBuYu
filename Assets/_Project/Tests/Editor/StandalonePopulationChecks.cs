#if POPULATION_STANDALONE
using System;
using EnemySystem;
using GameSystem;

/// <summary>无 Unity 进程的数量调度验证，直接编译生产调度代码运行。</summary>
internal static class StandalonePopulationChecks
{
    private static void Main()
    {
        int[][] quotas = { new[] {500,0,0}, new[] {800,1,0}, new[] {1000,1,0},
            new[] {1200,10,0}, new[] {1500,100,1}, new[] {1800,200,3}, new[] {1900,95,5} };
        for (int level = 0; level < quotas.Length; level++)
        {
            EnemyQuota quota = EnemyQuota.ForDifficulty((DifficultyLevel)level);
            int[] counts = new int[3];
            EnemyPopulationSchedule schedule = new(quota);
            Func<EnemyRank, int> count = rank => counts[(int)rank];
            Func<EnemyRank, bool> spawn = rank => { counts[(int)rank]++; return true; };
            schedule.Tick(2.5f, count, spawn);
            Check(counts[0] == quotas[level][0] / 2, "half initial");
            schedule.Tick(2.5f, count, spawn);
            for (int rank = 0; rank < 3; rank++)
            {
                Check(counts[rank] == quotas[level][rank], "initial quotas");
            }
            Check(schedule.InitialCompleted, "initial complete");
            Array.Clear(counts, 0, counts.Length);
            schedule.Tick(1f, count, spawn);
            Check(counts[0] == 50 && counts[1] == 0 && counts[2] == 0, "one second rates");
            schedule.Tick(4f, count, spawn);
            Check(counts[1] == Math.Min(1, quota.Elite), "elite interval");
            schedule.Tick(55f, count, spawn);
            Check(counts[2] == Math.Min(1, quota.Boss), "boss interval");
            for (int i = 0; i < 300; i++)
            {
                schedule.Tick(5f, count, spawn);
            }
            Check(!schedule.IsRefilling, "refill complete");
            for (int rank = 0; rank < 3; rank++)
            {
                Check(counts[rank] == quotas[level][rank], "refill quotas");
            }
        }
        EnemyPopulationSchedule easy = new(EnemyQuota.ForDifficulty(DifficultyLevel.Easy));
        int alive = 0;
        int Count(EnemyRank rank) => rank == EnemyRank.Normal ? alive : 0;
        bool Spawn(EnemyRank rank) { alive++; return true; }
        easy.Tick(5f, Count, Spawn);
        alive = 400;
        easy.Tick(1f, Count, Spawn);
        Check(alive == 400, "strict eighty percent");
        alive = 399;
        easy.Tick(1f, Count, Spawn);
        Check(alive == 449, "start refill");
        easy.Tick(2f, Count, Spawn);
        Check(alive == 500 && !easy.IsRefilling, "continue above threshold");
        Console.WriteLine("PASS: 7 difficulty quotas, initial distribution, independent rates, refill completion, strict threshold.");
    }

    private static void Check(bool condition, string rule)
    {
        if (!condition)
        {
            throw new InvalidOperationException(rule);
        }
    }
}
#endif
