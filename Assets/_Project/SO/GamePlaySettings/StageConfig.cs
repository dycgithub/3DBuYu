using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 关卡(单局)玩法参数 — 纯生存制。
    /// 倒计时自然归零(撑过目标时长)= 胜利;时间被惩罚扣光 = 失败;击杀加时提高容错。
    /// </summary>
    [CreateAssetMenu(fileName = "StageConfig", menuName = "Game/Stage Config")]
    public class StageConfig : ScriptableObject
    {
        [Tooltip("单局目标时长（秒）。倒计时自然归零即胜利。")]
        public float timeLimit = 300f;

        [Tooltip("胜利奖励积分(可配置,0 = 无奖励)。")]
        public int victoryRewardPoints = 300;

        [Tooltip("击杀时间奖励:每点敌人积分换算的秒数(0 = 不加时)。Normal 30→1.5s / Fast 40→2s / Tank 60→3s。")]
        public float killTimeRewardPerPoint = 0.05f;
    }
}
