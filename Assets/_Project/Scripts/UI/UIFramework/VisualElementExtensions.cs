using System;
using R3;
using UnityEngine.UIElements;

namespace _Project.UI.Framework
{
    public static class VisualElementExtensions
    {
        public static IDisposable AddTo(this IDisposable disposable, VisualElement element)
        {
            if (element == null)
            {
                disposable.Dispose();
                return Disposable.Empty;
            }

            void OnDetach(DetachFromPanelEvent _)
            {
                disposable.Dispose();
                element.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            element.RegisterCallback<DetachFromPanelEvent>(OnDetach);

            return Disposable.Create(() =>
            {
                disposable.Dispose();
                element.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            });
        }
    }
}
