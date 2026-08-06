using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 击杀时间奖励来源:按敌人积分值换算加时(每点积分 X 秒)。
    /// 数值由 GameManager.StartLevel 从 StageConfig.killTimeRewardPerPoint 注入。
    /// 替换旧的 NullTimeRewardSource(恒返回 0)。
    /// </summary>
    public class KillTimeRewardSource : ITimeRewardSource
    {
        private float _secondsPerPoint;

        /// <summary>设置每点敌人积分换算的秒数(0 = 不加时)。</summary>
        public void SetSecondsPerPoint(float secondsPerPoint)
        {
            _secondsPerPoint = Mathf.Max(0f, secondsPerPoint);
        }

        public float GetKillTimeReward(int enemyPointsValue, int currentCombo)
        {
            if (_secondsPerPoint <= 0f || enemyPointsValue <= 0) return 0f;
            return _secondsPerPoint * enemyPointsValue;
        }
    }
}
