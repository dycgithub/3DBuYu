using UnityEngine;

namespace ShaderSystem.Enemy
{
    /// <summary>
    /// 通过材质属性块驱动单个能量盾 Renderer 的命中、脉冲和透明度。
    /// 接触亮边依赖 CameraDepthScanRendererFeature 在透明物体前发布的场景深度。
    /// 不创建材质实例，也不读取战斗护盾状态；战斗层需要主动调用公开方法。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class EnergyShieldMaterialDriver : MonoBehaviour
    {
        private static readonly int HitPositionId = Shader.PropertyToID("_HitPositionWS");
        private static readonly int HitRadiusId = Shader.PropertyToID("_HitRadius");
        private static readonly int HitWidthId = Shader.PropertyToID("_HitWidth");
        private static readonly int HitProgressId = Shader.PropertyToID("_HitProgress");
        private static readonly int HitStrengthId = Shader.PropertyToID("_HitStrength");
        private static readonly int PulseProgressId = Shader.PropertyToID("_PulseProgress");
        private static readonly int ShieldOpacityId = Shader.PropertyToID("_ShieldOpacity");

        [Header("命中反馈")]
        [Min(0.01f)] [SerializeField] private float hitDuration = 0.35f;
        [Min(0.001f)] [SerializeField] private float hitRadius = 0.8f;
        [Min(0.001f)] [SerializeField] private float hitWidth = 0.08f;

        [Header("默认显示")]
        [Range(0f, 1f)] [SerializeField] private float shieldOpacity = 1f;
        [Range(0f, 1f)] [SerializeField] private float pulseProgress;

        private Renderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private Vector3 _hitPosition;
        private float _hitProgress = 1f;
        private float _hitStrength;

        /// <summary>
        /// 缓存 Renderer 和属性块，确保后续驱动不重复分配材质数据容器。
        /// </summary>
        private void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// 启用时恢复当前默认属性，支持对象池重新启用。
        /// </summary>
        private void OnEnable()
        {
            Initialize();
            _hitProgress = 1f;
            _hitStrength = 0f;
            ApplyProperties();
        }

        /// <summary>
        /// 推进当前命中波纹，结束后自动清除命中强度。
        /// </summary>
        private void Update()
        {
            if (_hitStrength <= 0f)
            {
                return;
            }

            float duration = Mathf.Max(hitDuration, 0.01f);
            _hitProgress = Mathf.Clamp01(_hitProgress + Time.deltaTime / duration);
            if (_hitProgress >= 1f)
            {
                _hitStrength = 0f;
            }

            ApplyProperties();
        }

        /// <summary>
        /// 从世界坐标触发一次护盾命中波纹。
        /// </summary>
        /// <param name="worldPosition">命中点的世界坐标。</param>
        /// <param name="strength">命中强度，范围为 0 到 1。</param>
        public void SetHit(Vector3 worldPosition, float strength)
        {
            Initialize();
            _hitPosition = worldPosition;
            _hitProgress = 0f;
            _hitStrength = Mathf.Clamp01(strength);
            ApplyProperties();
        }

        /// <summary>
        /// 设置从两极向赤道推进的脉冲相位。
        /// </summary>
        /// <param name="progress">相位，范围为 0 到 1。</param>
        public void SetPulse(float progress)
        {
            Initialize();
            pulseProgress = Mathf.Repeat(progress, 1f);
            ApplyProperties();
        }

        /// <summary>
        /// 设置护盾基础透明度。
        /// </summary>
        /// <param name="opacity">透明度，范围为 0 到 1。</param>
        public void SetOpacity(float opacity)
        {
            Initialize();
            shieldOpacity = Mathf.Clamp01(opacity);
            ApplyProperties();
        }

        /// <summary>
        /// 立即移除当前命中反馈，但保留护盾的其他显示属性。
        /// </summary>
        public void ClearHit()
        {
            Initialize();
            _hitStrength = 0f;
            _hitProgress = 1f;
            ApplyProperties();
        }

        /// <summary>
        /// 获取组件上的 Renderer 并创建可复用属性块。
        /// </summary>
        private void Initialize()
        {
            _renderer ??= GetComponent<Renderer>();
            _propertyBlock ??= new MaterialPropertyBlock();
        }

        /// <summary>
        /// 将当前驱动状态写入 Renderer 的实例属性块。
        /// </summary>
        private void ApplyProperties()
        {
            if (_renderer == null || _propertyBlock == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetVector(HitPositionId, _hitPosition);
            _propertyBlock.SetFloat(HitRadiusId, Mathf.Max(hitRadius, 0.001f));
            _propertyBlock.SetFloat(HitWidthId, Mathf.Max(hitWidth, 0.001f));
            _propertyBlock.SetFloat(HitProgressId, _hitProgress);
            _propertyBlock.SetFloat(HitStrengthId, _hitStrength);
            _propertyBlock.SetFloat(PulseProgressId, pulseProgress);
            _propertyBlock.SetFloat(ShieldOpacityId, shieldOpacity);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
