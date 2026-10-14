using System;
using System.Collections.Generic;
using R3;
using Services;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.MousePointer
{
    /// <summary>绘制跟随鼠标的准星，以及按住状态区分的 Sprite 痕迹。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MousePointerGraphic : Graphic
    {
        private const int MaximumTrailStamps = 128;
        private const int CircleSegments = 24;

        private enum TrailType
        {
            Free,
            Held
        }

        private struct TrailStamp
        {
            public Vector2 Position;
            public TrailType Type;
            public float CreatedAt;
            public float Lifetime;
        }

        [Header("痕迹")]
        [SerializeField] private Sprite trailSprite;
        [SerializeField, Min(0.05f)] private float heldTrailDuration = 0.8f;
        [SerializeField, Min(0.05f)] private float freeTrailDuration = 0.2f;
        [SerializeField, Min(1f)] private float stampSpacing = 12f;
        [SerializeField, Min(0f)] private float stampRandomOffset = 6f;
        [SerializeField, Min(1f)] private float heldStampSize = 20f;
        [SerializeField, Min(1f)] private float freeStampSize = 13f;
        [SerializeField] private Color heldTrailColor = new(1f, 0.42f, 0.18f, 0.9f);
        [SerializeField] private Color freeTrailColor = new(0.2f, 0.9f, 1f, 0.85f);

        [Header("准星")]
        [SerializeField, Min(1f)] private float reticleRadius = 10f;
        [SerializeField, Min(1f)] private float reticleStroke = 2f;
        [SerializeField, Min(1f)] private float reticleGap = 4f;
        [SerializeField] private Color reticleColor = Color.white;

        [Inject] private IInputService _input;

        private readonly List<TrailStamp> _trailStamps = new();
        private readonly System.Random _random = new();
        private IDisposable _mouseFireSubscription;
        private Vector2 _pointerPosition;
        private float _distanceToNextStamp;
        private TrailType _trailType;
        private bool _hasPointerPosition;

        /// <summary>使用痕迹 Sprite 的纹理，让单个 Canvas Graphic 批量绘制所有图章。</summary>
        public override Texture mainTexture => trailSprite != null
            ? trailSprite.texture
            : Texture2D.whiteTexture;

        /// <summary>关闭射线拦截，并在容器注入前后尝试订阅鼠标状态。</summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            SubscribeToInput();
        }

        /// <summary>释放 R3 订阅并清除旧轨迹，避免禁用后残留输入状态。</summary>
        protected override void OnDisable()
        {
            _mouseFireSubscription?.Dispose();
            _mouseFireSubscription = null;
            _trailStamps.Clear();
            _hasPointerPosition = false;
            base.OnDisable();
        }

        /// <summary>采样指针位置；Canvas 暂停时停止记录轨迹。</summary>
        private void Update()
        {
            if (_input == null || canvas == null)
            {
                return;
            }

            SubscribeToInput();
            if (!canvas.enabled)
            {
                _trailStamps.Clear();
                _hasPointerPosition = false;
                _distanceToNextStamp = stampSpacing;
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    _input.PointerPosition,
                    null,
                    out Vector2 currentPosition))
            {
                return;
            }

            bool pointerMoved = UpdatePointerPosition(currentPosition, Time.unscaledTime);
            bool removedStamps = RemoveExpiredStamps(Time.unscaledTime);
            if (pointerMoved || removedStamps || _trailStamps.Count > 0)
            {
                SetVerticesDirty();
            }
        }

        /// <summary>绘制所有未过期图章和始终跟随指针的准星。</summary>
        /// <param name="vertexHelper">本帧接收 UI 顶点的网格构建器。</param>
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!_hasPointerPosition)
            {
                return;
            }

            float currentTime = Time.unscaledTime;
            for (int index = 0; index < _trailStamps.Count; index++)
            {
                AppendTrailStamp(vertexHelper, _trailStamps[index], currentTime);
            }

            AppendPointerIcon(vertexHelper);
        }

        /// <summary>订阅输入服务的 R3 按住状态，并将其映射到痕迹类型。</summary>
        private void SubscribeToInput()
        {
            if (_mouseFireSubscription != null || _input?.MouseFireHeld == null)
            {
                return;
            }

            _mouseFireSubscription = _input.MouseFireHeld.Subscribe(HandleMouseFireChanged);
        }

        /// <summary>按 R3 发布的门控状态切换后续痕迹类型。</summary>
        /// <param name="isHeld">当前游戏是否接受鼠标按住输入。</param>
        private void HandleMouseFireChanged(bool isHeld)
        {
            _trailType = isHeld ? TrailType.Held : TrailType.Free;
            SetVerticesDirty();
        }

        /// <summary>更新准星位置，并沿移动线段按固定间距加入 Sprite 图章。</summary>
        /// <param name="currentPosition">当前 Canvas 局部坐标。</param>
        /// <param name="currentTime">当前非缩放时间。</param>
        /// <returns>指针位置是否发生变化。</returns>
        private bool UpdatePointerPosition(Vector2 currentPosition, float currentTime)
        {
            if (!_hasPointerPosition)
            {
                _pointerPosition = currentPosition;
                _distanceToNextStamp = stampSpacing;
                _hasPointerPosition = true;
                return true;
            }

            Vector2 previousPosition = _pointerPosition;
            _pointerPosition = currentPosition;
            if ((currentPosition - previousPosition).sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            AddStampsAlongSegment(previousPosition, currentPosition, currentTime);
            return true;
        }

        /// <summary>在快速移动的两个采样点之间补齐等距痕迹图章。</summary>
        /// <param name="start">移动线段起点。</param>
        /// <param name="end">移动线段终点。</param>
        /// <param name="currentTime">图章创建时间。</param>
        private void AddStampsAlongSegment(Vector2 start, Vector2 end, float currentTime)
        {
            Vector2 movement = end - start;
            float segmentLength = movement.magnitude;
            if (segmentLength <= Mathf.Epsilon)
            {
                return;
            }

            Vector2 perpendicular = new Vector2(-movement.y, movement.x) / segmentLength;
            float distanceTravelled = 0f;
            while (distanceTravelled + _distanceToNextStamp <= segmentLength)
            {
                distanceTravelled += _distanceToNextStamp;
                Vector2 stampPosition = Vector2.LerpUnclamped(
                    start,
                    end,
                    distanceTravelled / segmentLength) + GetStampOffset(perpendicular);
                AddTrailStamp(stampPosition, currentTime);
                _distanceToNextStamp = stampSpacing;
            }

            _distanceToNextStamp -= segmentLength - distanceTravelled;
        }

        /// <summary>沿路径法线生成一次性随机偏移，不使用全局 Unity 随机状态。</summary>
        /// <param name="perpendicular">已归一化的路径法线。</param>
        /// <returns>路径法线方向上的随机偏移。</returns>
        private Vector2 GetStampOffset(Vector2 perpendicular)
        {
            float magnitude = (float)(_random.NextDouble() * 2.0 - 1.0) * stampRandomOffset;
            return perpendicular * magnitude;
        }

        /// <summary>记录一个带创建时状态和独立寿命的轨迹图章。</summary>
        /// <param name="position">图章 Canvas 局部坐标。</param>
        /// <param name="createdAt">图章创建时间。</param>
        private void AddTrailStamp(Vector2 position, float createdAt)
        {
            if (_trailStamps.Count >= MaximumTrailStamps)
            {
                _trailStamps.RemoveAt(0);
            }

            _trailStamps.Add(new TrailStamp
            {
                Position = position,
                Type = _trailType,
                CreatedAt = createdAt,
                Lifetime = GetTrailLifetime(_trailType)
            });
        }

        /// <summary>移除各自达到寿命的旧图章。</summary>
        /// <param name="currentTime">当前非缩放时间。</param>
        /// <returns>是否至少移除了一个图章。</returns>
        private bool RemoveExpiredStamps(float currentTime)
        {
            bool removedStamps = false;
            for (int index = _trailStamps.Count - 1; index >= 0; index--)
            {
                TrailStamp stamp = _trailStamps[index];
                if (currentTime - stamp.CreatedAt < stamp.Lifetime)
                {
                    continue;
                }

                _trailStamps.RemoveAt(index);
                removedStamps = true;
            }

            return removedStamps;
        }

        /// <summary>按创建时的痕迹类型绘制带透明度的现有 Sprite 图章。</summary>
        /// <param name="vertexHelper">接收图章顶点的网格构建器。</param>
        /// <param name="stamp">要绘制的图章数据。</param>
        /// <param name="currentTime">当前非缩放时间。</param>
        private void AppendTrailStamp(VertexHelper vertexHelper, TrailStamp stamp, float currentTime)
        {
            if (trailSprite == null)
            {
                return;
            }

            float alpha = GetStampAlpha(stamp, currentTime);
            if (alpha <= 0f)
            {
                return;
            }

            float size = stamp.Type == TrailType.Held ? heldStampSize : freeStampSize;
            float aspect = trailSprite.rect.width / Mathf.Max(1f, trailSprite.rect.height);
            Vector2 halfSize = new(size * 0.5f, size / aspect * 0.5f);
            Rect uv = GetSpriteUv(trailSprite);
            Color color = GetTrailColor(stamp.Type);
            color.a *= alpha;

            AddTexturedQuad(
                vertexHelper,
                stamp.Position - halfSize,
                stamp.Position + new Vector2(-halfSize.x, halfSize.y),
                stamp.Position + halfSize,
                stamp.Position + new Vector2(halfSize.x, -halfSize.y),
                uv,
                color);
        }

        /// <summary>绘制固定尺寸的屏幕准星。</summary>
        /// <param name="vertexHelper">接收准星顶点的网格构建器。</param>
        private void AppendPointerIcon(VertexHelper vertexHelper)
        {
            Vector2 center = _pointerPosition;
            AppendCircle(vertexHelper, center, reticleRadius, reticleStroke, reticleColor);
            AppendStroke(vertexHelper,
                center + Vector2.up * reticleGap,
                center + Vector2.up * reticleRadius,
                reticleStroke,
                reticleColor);
            AppendStroke(vertexHelper,
                center + Vector2.down * reticleGap,
                center + Vector2.down * reticleRadius,
                reticleStroke,
                reticleColor);
            AppendStroke(vertexHelper,
                center + Vector2.left * reticleGap,
                center + Vector2.left * reticleRadius,
                reticleStroke,
                reticleColor);
            AppendStroke(vertexHelper,
                center + Vector2.right * reticleGap,
                center + Vector2.right * reticleRadius,
                reticleStroke,
                reticleColor);
        }

        /// <summary>沿圆周绘制由短线段组成的准星圆环。</summary>
        /// <param name="vertexHelper">接收圆环顶点的网格构建器。</param>
        /// <param name="center">圆环中心。</param>
        /// <param name="radius">圆环半径。</param>
        /// <param name="width">圆环线宽。</param>
        /// <param name="color">圆环颜色。</param>
        private void AppendCircle(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float width,
            Color color)
        {
            Vector2 previous = center + Vector2.right * radius;
            for (int index = 1; index <= CircleSegments; index++)
            {
                float angle = index / (float)CircleSegments * Mathf.PI * 2f;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                AppendStroke(vertexHelper, previous, next, width, color);
                previous = next;
            }
        }

        /// <summary>绘制两个端点之间的实色矩形线段。</summary>
        /// <param name="vertexHelper">接收线段顶点的网格构建器。</param>
        /// <param name="start">线段起点。</param>
        /// <param name="end">线段终点。</param>
        /// <param name="width">线段宽度。</param>
        /// <param name="color">线段颜色。</param>
        private void AppendStroke(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float width,
            Color color)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Vector2 offset = new Vector2(-direction.y, direction.x).normalized * width * 0.5f;
            Vector2 uv = trailSprite != null
                ? GetSpriteUv(trailSprite).center
                : Vector2.one * 0.5f;
            AddQuad(
                vertexHelper,
                start - offset,
                start + offset,
                end + offset,
                end - offset,
                uv,
                color);
        }

        /// <summary>根据痕迹寿命取得 Sprite 在主纹理上的 UV 矩形。</summary>
        /// <param name="sprite">要绘制的 Sprite。</param>
        /// <returns>Sprite 的纹理 UV 范围。</returns>
        private static Rect GetSpriteUv(Sprite sprite)
        {
            Vector4 outerUv = DataUtility.GetOuterUV(sprite);
            return Rect.MinMaxRect(outerUv.x, outerUv.y, outerUv.z, outerUv.w);
        }

        /// <summary>按 Sprite UV 和同色顶点生成一个有纹理的四边形。</summary>
        /// <param name="vertexHelper">接收四边形顶点的网格构建器。</param>
        /// <param name="bottomLeft">左下顶点。</param>
        /// <param name="topLeft">左上顶点。</param>
        /// <param name="topRight">右上顶点。</param>
        /// <param name="bottomRight">右下顶点。</param>
        /// <param name="uv">Sprite UV 范围。</param>
        /// <param name="color">顶点颜色。</param>
        private static void AddTexturedQuad(
            VertexHelper vertexHelper,
            Vector2 bottomLeft,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            Rect uv,
            Color color)
        {
            int firstVertex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, bottomLeft, new Vector2(uv.xMin, uv.yMin), color);
            AddVertex(vertexHelper, topLeft, new Vector2(uv.xMin, uv.yMax), color);
            AddVertex(vertexHelper, topRight, new Vector2(uv.xMax, uv.yMax), color);
            AddVertex(vertexHelper, bottomRight, new Vector2(uv.xMax, uv.yMin), color);
            AddQuadTriangles(vertexHelper, firstVertex);
        }

        /// <summary>按单一 UV 坐标生成准星线段四边形。</summary>
        /// <param name="vertexHelper">接收四边形顶点的网格构建器。</param>
        /// <param name="bottomLeft">左下顶点。</param>
        /// <param name="topLeft">左上顶点。</param>
        /// <param name="topRight">右上顶点。</param>
        /// <param name="bottomRight">右下顶点。</param>
        /// <param name="uv">纹理采样坐标。</param>
        /// <param name="color">顶点颜色。</param>
        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 bottomLeft,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            Vector2 uv,
            Color color)
        {
            int firstVertex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, bottomLeft, uv, color);
            AddVertex(vertexHelper, topLeft, uv, color);
            AddVertex(vertexHelper, topRight, uv, color);
            AddVertex(vertexHelper, bottomRight, uv, color);
            AddQuadTriangles(vertexHelper, firstVertex);
        }

        /// <summary>以矩形四顶点的索引追加两个三角形。</summary>
        /// <param name="vertexHelper">接收三角形索引的网格构建器。</param>
        /// <param name="firstVertex">矩形第一个顶点索引。</param>
        private static void AddQuadTriangles(VertexHelper vertexHelper, int firstVertex)
        {
            vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
            vertexHelper.AddTriangle(firstVertex + 2, firstVertex + 3, firstVertex);
        }

        /// <summary>创建包含局部坐标、纹理坐标和颜色的 UI 顶点。</summary>
        /// <param name="vertexHelper">接收顶点的网格构建器。</param>
        /// <param name="position">顶点局部坐标。</param>
        /// <param name="uv">顶点纹理坐标。</param>
        /// <param name="color">顶点颜色。</param>
        private static void AddVertex(
            VertexHelper vertexHelper,
            Vector2 position,
            Vector2 uv,
            Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.uv0 = uv;
            vertex.color = color;
            vertexHelper.AddVert(vertex);
        }

        /// <summary>计算图章当前剩余透明度。</summary>
        /// <param name="stamp">要计算的图章。</param>
        /// <param name="currentTime">当前非缩放时间。</param>
        /// <returns>从零到一的剩余透明度。</returns>
        private static float GetStampAlpha(TrailStamp stamp, float currentTime)
        {
            return Mathf.Clamp01(1f - (currentTime - stamp.CreatedAt) / stamp.Lifetime);
        }

        /// <summary>读取指定状态下的痕迹保留时长。</summary>
        /// <param name="type">图章生成时的按住状态。</param>
        /// <returns>图章保留秒数。</returns>
        private float GetTrailLifetime(TrailType type)
        {
            return type == TrailType.Held ? heldTrailDuration : freeTrailDuration;
        }

        /// <summary>读取指定状态下的痕迹颜色。</summary>
        /// <param name="type">图章生成时的按住状态。</param>
        /// <returns>图章颜色。</returns>
        private Color GetTrailColor(TrailType type)
        {
            return type == TrailType.Held ? heldTrailColor : freeTrailColor;
        }

#if UNITY_EDITOR
        /// <summary>仅在编辑器中校验图章参数；UGUI 的基类校验回调不参与 Player 编译。</summary>
        protected override void OnValidate()
        {
            base.OnValidate();
            freeTrailDuration = Mathf.Max(0.05f, freeTrailDuration);
            heldTrailDuration = Mathf.Max(freeTrailDuration + 0.05f, heldTrailDuration);
            stampSpacing = Mathf.Max(1f, stampSpacing);
            stampRandomOffset = Mathf.Max(0f, stampRandomOffset);
            heldStampSize = Mathf.Max(1f, heldStampSize);
            freeStampSize = Mathf.Max(1f, freeStampSize);
            reticleRadius = Mathf.Max(1f, reticleRadius);
            reticleStroke = Mathf.Max(1f, reticleStroke);
            reticleGap = Mathf.Clamp(reticleGap, 1f, reticleRadius);
        }
#endif
    }
}
