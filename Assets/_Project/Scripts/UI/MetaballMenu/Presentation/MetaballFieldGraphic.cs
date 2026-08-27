using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.MetaballMenu
{
    /// <summary>
    /// 在 UGUI 矩形内绘制多个平滑融合球体的图形层。
    /// 球体数据由菜单控制器提供，本类不处理输入或业务状态。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MetaballFieldGraphic : MaskableGraphic
    {
        public const int MaxBallCount = 16;

        private static readonly int BallDataId = Shader.PropertyToID("_BallData");
        private static readonly int BallColorsId = Shader.PropertyToID("_BallColors");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

        [SerializeField] private Shader _shader;
        [SerializeField, Range(0.1f, 4f)] private float _threshold = 1f;
        [SerializeField, Range(0.001f, 0.5f)] private float _softness = 0.06f;

        private readonly Vector2[] _ballPositions = new Vector2[MaxBallCount];
        private readonly float[] _ballRadii = new float[MaxBallCount];
        private readonly Color[] _ballColors = new Color[MaxBallCount];
        private readonly bool[] _ballActive = new bool[MaxBallCount];
        private readonly Vector4[] _ballData = new Vector4[MaxBallCount];
        private readonly Vector4[] _shaderColors = new Vector4[MaxBallCount];

        private Material _runtimeMaterial;
        private bool _shaderErrorLogged;

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureMaterial();
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = Color.white;

            vertex.position = new Vector3(rect.xMin, rect.yMin);
            vertex.uv0 = new Vector2(0f, 0f);
            vertexHelper.AddVert(vertex);

            vertex.position = new Vector3(rect.xMin, rect.yMax);
            vertex.uv0 = new Vector2(0f, 1f);
            vertexHelper.AddVert(vertex);

            vertex.position = new Vector3(rect.xMax, rect.yMax);
            vertex.uv0 = new Vector2(1f, 1f);
            vertexHelper.AddVert(vertex);

            vertex.position = new Vector3(rect.xMax, rect.yMin);
            vertex.uv0 = new Vector2(1f, 0f);
            vertexHelper.AddVert(vertex);

            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);
        }

        protected override void UpdateMaterial()
        {
            base.UpdateMaterial();
            if (_runtimeMaterial != null)
                canvasRenderer.SetMaterial(_runtimeMaterial, 0);
        }

        private void LateUpdate()
        {
            ApplyBallsToMaterial();
        }

        /// <summary>清除本帧所有球体数据。</summary>
        public void ClearBalls()
        {
            for (int i = 0; i < MaxBallCount; i++)
                _ballActive[i] = false;
        }

        /// <summary>
        /// 设置一个球体的菜单本地坐标、半径和颜色。
        /// </summary>
        public void SetBall(int index, Vector2 localPosition, float radius, Color ballColor)
        {
            if (index < 0 || index >= MaxBallCount)
                return;

            _ballPositions[index] = localPosition;
            _ballRadii[index] = Mathf.Max(0f, radius);
            _ballColors[index] = ballColor;
            _ballActive[index] = true;
        }

        private void ApplyBallsToMaterial()
        {
            if (_runtimeMaterial == null)
                return;

            Rect rect = rectTransform.rect;
            float width = Mathf.Max(1f, rect.width);
            float height = Mathf.Max(1f, rect.height);
            float aspect = width / height;
            Vector2 rectCenter = rect.center;

            for (int i = 0; i < MaxBallCount; i++)
            {
                Vector2 position = _ballPositions[i];
                _ballData[i] = new Vector4(
                    0.5f + (position.x - rectCenter.x) / width,
                    0.5f + (position.y - rectCenter.y) / height,
                    _ballRadii[i] / height,
                    _ballActive[i] ? 1f : 0f);
                _shaderColors[i] = _ballColors[i];
            }

            _runtimeMaterial.SetFloat(AspectId, aspect);
            _runtimeMaterial.SetFloat(ThresholdId, Mathf.Max(0.1f, _threshold));
            _runtimeMaterial.SetFloat(SoftnessId, Mathf.Max(0.001f, _softness));
            _runtimeMaterial.SetVectorArray(BallDataId, _ballData);
            _runtimeMaterial.SetVectorArray(BallColorsId, _shaderColors);
        }

        private void EnsureMaterial()
        {
            if (_runtimeMaterial != null)
                return;

            Shader shader = _shader != null ? _shader : Shader.Find("UI/MetaballField");
            if (shader == null)
            {
                if (!_shaderErrorLogged)
                {
                    Debug.LogError("[MetaballFieldGraphic] 未配置 Metaball Shader。", this);
                    _shaderErrorLogged = true;
                }

                return;
            }

            _runtimeMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        protected override void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_runtimeMaterial);
                else
                    DestroyImmediate(_runtimeMaterial);
            }

            base.OnDestroy();
        }
    }
}
