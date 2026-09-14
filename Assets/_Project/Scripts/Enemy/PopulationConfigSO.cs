using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 敌群配置集合，按顺序持有所有 <see cref="PopulationDataSO"/>。
    /// 按 populations 列表顺序推进，完成后由数量服务发布事实事件。
    /// </summary>
    [CreateAssetMenu(fileName = "PopulationConfig", menuName = "Game/Population Config")]
    public class PopulationConfigSO : ScriptableObject
    {
        [Header("敌群列表")]
        [Tooltip("所有敌群数据，按列表顺序依次进行")]
        public List<PopulationDataSO> populations = new List<PopulationDataSO>();

        [Header("全局设置")]
        [Tooltip("初始敌群编号(1 起)")]
        public int startPopulation = 1;

        /// <summary>
        /// 获取配置中的敌群数量。
        /// </summary>
        /// <returns>敌群列表中的有效元素总数。</returns>
        public int GetTotalPopulationCount()
        {
            return populations != null ? populations.Count : 0;
        }

        /// <summary>
        /// 获取指定编号的敌群配置。
        /// </summary>
        /// <param name="populationNumber">从 1 开始的敌群编号。</param>
        /// <returns>对应敌群配置；编号越界或列表为空时返回 null。</returns>
        public PopulationDataSO GetPopulation(int populationNumber)
        {
            if (populations == null)
            {
                return null;
            }

            if (populationNumber < 1 || populationNumber > populations.Count)
            {
                return null;
            }

            return populations[populationNumber - 1];
        }
    }
}
