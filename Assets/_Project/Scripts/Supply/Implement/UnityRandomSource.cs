using System;
using UnityEngine;

namespace Supply
{
    /// <summary>将 Unity 随机数实现适配为 Supply 使用的随机源。</summary>
    public sealed class UnityRandomSource : IRandomSource
    {
        /// <summary>返回指定上限以内的随机整数。</summary>
        /// <param name="exclusiveMax">随机上限，不包含该值。</param>
        /// <returns>随机整数。</returns>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            }

            return UnityEngine.Random.Range(0, exclusiveMax);
        }

        /// <summary>返回 Unity 生成的随机浮点值。</summary>
        /// <returns>范围为 0 到 1 的随机浮点值。</returns>
        public float NextFloat()
        {
            return UnityEngine.Random.value;
        }
    }
}
