using UnityEngine;

namespace Services
{
    /// <summary>
    /// 特效/视觉反馈服务接口。
    /// 替代直接调用 <see cref="EffectSystem.EffectManager.Instance"/>，
    /// 使 EnemyBase、Turret 等通过依赖注入获取特效能力。
    /// </summary>
    public interface IEffectService
    {
        /// <summary>在指定位置播放一个特效。</summary>
        void Play(string effectName, Vector3 position);

        /// <summary>注册一个特效预制体到服务中。</summary>
        void RegisterEffect(string name, GameObject prefab);

        /// <summary>停止指定名称的所有特效。</summary>
        void Stop(string effectName);
    }
}
