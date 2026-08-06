using System;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 波次中的敌人生成计划(单组敌人)。
    /// 敌人类型由预制体上的 <see cref="EnemyAttributes"/> 决定,此处不再冗余配置。
    /// </summary>
    [Serializable]
    public class WaveEnemyInfo
    {
        [Tooltip("敌人预制体(必须挂载 Enemy 组件,可选挂载行为组件)")]
        public Enemy prefab;

        [Tooltip("生成数量")]
        public int spawnCount = 5;

        [Tooltip("生成间隔(秒)")]
        public float spawnInterval = 1f;

        [Tooltip("延迟开始生成(秒,自本波次开始计时)")]
        public float delayStart = 0f;
    }
}
