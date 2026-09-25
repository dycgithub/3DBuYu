using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证 Shadertoy 网格效果已转换为可导入的 URP 透明 Shader。
/// </summary>
public sealed class VoidGridShaderTests
{
    private const string ShaderPath = "Assets/_Project/Shader/VoidGrid.shader";

    [Test]
    public void Shader_CompilesWithoutErrors()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        Assert.That(shader, Is.Not.Null, ShaderPath);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, ShaderPath);
    }

    [Test]
    public void Source_UsesUnityScreenAndTimeInputs()
    {
        string source = File.ReadAllText(ShaderPath);

        Assert.That(source, Does.Contain("_ScreenParams"));
        Assert.That(source, Does.Contain("_Time.y"));
        Assert.That(source, Does.Not.Contain("mainImage"));
        Assert.That(source, Does.Not.Contain("iResolution"));
        Assert.That(source, Does.Not.Contain("iTime"));
    }

    [Test]
    public void Source_ConfiguresTransparentUrpRendering()
    {
        string source = File.ReadAllText(ShaderPath);

        Assert.That(source, Does.Contain("\"RenderPipeline\" = \"UniversalPipeline\""));
        Assert.That(source, Does.Contain("\"Queue\" = \"Transparent\""));
        Assert.That(source, Does.Contain("Blend SrcAlpha OneMinusSrcAlpha"));
    }
}
