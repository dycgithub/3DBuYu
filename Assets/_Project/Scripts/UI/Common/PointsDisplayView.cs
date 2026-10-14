using System;
using R3;
using Services;
using TMPro;
using UnityEngine;
using VContainer;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace _Project.UI.Common
{
    /// <summary>
    /// 积分实时显示(挂到任意 uGUI 文本物体上,自动读取同物体 TextMeshProUGUI):
    /// 订阅积分和语言变化；数字格式保留，带标签的配置使用翻译表。
    /// 依赖由所属场景 LifetimeScope 注入([Inject]),不再使用服务定位器。
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

            _subscription = _points.Points.Subscribe(RefreshPoints);
            LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;
        }

        /// <summary>格式化当前积分，纯数字配置保留原有布局。</summary>
        private void RefreshPoints(int value)
        {
            label.text = format == "{0}" ? string.Format(format, value) : L10n.Get("hud.points", value);
        }

        /// <summary>语言变化时立即刷新当前数值。</summary>
        private void RefreshLanguage(Locale _)
        {
            if (_points != null && label != null)
            {
                RefreshPoints(_points.Points.CurrentValue);
            }
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;
            _subscription?.Dispose();
        }
    }
}
