using EffectSystem;
using Services;
using UnityEngine;
using VContainer;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>独立预览战斗特效目录，不代表技能或子弹实际触发链路。</summary>
    public sealed class CombatPlaygroundEffectPreview : MonoBehaviour
    {
        private static readonly EffectId[] PreviewEffectIds =
        {
            EffectId.EnemyHit,
            EffectId.EnemyDeath,
            EffectId.ShieldHit,
            EffectId.EnemyDodge,
            EffectId.BigExplosion,
            EffectId.BulletHit,
            EffectId.BulletExpired,
            EffectId.TransmitterFire
        };

        [Inject] private IEffectService _effects;

        /// <summary>读取静态视效预览按钮数量。</summary>
        internal int EffectCount => PreviewEffectIds.Length;

        /// <summary>在指定目标或位置播放目录特效。</summary>
        internal string Play(int index, Vector3 position, Transform parent)
        {
            if (_effects == null || index < 0 || index >= PreviewEffectIds.Length)
            {
                return "视效预览失败：目录或索引不可用";
            }

            _effects.Play(PreviewEffectIds[index], position, parent);
            return $"独立预览：{PreviewEffectIds[index]}";
        }
    }
}
