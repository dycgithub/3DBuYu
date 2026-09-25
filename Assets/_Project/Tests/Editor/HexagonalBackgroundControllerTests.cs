using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using _Project.UI.HexagonalBackground.Implement;

/// <summary>
/// 验证鼠标缓慢移动时仍能累计触发五边形断边影响。
/// </summary>
public sealed class HexagonalBackgroundControllerTests
{
    private static readonly MethodInfo UpdateImpactMethod = typeof(HexagonalBackgroundController)
        .GetMethod("UpdateImpact", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo ImpactAgeField = typeof(HexagonalBackgroundController)
        .GetField("_impactAge", BindingFlags.Instance | BindingFlags.NonPublic);

    [Test]
    public void SlowMovement_TriggersImpactAfterAccumulatingDistance()
    {
        GameObject target = new GameObject("HexagonalBackgroundControllerTest");
        try
        {
            HexagonalBackgroundController controller = target.AddComponent<HexagonalBackgroundController>();
            InvokeImpact(controller, new Vector2(0.5f, 0.5f));
            InvokeImpact(controller, new Vector2(0.5008f, 0.5f));
            InvokeImpact(controller, new Vector2(0.5016f, 0.5f));

            float impactAge = (float)ImpactAgeField.GetValue(controller);
            Assert.That(impactAge, Is.EqualTo(0f));
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }

    private static void InvokeImpact(
        HexagonalBackgroundController controller,
        Vector2 mouseUv)
    {
        UpdateImpactMethod.Invoke(controller, new object[] { mouseUv });
    }
}
