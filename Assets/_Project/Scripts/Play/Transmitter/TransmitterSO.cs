using System;
using UnityEngine;
using CombatSystem;

namespace Play
{
    /// <summary>
    /// 炮台发射口配置 ScriptableObject。
    /// 每个发射口独立配置位置、朝向、默认子弹、背包映射。
    /// 多个发射口 = 多管炮台，可独立发射不同子弹。
    /// </summary>
    [CreateAssetMenu(menuName = "Play/Port")]
    public class TransmitterSO : ScriptableObject
    {
        [Header("基本信息")]
        public string portName = "主炮口";

        [Header("瞄准")]
        [Tooltip("端口跟踪目标的旋转速度（度/秒）。")]
        [Range(1f, 60f)]
        public float trackingSpeed = 10f;

        [Header("锁定状态")]
        [Tooltip("初始是否锁定（需 PortExpander 道具解锁）。锁定端口不可开火、不可瞄准。")]
        public bool isInitiallyLocked = false;

        [Header("模型")]
        [Tooltip("发射口可视化模型预制体（可选）。为空则不显示模型。")]
        public GameObject portModelPrefab;

        [Tooltip("锁定状态下使用的模型预制体（可选，为空则使用 portModelPrefab 或隐藏）。")]
        public GameObject lockedModelPrefab;

        [Header("默认子弹")]
        [Tooltip("当背包未装备对应武器时使用的弱子弹。")]
        public BulletProfile defaultBullet;
    }
}
