using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 验证背景 Shader 的导入状态、五边形交互输入和六边形绘制移除结果。
/// </summary>
public sealed class HexagonalBackgroundShaderTests
{
    private const string ShaderPath = "Assets/_Project/Shader/HexagonalSampling.shader";

    [Test]
    public void Shader_CompilesWithoutErrors()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        Assert.That(shader, Is.Not.Null, ShaderPath);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, ShaderPath);
    }

    [Test]
    public void Source_UsesMeshRenderingState()
    {
        string source = File.ReadAllText(ShaderPath);

        Assert.That(source, Does.Contain("ZTest LEqual"));
        Assert.That(source, Does.Contain("float2 logical = input.uv"));
        Assert.That(source, Does.Not.Contain("ZTest [unity_GUIZTestMode]"));
        Assert.That(source, Does.Not.Contain("Stencil"));
        Assert.That(source, Does.Not.Contain("UNITY_UI_"));
    }

    [Test]
    public void Source_RendersOnlyMeshInterior()
    {
        string source = File.ReadAllText(ShaderPath);

        Assert.That(source, Does.Contain("Cull Front"));
        Assert.That(source, Does.Not.Contain("Cull Back"));
        Assert.That(source, Does.Contain("output.uv = input.uv"));
        Assert.That(source, Does.Contain("logical = frac(input.uv)"));
        Assert.That(source, Does.Not.Contain("_ScreenParams"));
    }

    [Test]
    public void Source_RemovesHexagonalOverlay()
    {
        string source = File.ReadAllText(ShaderPath);
        string[] removedSymbols =
        {
            "_HexColor",
            "_HexActiveColor",
            "_HexScale",
            "HexagonalLayer",
            "TriangleFanLines"
        };

        foreach (string removedSymbol in removedSymbols)
        {
            Assert.That(source, Does.Not.Contain(removedSymbol), removedSymbol);
        }
    }

    [Test]
    public void Source_ContainsPentagonImpactInput()
    {
        string source = File.ReadAllText(ShaderPath);

        Assert.That(source, Does.Contain("_PentagonImpact"));
        Assert.That(source, Does.Contain("_PentagonCutRadius"));
        Assert.That(source, Does.Contain("_PentagonRepairDuration"));
        Assert.That(source, Does.Contain("[loop]"));
    }
}
