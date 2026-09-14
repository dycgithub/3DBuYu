using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.Card
{
    /// <summary>统一控制 UGUI 按钮的交互状态和 Unity 内置禁用表现。</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonStateView : MonoBehaviour
    {
        [SerializeField] private Button button;

        private void Awake()
        {
            ResolveButton();
        }

        /// <summary>设置按钮是否可交互；Button 自身负责阻止禁用状态下的点击。</summary>
        /// <param name="interactable">是否允许指针和导航交互。</param>
        public void SetInteractable(bool interactable)
        {
            ResolveButton();
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
        }

        private void ResolveButton()
        {
            button ??= GetComponent<Button>();
        }
    }
}
