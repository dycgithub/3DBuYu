using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证六边形采样 Shader 已被 Unity 导入，并暴露运行时所需的输入。
/// </summary>
public sealed class HexagonalSamplingShaderTests
{
    private const string ShaderPath = "Assets/_Project/Shader/HexagonalSampling.shader";

    [Test]
    public void Shader_IsImportedWithRequiredRuntimeInputs()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        Assert.That(shader, Is.Not.Null, ShaderPath);

        Material material = new Material(shader);
        try
        {
            string[] requiredProperties =
            {
                "_MainTex",
                "_Color",
                "_MousePosition",
                "_TilingMinimum",
                "_TilingMaximum",
                "_HexDistanceScale",
                "_ColorTimeFrequency",
                "_ColorTimeAmplitude",
                "_NoiseFrequency",
                "_NoiseAnimation",
                "_ScanSpeed",
                "_ScanFadeEnd",
                "_ScanNoiseOffset",
                "_ScanPatternFrequency",
                "_ScanPatternAmplitude",
                "_GlowWidth",
                "_GlowPower",
                "_TextureBlend",
                "_HighlightColor"
            };

            foreach (string propertyName in requiredProperties)
                Assert.That(material.HasProperty(propertyName), Is.True, propertyName);
        }
        finally
        {
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void Source_UsesUnityHlslEntryPointsInsteadOfShadertoyEntryPoint()
    {
        string source = File.ReadAllText(ShaderPath);

        Assert.That(source, Does.Contain("HLSLPROGRAM"));
        Assert.That(source, Does.Contain("#pragma vertex Vert"));
        Assert.That(source, Does.Contain("#pragma fragment Frag"));
        Assert.That(source, Does.Not.Contain("mainImage"));
        Assert.That(source, Does.Not.Contain("vec2"));
    }
}
