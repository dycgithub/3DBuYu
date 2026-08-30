using UnityEngine;
using EffectSystem;

namespace Services
{
    /// <summary>
    /// 特效/视觉反馈服务接口。
    /// 替代直接调用 <see cref="EffectSystem.EffectManager.Instance"/>，
    /// 使战斗逻辑通过依赖注入获取类型化的特效能力。
    /// </summary>
    public interface IEffectService
    {
        /// <summary>在指定位置播放一个目录特效。</summary>
        void Play(EffectId effectId, Vector3 position);

        /// <summary>在指定位置播放一个目录特效，可选地挂到目标上。</summary>
        void Play(EffectId effectId, Vector3 position, Transform parent);

        /// <summary>停止指定键对应的所有特效。</summary>
        void Stop(EffectId effectId);
    }
}
