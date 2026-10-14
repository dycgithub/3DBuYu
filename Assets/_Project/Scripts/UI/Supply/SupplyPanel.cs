using _Project.UI.Animations;
using System;
using R3;
using Supply;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>执行五件抽取并显示当前语言结果；语言刷新只重绘，不重复抽取。</summary>
public class SupplyPanel : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _returnButton;
    [SerializeField] private UIPanelShowHide _panelTween;
    [SerializeField] private Button _drawButton;
    [SerializeField] private GridView _storageGrid;
    [SerializeField] private TMP_Text _resultText;
    [SerializeField] private Image[] _resultIcons;
    [SerializeField] private TMP_Text[] _resultLabels;
    [SerializeField, Min(0)] private int _drawCost = 10000;

    [Inject] private SupplyFiveDrawService _drawService;
    [Inject] private Services.IPointsService _points;
    
    // CanvasGroup 必须由场景预置，运行时只读取引用，不补挂组件。
    private CanvasGroup _panelGroup;
    private SupplyBackpackStorage _storage;
    private IDisposable _pointsSubscription;
    private SupplyFiveDrawResult _lastResult;
    private string _messageKey = "supply.warning";
    private object[] _messageArguments = Array.Empty<object>();

    /// <summary>绑定抽取和返回按钮，并随积分变化更新抽取可用状态。</summary>
    private void Start()
    {
        if (_returnButton != null)
        {
            _returnButton.onClick.AddListener(Hide);
        }

        _storage = new SupplyBackpackStorage(_storageGrid);
        if (_drawButton != null)
        {
            _drawButton.onClick.AddListener(Draw);
            TMP_Text label = _drawButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = L10n.Get("supply.draw", SupplyFiveDrawService.DrawCount, _drawCost);
            }
        }

        if (_resultText != null)
        {
            _resultText.text = L10n.Get("supply.warning");
        }

        ClearResults();

        _pointsSubscription = _points?.Points.Subscribe(_ => UpdateDrawButton());
        UpdateDrawButton();
        LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;
    }

    /// <summary>解除按钮和积分变化监听。</summary>
    private void OnDestroy()
    {
        _returnButton?.onClick.RemoveListener(Hide);
        _drawButton?.onClick.RemoveListener(Draw);
        _pointsSubscription?.Dispose();
        LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;
    }

    /// <summary>抽取五件道具，并显示每件道具的入包或消失状态。</summary>
    private void Draw()
    {
        if (_drawService == null)
        {
            ShowResult("supply.notReady");
            return;
        }

        _drawButton.interactable = false;
        SupplyFiveDrawResult result = _drawService.TryDraw(_drawCost, _storage);
        if (result.Status != SupplyFiveDrawStatus.Completed)
        {
            ShowResult("supply.failed");
            UpdateDrawButton();
            return;
        }

        RenderResults(result);
        _lastResult = result;
        ShowResult("supply.result", result.StoredCount, result.DroppedCount);
        UpdateDrawButton();
    }

    /// <summary>将五件奖励的图标、名称和入包状态写入预置结果栏。</summary>
    /// <param name="result">已完成的抽取结果。</param>
    private void RenderResults(SupplyFiveDrawResult result)
    {
        for (int index = 0; index < result.Items.Count; index++)
        {
            SupplyFiveDrawItem item = result.Items[index];
            if (_resultIcons != null && index < _resultIcons.Length && _resultIcons[index] != null)
            {
                _resultIcons[index].sprite = item.Definition.Icon;
                _resultIcons[index].enabled = item.Definition.Icon != null;
            }

            if (_resultLabels != null && index < _resultLabels.Length && _resultLabels[index] != null)
            {
                string status = L10n.Get(item.Stored ? "supply.stored" : "supply.dropped");
                _resultLabels[index].text = $"{item.Definition.DisplayName} · {status}";
            }
        }
    }

    /// <summary>隐藏尚未产生的抽取结果图标与文字。</summary>
    private void ClearResults()
    {
        if (_resultIcons != null)
        {
            foreach (Image icon in _resultIcons)
            {
                if (icon != null)
                {
                    icon.enabled = false;
                }
            }
        }

        if (_resultLabels != null)
        {
            foreach (TMP_Text label in _resultLabels)
            {
                if (label != null)
                {
                    label.text = string.Empty;
                }
            }
        }
    }

    /// <summary>仅当费用、奖池和背包就绪时允许点击抽取。</summary>
    private void UpdateDrawButton()
    {
        if (_drawButton != null)
        {
            _drawButton.interactable = _drawService != null &&
                                       _drawService.CanDraw(_drawCost, _storage);
        }
    }

    /// <summary>将抽取结果写入场景中预置的 TMP 文本。</summary>
    /// <param name="key">结果说明的翻译键。</param>
    /// <param name="arguments">本次抽取的计数参数。</param>
    private void ShowResult(string key, params object[] arguments)
    {
        _messageKey = key;
        _messageArguments = arguments;
        if (_resultText != null)
        {
            _resultText.text = L10n.Get(key, arguments);
        }
    }

    /// <summary>重绘已有结果，不清空奖品、不扣除积分。</summary>
    private void RefreshLanguage(Locale _)
    {
        TMP_Text label = _drawButton != null ? _drawButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null)
        {
            label.text = L10n.Get("supply.draw", SupplyFiveDrawService.DrawCount, _drawCost);
        }

        ShowResult(_messageKey, _messageArguments);
        if (_lastResult != null)
        {
            RenderResults(_lastResult);
        }
    }

    /// <summary>关闭补给面板并恢复下层界面操作。</summary>
    private void Hide()
    {
        if (_panelTween != null)
        {
            _panelTween.Hide();
            return;
        }
        var group = GetPanelGroup();
        if (group == null) return;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    /// <summary>读取场景预置的面板 CanvasGroup。</summary>
    /// <returns>面板未配置时返回 null。</returns>
    private CanvasGroup GetPanelGroup()
    {
        if (_panelGroup == null && _panelRoot != null)
            _panelGroup = _panelRoot.GetComponent<CanvasGroup>();
        return _panelGroup;
    }
}
