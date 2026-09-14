using System;
using System.Reflection;
using NUnit.Framework;
using Services;
using UnityEngine;
using _Project.UI.Common;

namespace _Project.Tests.Editor
{
    public sealed class ItemTooltipLifecycleTests
    {
        [Test]
        public void Dispose_DoesNotAccessDestroyedTooltip()
        {
            var selection = new SelectionState();
            var manager = new ItemTooltipManager(
                selection,
                null,
                null,
                ItemTooltipMode.Moving,
                true,
                false);
            GameObject tooltipObject = new("ItemHoverTooltip");
            ItemTooltip tooltip = tooltipObject.AddComponent<ItemTooltip>();

            typeof(ItemTooltipManager)
                .GetField("_hoverTooltip", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, tooltip);

            try
            {
                UnityEngine.Object.DestroyImmediate(tooltipObject);

                Assert.DoesNotThrow(manager.Dispose);
            }
            finally
            {
                if (tooltipObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(tooltipObject);
                }
            }
        }

        private sealed class SelectionState : IInventorySelectionState
        {
            public ItemView SelectedItem => null;
            public event Action<ItemView> SelectionChanged;

            public void Select(ItemView item)
            {
            }

            public void ClearSelection()
            {
            }
        }
    }
}
