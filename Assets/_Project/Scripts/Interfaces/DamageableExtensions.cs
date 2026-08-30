using UnityEngine;

namespace Interfaces
{
    /// <summary>统一处理 Unity 对象销毁后仍被接口引用的情况。</summary>
    public static class DamageableExtensions
    {
        public static bool IsAliveAndValid(this IDamageable target)
        {
            if (target == null)
                return false;

            if (target is Object unityObject && unityObject == null)
                return false;

            return target.IsAlive;
        }
    }
}
