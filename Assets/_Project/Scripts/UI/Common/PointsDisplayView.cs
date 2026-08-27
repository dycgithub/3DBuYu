using System;
using R3;
using Services;
using TMPro;
using UnityEngine;
using VContainer;

namespace _Project.UI.Common
{
    /// <summary>
    /// 积分实时显示(挂到任意 uGUI 文本物体上,自动读取同物体 TextMeshProUGUI):
    /// 订阅 IPointsService.Points(R3 ReadOnlyReactiveProperty)实时刷新,订阅即得当前值。
    /// 依赖通过 UISceneLifetimeScope 的 autoInjectGameObjects 注入([Inject]),不再使用服务定位器。
    /// </summary>
    public class PointsDisplayView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private string format = "{0}";

        [Inject] private IPointsService _points;
        private IDisposable _subscription;

        private void Awake()
        {
            if (label == null) label = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            if (label == null)
            {
                Debug.LogWarning("[PointsDisplayView] 未找到 TextMeshProUGUI 组件", this);
                return;
            }

            if (_points == null)
            {
                Debug.LogWarning("[PointsDisplayView] 无法解析 IPointsService(未注入,请检查 UISceneLifetimeScope 的 autoInjectGameObjects)", this);
                return;
            }

            _subscription = _points.Points.Subscribe(x => label.text = string.Format(format, x));
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }
    }
}
