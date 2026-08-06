using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace InventorySystem.GridSnap
{
    /// <summary>
    /// 吸附会话(Unity 适配层):唯一触碰 RectTransform 的地方。
    /// 屏幕→本地转换后,算法全部委托 GridSnapMath;结果以 R3 流输出。
    /// 容器约定:pivot=(0,1)(左上角),格子 (row,col) 左上角位于本地 (col*Step, row*Step) 视觉坐标。
    /// </summary>
    public sealed class GridSnapSession : IDisposable
    {
        private readonly RectTransform _container;
        private readonly Camera _uiCamera;
        private readonly Subject<GridSnapState> _snapChanged = new();

        /// <summary>吸附结果流:订阅后驱动幽灵定位/着色。</summary>
        public Observable<GridSnapState> SnapChanged => _snapChanged;

        private GridSnapConfig _config;
        private IGridSnapPlacement _placement;
        private IReadOnlyList<SnapCell> _cells = Array.Empty<SnapCell>();
        private SnapCell _pointerOffset;
        private bool _active;

        public GridSnapSession(RectTransform container, Camera uiCamera = null)
        {
            _container = container;
            _uiCamera = uiCamera;
        }

        /// <summary>开始会话:注入配置、放置策略、旋转后形状与按下偏移。</summary>
        public void Begin(GridSnapConfig config, IGridSnapPlacement placement,
            IReadOnlyList<SnapCell> cells, SnapCell pointerOffset)
        {
            _config = config;
            _placement = placement;
            _cells = cells;
            _pointerOffset = pointerOffset;
            _active = true;
        }

        /// <summary>形状变化(如 R 键旋转):旋转后按下偏移需由调用方重新提供。</summary>
        public void SetCells(IReadOnlyList<SnapCell> cells, SnapCell pointerOffset)
        {
            _cells = cells;
            _pointerOffset = pointerOffset;
        }

        /// <summary>拖拽期间每帧调用:计算 hover → anchor → 合法性,推送状态。</summary>
        public void UpdateSnap(Vector2 screenPosition)
        {
            if (!_active) return;
            if (!ScreenToLocal(screenPosition, out var local)) return;

            var hover = GridSnapMath.LocalToCell(local, _config);
            var anchor = GridSnapMath.ComputeAnchor(hover, _pointerOffset);
            bool valid = GridSnapMath.CanPlace(_cells, anchor, _placement);

            _snapChanged.OnNext(new GridSnapState(
                hover, anchor, valid, GridSnapMath.CellToLocal(anchor, _config)));
        }

        public void End()
        {
            _active = false;
            _placement = null;
            _cells = Array.Empty<SnapCell>();
        }

        private bool ScreenToLocal(Vector2 screenPosition, out SnapPoint local)
        {
            if (_container == null)
            {
                local = default;
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _container, screenPosition, _uiCamera, out var v))
            {
                local = default;
                return false;
            }

            // RectTransformUtility 的 local y 向上为正,转换为视觉坐标(Y 向下为正)
            local = new SnapPoint(v.x, -v.y);
            return true;
        }

        public void Dispose() => _snapChanged.Dispose();
    }
}
