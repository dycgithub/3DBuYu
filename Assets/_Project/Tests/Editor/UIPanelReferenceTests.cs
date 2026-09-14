using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using _Project.UI.BackPack;
using _Project.UI.Settings;

namespace _Project.Tests.Editor
{
    public sealed class UIPanelReferenceTests
    {
        [Test]
        public void SettingsPanel_DoesNotCreateCanvasGroupWhenReferenceIsMissing()
        {
            GameObject panelObject = new("SettingsPanel");
            try
            {
                SettingsPanel panel = panelObject.AddComponent<SettingsPanel>();
                typeof(SettingsPanel).GetField("_panelRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(panel, panelObject);
                panel.Toggle();

                Assert.That(panelObject.GetComponent<CanvasGroup>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
            }
        }

        [Test]
        public void BackPackPanel_DoesNotCreateCanvasGroupWhenReferenceIsMissing()
        {
            GameObject panelObject = new("BackPackPanel");
            try
            {
                BackPackPanel panel = panelObject.AddComponent<BackPackPanel>();
                typeof(BackPackPanel).GetField("_panelRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(panel, panelObject);
                panel.Toggle();

                Assert.That(panelObject.GetComponent<CanvasGroup>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
            }
        }
    }
}
