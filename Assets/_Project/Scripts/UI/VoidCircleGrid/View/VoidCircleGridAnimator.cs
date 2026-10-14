using DG.Tweening;
using UnityEngine;

namespace _Project.UI.VoidCircleGrid
{
    /// <summary>
    /// 使用 DOTween 曲线驱动 Image/RawImage 的圆环材质相位。
    /// 拥有独立材质副本；禁用时停止动画并恢复原材质，不创建 UI 对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoidCircleGridAnimator : MonoBehaviour
    {
        private const float CyclePhase = 4f;
        private static readonly int PhaseId = Shader.PropertyToID("_AnimationPhase");

        [SerializeField, Tooltip("使用 Custom/VoidCircleGrid 材质的 Image 或 RawImage。")]
        private UnityEngine.UI.Graphic _graphic;
        [SerializeField, Min(0.01f), Tooltip("完整变化周期，单位为秒；重新启用后从头播放。")]
        private float _cycleDuration = 4f;
        [SerializeField, Tooltip("横轴为周期时间，纵轴为图形进度；首尾应为 (0,0)、(1,1)，以连续循环。")]
        private AnimationCurve _progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Material _sourceMaterial;
        private Material _animatedMaterial;
        private Tween _tween;
        private float _phase;

        /// <summary>验证注入引用，复制材质，并启动不受 Time.timeScale 影响的循环。</summary>
        private void OnEnable()
        {
            if (_graphic == null || !_graphic.material.HasProperty(PhaseId))
            {
                Debug.LogError("请指定使用 VoidCircleGrid 材质的 Image/RawImage。", this);
                return;
            }

            _sourceMaterial = _graphic.material;
            _animatedMaterial = new Material(_sourceMaterial);
            _graphic.material = _animatedMaterial;
            SetPhase(0f);

            _tween = DOTween.To(() => _phase, SetPhase, CyclePhase, Mathf.Max(0.01f, _cycleDuration))
                .SetEase(_progressCurve)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true);
        }

        /// <summary>写入曲线求值后的相位，不修改其他 UI 共用的材质资产。</summary>
        /// <param name="phase">完整周期为 4 的动画相位；允许曲线反向或超出范围。</param>
        private void SetPhase(float phase)
        {
            _phase = phase;
            _animatedMaterial.SetFloat(PhaseId, phase);
        }

        /// <summary>停止回调后恢复原材质，再释放由本组件创建的副本。</summary>
        private void OnDisable()
        {
            _tween?.Kill();
            _tween = null;

            if (_animatedMaterial == null)
            {
                return;
            }

            if (_graphic != null && _graphic.material == _animatedMaterial)
            {
                _graphic.material = _sourceMaterial;
            }

            if (Application.isPlaying)
            {
                Destroy(_animatedMaterial);
            }
            else
            {
                DestroyImmediate(_animatedMaterial);
            }

            _animatedMaterial = null;
            _sourceMaterial = null;
        }
    }
}
