using DG.Tweening;
using CombatSystem;
using EnemySystem;
using UnityEngine;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>
    /// 管理实验场景发射器和敌人的局部 Juice，不修改正式 Prefab 或表现配置。
    /// </summary>
    public sealed class CombatPlaygroundJuice : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Enemy target;
        [SerializeField] private Transform targetVisual;
        [SerializeField] private Transform emitterVisual;

        private Renderer[] _targetRenderers;
        private Renderer[] _emitterRenderers;
        private Vector3 _targetScale;
        private Vector3 _emitterScale;
        private Tween _targetPunch;
        private Tween _emitterPunch;
        private readonly FlashState _targetFlash = new();
        private readonly FlashState _emitterFlash = new();
        private float _scalePunch = 0.35f;
        private float _punchDuration = 0.24f;
        private float _flashDuration = 0.16f;
        private Color _flashColor = new(1f, 0.38f, 0.16f, 1f);

        private sealed class FlashState
        {
            public Tween Tween;
            public MaterialPropertyBlock[] OriginalBlocks;
        }

        /// <summary>缓存目标和发射器的视觉节点。</summary>
        private void Awake()
        {
            CacheVisuals();
        }

        /// <summary>订阅目标命中和死亡事件。</summary>
        private void OnEnable()
        {
            CacheVisuals();
            Enemy.AnyDamageApplied += OnDamageApplied;
            if (target != null)
            {
                target.OnDied += OnTargetDied;
            }
        }

        /// <summary>解除事件并清理当前表现。</summary>
        private void OnDisable()
        {
            Enemy.AnyDamageApplied -= OnDamageApplied;
            if (target != null)
            {
                target.OnDied -= OnTargetDied;
            }

            StopAllJuice();
        }

        /// <summary>更新会话内 Juice 参数。</summary>
        public void SetTuning(
            float scalePunch,
            float punchDuration,
            Color flashColor,
            float flashDuration)
        {
            _scalePunch = Mathf.Clamp01(scalePunch);
            _punchDuration = Mathf.Max(0.01f, punchDuration);
            _flashColor = flashColor;
            _flashDuration = Mathf.Max(0f, flashDuration);
        }

        /// <summary>播放发射器蓄力表现。</summary>
        public void PlayCharge(float duration)
        {
            PlayPunch(emitterVisual, ref _emitterPunch, 0.06f, Mathf.Max(0f, duration));
        }

        /// <summary>播放发射前的发射器闪烁表现。</summary>
        public void PlayBeforeFire()
        {
            PlayFlash(_emitterRenderers, _emitterFlash, Color.cyan, 0.08f);
        }

        /// <summary>播放发射成功或失败后的发射器反馈。</summary>
        public void PlayAfterFire(bool succeeded)
        {
            float intensity = succeeded ? _scalePunch : _scalePunch * 0.5f;
            PlayPunch(emitterVisual, ref _emitterPunch, intensity, _punchDuration);
        }

        /// <summary>手动播放一次当前敌人的命中 Juice。</summary>
        public void PlayTargetHit()
        {
            PlayPunch(targetVisual, ref _targetPunch, _scalePunch, _punchDuration);
            PlayFlash(_targetRenderers, _targetFlash, _flashColor, _flashDuration);
        }

        /// <summary>手动播放一次当前敌人的死亡 Juice。</summary>
        public void PlayTargetDeath()
        {
            PlayFlash(_targetRenderers, _targetFlash, Color.yellow, _flashDuration);
            PlayPunch(targetVisual, ref _targetPunch, _scalePunch * 1.25f, _punchDuration);
        }

        /// <summary>恢复实验场景中的两个视觉节点。</summary>
        public void ResetVisuals()
        {
            StopAllJuice();
        }

        /// <summary>缓存场景中预先创建的 Renderer 和初始缩放。</summary>
        private void CacheVisuals()
        {
            if (targetVisual == null && target != null)
            {
                targetVisual = target.transform;
            }

            if (emitterVisual == null)
            {
                emitterVisual = transform;
            }

            _targetRenderers = targetVisual != null
                ? targetVisual.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            _emitterRenderers = emitterVisual != null
                ? emitterVisual.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            _targetScale = targetVisual != null ? targetVisual.localScale : Vector3.one;
            _emitterScale = emitterVisual != null ? emitterVisual.localScale : Vector3.one;
        }

        /// <summary>接收当前目标的真实伤害事件并补充缩放脉冲。</summary>
        private void OnDamageApplied(Enemy enemy, DamageResult result)
        {
            if (enemy == target && result.WasApplied)
            {
                PlayPunch(targetVisual, ref _targetPunch, _scalePunch, _punchDuration);
            }
        }

        /// <summary>接收当前目标的死亡事件并补充缩放脉冲。</summary>
        private void OnTargetDied(Enemy enemy)
        {
            PlayPunch(targetVisual, ref _targetPunch, _scalePunch * 1.25f, _punchDuration);
        }

        /// <summary>播放指定视觉节点的缩放脉冲。</summary>
        private void PlayPunch(
            Transform visual,
            ref Tween tween,
            float intensity,
            float duration)
        {
            if (visual == null || duration <= 0f)
            {
                return;
            }

            tween?.Kill();
            visual.localScale = visual == targetVisual ? _targetScale : _emitterScale;
            tween = visual.DOPunchScale(
                    Vector3.one * Mathf.Max(0f, intensity),
                    duration,
                    8,
                    0.75f)
                .SetUpdate(true)
                .OnComplete(() => RestoreScale(visual));
        }

        /// <summary>播放指定 Renderer 的颜色闪烁。</summary>
        private void PlayFlash(
            Renderer[] renderers,
            FlashState state,
            Color color,
            float duration)
        {
            if (renderers == null || renderers.Length == 0 || duration <= 0f)
            {
                return;
            }

            state.Tween?.Kill();
            RestorePropertyBlocks(renderers, state.OriginalBlocks);
            state.OriginalBlocks = CapturePropertyBlocks(renderers);
            MaterialPropertyBlock[] originalBlocks = state.OriginalBlocks;
            state.Tween = DOTween.Sequence()
                .Append(DOVirtual.Float(0f, 1f, duration * 0.5f, value => ApplyColor(renderers, color, value)))
                .Append(DOVirtual.Float(1f, 0f, duration * 0.5f, value => ApplyColor(renderers, color, value)))
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    RestorePropertyBlocks(renderers, originalBlocks);
                    if (ReferenceEquals(state.OriginalBlocks, originalBlocks))
                    {
                        state.OriginalBlocks = null;
                    }
                });
        }

        /// <summary>按闪烁进度写入材质属性块。</summary>
        private static void ApplyColor(Renderer[] renderers, Color color, float progress)
        {
            Color current = Color.Lerp(Color.white, color, Mathf.Clamp01(progress));
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = new();
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, current);
                block.SetColor(ColorId, current);
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>缓存当前材质属性块，便于 Juice 结束后恢复其他表现。</summary>
        private static MaterialPropertyBlock[] CapturePropertyBlocks(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }

            MaterialPropertyBlock[] blocks = new MaterialPropertyBlock[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = new();
                renderers[index].GetPropertyBlock(block);
                blocks[index] = block;
            }

            return blocks;
        }

        /// <summary>恢复 Juice 覆盖前的材质属性块。</summary>
        private static void RestorePropertyBlocks(
            Renderer[] renderers,
            MaterialPropertyBlock[] blocks)
        {
            if (renderers == null || blocks == null)
            {
                return;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].SetPropertyBlock(blocks[index]);
                }
            }
        }

        /// <summary>恢复指定节点的初始缩放。</summary>
        private void RestoreScale(Transform visual)
        {
            if (visual == null)
            {
                return;
            }

            if (visual == targetVisual)
            {
                visual.localScale = _targetScale;
            }
            else if (visual == emitterVisual)
            {
                visual.localScale = _emitterScale;
            }
        }

        /// <summary>停止 Tween 并恢复初始视觉。</summary>
        private void StopAllJuice()
        {
            _targetPunch?.Kill();
            _emitterPunch?.Kill();
            _targetFlash.Tween?.Kill();
            _emitterFlash.Tween?.Kill();
            _targetPunch = null;
            _emitterPunch = null;
            RestorePropertyBlocks(_targetRenderers, _targetFlash.OriginalBlocks);
            RestorePropertyBlocks(_emitterRenderers, _emitterFlash.OriginalBlocks);
            _targetFlash.Tween = null;
            _emitterFlash.Tween = null;
            _targetFlash.OriginalBlocks = null;
            _emitterFlash.OriginalBlocks = null;
            RestoreScale(targetVisual);
            RestoreScale(emitterVisual);
        }
    }
}
