using GameSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.Item
{
    /// <summary>
    /// 战斗结算面板:订阅 GameManager.OnSettled,
    /// 显示 胜利/失败 + 本局积分 + 胜利奖励,并提供"返回基地"按钮。
    /// UI 全部代码自建(不依赖预制体/字体资产修改),默认隐藏。
    /// </summary>
    public class BattleResultPanel : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _root;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _infoText;
        private bool _subscribed;

        [Inject] private IObjectResolver _resolver;
        [Inject] private SceneLoader _sceneLoader;
        private GameManager _gameManager;

        private void Start()
        {
            BuildUi();

            if (_resolver != null)
                _resolver.TryResolve(out _gameManager);

            if (_gameManager != null)
            {
                _gameManager.OnSettled += HandleSettled;
                _subscribed = true;
            }

            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;
            if (_gameManager != null)
                _gameManager.OnSettled -= HandleSettled;
        }

        private void HandleSettled(bool success, int sessionPoints)
        {
            int reward = 0;
            if (success && _gameManager != null)
                reward = _gameManager.VictoryReward;

            _titleText.text = success ? "胜 利" : "失 败";
            _titleText.color = success ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);

            _infoText.text = success
                ? $"本局积分: {sessionPoints}\n胜利奖励: +{reward} 积分"
                : $"本局积分: {sessionPoints}\n未满足本局条件,无结算奖励";

            SetVisible(true);
        }

        private void ReturnToBase()
        {
            _sceneLoader?.ReturnToMainMenu();
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.SetActive(visible);
        }

        #region UI 构建(代码自建)

        private void BuildUi()
        {
            _root = new GameObject("BattleResultRoot");
            _root.transform.SetParent(transform, false);

            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;

            _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _root.AddComponent<GraphicRaycaster>();

            // 半透明背景,拦截点击
            var bg = CreateUiObject("Background", _root.transform);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.7f);
            StretchToParent(bg);

            // 中央面板
            var panel = CreateUiObject("Panel", _root.transform);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.12f, 0.13f, 0.18f, 0.95f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 400f);

            // // 标题
            // _titleText = CreateText("Title", panel.transform, "胜 利", 56f);
            // Place(panel.GetComponent<RectTransform>(), _titleText, new Vector2(0f, 110f));
            //
            // // 信息
            // _infoText = CreateText("Info", panel.transform, "", 28f);
            // Place(panel.GetComponent<RectTransform>(), _infoText, new Vector2(0f, 0f));

            // 返回基地按钮
            var button = CreateUiObject("ReturnButton", panel.transform);
            var buttonImage = button.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.55f, 0.95f, 1f);
            button.AddComponent<Button>().onClick.AddListener(ReturnToBase);
            Place(panel.GetComponent<RectTransform>(), button, new Vector2(0f, -130f));
            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(280f, 64f);

            var buttonText = CreateText("Label", button.transform, "返回基地", 26f);
            var btRect = buttonText.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            buttonText.alignment = TextAlignmentOptions.Center;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string content, float fontSize)
        {
            var go = CreateUiObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var rect = tmp.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500f, 60f);
            return tmp;
        }

        private static void Place(RectTransform parent, GameObject child, Vector2 offset)
        {
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            _ = parent;
        }

        private static void StretchToParent(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
