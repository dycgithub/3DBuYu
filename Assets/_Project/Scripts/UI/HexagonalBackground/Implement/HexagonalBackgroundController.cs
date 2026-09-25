using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _Project.UI.HexagonalBackground.Implement
{
    /// <summary>
    /// 仅供 UIScene 背景使用的鼠标位置和五边形断边控制器。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class HexagonalBackgroundController : MonoBehaviour
    {
        private const string ShaderName = "Unlit/Hexagonal Sampling";
        private const float MouseMoveThreshold = 0.0015f;
        private const float NoImpactAge = 10f;

        private static readonly int PentagonImpactId = Shader.PropertyToID("_PentagonImpact");

        private Image _background;
        private Material _material;
        private Vector2 _impactUv;
        private float _impactAge = NoImpactAge;
        private bool _hasMouseSample;

        /// <summary>获取背景 Image 并创建独立材质。</summary>
        private void Awake()
        {
            _background = GetComponent<Image>();
            CreateMaterial();
        }

        /// <summary>读取鼠标位置并提交当前输入及最近一次断边影响。</summary>
        private void Update()
        {
            if (_material == null)
            {
                return;
            }

            Vector2 mouseUv = ReadMouseUv();
            UpdateImpact(mouseUv);
            _material.SetVector(
                PentagonImpactId,
                new Vector4(_impactUv.x, _impactUv.y, _impactAge, _hasMouseSample ? 1f : 0f));
        }

        /// <summary>释放背景专用材质实例，不修改场景材质资产。</summary>
        private void OnDestroy()
        {
            if (_material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_material);
            }
            else
            {
                DestroyImmediate(_material);
            }
        }

        /// <summary>记录鼠标移动产生的断边位置，并推进修复计时。</summary>
        private void UpdateImpact(Vector2 mouseUv)
        {
            if (!_hasMouseSample)
            {
                _impactUv = mouseUv;
                _hasMouseSample = true;
                return;
            }

            Vector2 movement = mouseUv - _impactUv;
            if (movement.sqrMagnitude > MouseMoveThreshold * MouseMoveThreshold)
            {
                _impactUv = mouseUv;
                _impactAge = 0f;
            }
            else
            {
                _impactAge = Mathf.Min(
                    _impactAge + Time.unscaledDeltaTime,
                    NoImpactAge);
            }
        }

        /// <summary>为背景 Image 创建独立材质实例。</summary>
        private void CreateMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return;
            }

            Material source = _background.material;
            _material = source != null && source.shader == shader
                ? new Material(source)
                : new Material(shader);
            _material.name = $"{name}_{ShaderName}";
            _material.hideFlags = HideFlags.HideAndDontSave;
            _background.material = _material;
        }

        /// <summary>读取当前屏幕鼠标位置并转为 0 到 1 的 UI 坐标。</summary>
        private static Vector2 ReadMouseUv()
        {
            Vector2 screenSize = new(Screen.width, Screen.height);
            if (screenSize.x <= 0f || screenSize.y <= 0f)
            {
                return new Vector2(0.5f, 0.5f);
            }

            Vector2 screenPosition = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : screenSize * 0.5f;
            return new Vector2(
                Mathf.Clamp01(screenPosition.x / screenSize.x),
                Mathf.Clamp01(1f - screenPosition.y / screenSize.y));
        }
    }
}
