using UnityEngine;

namespace Services
{
    /// <summary>管理短生命周期视觉对象的播放、停止与回收。</summary>
    public interface IPooledEffectService
    {
        GameObject Play(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float lifetime,
            Transform parent = null);

        void Stop(GameObject instance);
    }
}
