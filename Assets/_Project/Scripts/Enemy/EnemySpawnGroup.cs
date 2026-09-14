using System;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 敌群中的敌人生成计划，描述同一预制体的一组敌人。
    /// 敌人类型由预制体上的 <see cref="EnemyAttributesSO"/> 决定，此处不再重复配置。
    /// </summary>
    [Serializable]
    public class EnemySpawnGroup
    {
        [Tooltip("敌人预制体(必须挂载 Enemy 组件,可选挂载行为组件)")]
        public Enemy prefab;

        [Tooltip("生成数量")]
        public int spawnCount = 5;

        [Tooltip("生成间隔(秒)")]
        public float spawnInterval = 1f;

        [Tooltip("延迟开始生成(秒,自本敌群开始计时)")]
        public float delayStart = 0f;
    }
}
