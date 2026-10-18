using NUnit.Framework;
using Play;
using UnityEditor;
using UnityEngine;

/// <summary>Checks prefab nets against signed axis-corner identities, not just triangle silhouettes.</summary>
public sealed class DiamondNetLayoutTests
{
    /// <summary>All eight triangles form a non-overlapping tree of seven correctly oriented real edges.</summary>
    [TestCase("DiamondNet")]
    [TestCase("DiamondNetStrip")]
    [TestCase("DiamondNetBranch")]
    public void PrefabHasRealSharedEdges(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/UI/DiamondTwist/" + name + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        var data = new SerializedObject(prefab.GetComponent<DiamondNetView>());
        var points = data.FindProperty("_faceVertices");
        int edges = 0;
        var reached = new bool[8];
        reached[0] = true;
        var adjacency = new bool[8, 8];
        float side = Vector2.Distance(points.GetArrayElementAtIndex(0).vector2Value,
            points.GetArrayElementAtIndex(1).vector2Value);
        for (int face = 0; face < 8; face++)
        {
            Vector2 x = points.GetArrayElementAtIndex(face * 3).vector2Value;
            Vector2 y = points.GetArrayElementAtIndex(face * 3 + 1).vector2Value;
            Vector2 z = points.GetArrayElementAtIndex(face * 3 + 2).vector2Value;
            int parity = ((face >> 2) + ((face >> 1) & 1) + (face & 1)) % 2;
            float area = (y.x - x.x) * (z.y - x.y) - (y.y - x.y) * (z.x - x.x);
            Assert.That(area * (parity == 0 ? 1 : -1), Is.GreaterThan(0), "Face normal must unfold without mirroring corner identities");
            for (int axis = 0; axis < 3; axis++)
            {
                Vector2 a = points.GetArrayElementAtIndex(face * 3 + axis).vector2Value;
                Vector2 b = points.GetArrayElementAtIndex(face * 3 + (axis + 1) % 3).vector2Value;
                Assert.That(Vector2.Distance(a, b), Is.EqualTo(side).Within(0.00001f));
            }
        }
        for (int a = 0; a < 8; a++)
        {
            for (int b = a + 1; b < 8; b++)
            {
                int shared = 0;
                Vector2 ca = Vector2.zero;
                Vector2 cb = Vector2.zero;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 pa = points.GetArrayElementAtIndex(a * 3 + i).vector2Value;
                    ca += pa / 3f;
                    cb += points.GetArrayElementAtIndex(b * 3 + i).vector2Value / 3f;
                    for (int j = 0; j < 3; j++)
                    {
                        Vector2 pb = points.GetArrayElementAtIndex(b * 3 + j).vector2Value;
                        if (Vector2.Distance(pa, pb) < 0.00001f)
                        {
                            Assert.That(i, Is.EqualTo(j), "Shared endpoint must keep its axis identity");
                            Assert.That((a & (4 >> i)), Is.EqualTo(b & (4 >> i)), "Shared endpoint must keep its sign");
                            shared++;
                        }
                    }
                }

                Assert.That(Vector2.Distance(ca, cb), Is.GreaterThan(0.00001f));
                if (shared == 2)
                {
                    int difference = a ^ b;
                    Assert.That(difference == 1 || difference == 2 || difference == 4, Is.True);
                    adjacency[a, b] = adjacency[b, a] = true;
                    edges++;
                }
            }
        }

        Assert.That(edges, Is.EqualTo(7));
        for (int pass = 0; pass < 8; pass++)
        {
            for (int a = 0; a < 8; a++)
            {
                for (int b = 0; b < 8; b++)
                {
                    reached[b] |= reached[a] && adjacency[a, b];
                }
            }
        }

        Assert.That(reached, Is.All.True);
        if (name == "DiamondNetStrip")
        {
            for (int a = 0; a < 8; a++)
            {
                int degree = 0;
                for (int b = 0; b < 8; b++)
                {
                    degree += adjacency[a, b] ? 1 : 0;
                }

                Assert.That(degree, Is.LessThanOrEqualTo(2));
            }
        }
    }
}

/// <summary>Exercises prefab selection using the actual puzzle fixture without rebuilding or resetting its state.</summary>
public sealed partial class DiamondTwistControllerTests
{
    /// <summary>All layouts immediately render the latest committed colors through the same injected view.</summary>
    [Test]
    public void LayoutSwitchKeepsLatestColors()
    {
        var canvasObject = new GameObject("NetTestCanvas", typeof(Canvas));
        try
        {
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            string folder = "Assets/_Project/Prefabs/UI/DiamondTwist/";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "DiamondNet.prefab");
            var instance = Object.Instantiate(source, canvasObject.transform);
            var view = instance.GetComponent<DiamondNetView>();
            view.Construct(_controller);
            _controller.RequestTurn(DiamondTwistMove.L);
            CompleteTurn();
            var state = _controller.NetState;
            int[] colors = CaptureColors();
            var data = new SerializedObject(view);
            foreach (string name in new[] { "DiamondNetStrip", "DiamondNetBranch", "DiamondNet", "DiamondNetStrip" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
                data.FindProperty("_layoutPrefab").objectReferenceValue = prefab.GetComponent<DiamondNetView>();
                data.ApplyModifiedPropertiesWithoutUndo();
                Canvas.ForceUpdateCanvases();
                Mesh mesh = view.canvasRenderer.GetMesh();
                Assert.That(mesh.vertexCount, Is.EqualTo(120));
                for (int face = 0; face < 8; face++)
                {
                    for (int slot = 0; slot < 4; slot++)
                    {
                        Color expected = data.FindProperty("_palette").GetArrayElementAtIndex(state.GetColor(face, slot)).colorValue;
                        Assert.That(mesh.colors32[face * 15 + 3 + slot * 3], Is.EqualTo((Color32)expected));
                    }
                }

                Assert.That(_controller.NetState, Is.SameAs(state));
                CollectionAssert.AreEqual(colors, CaptureColors());
            }
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }
}
