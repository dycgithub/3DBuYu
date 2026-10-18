using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using Play;
using UnityEngine;

/// <summary>Checks completed-state timing and validates the fixed slots against actual imported mesh geometry.</summary>
public sealed partial class DiamondTwistControllerTests
{
    /// <summary>Partial animation and cancellation leave the initial complete state and emit no completion.</summary>
    [Test]
    public void ColorsWaitForCompletion()
    {
        int completed = 0;
        _controller.TurnCompleted += () => completed++;
        int[] initial = CaptureColors();
        _controller.RequestTurn(DiamondTwistMove.L);
        DOTween.Goto(_controller, HalfTurnTweenTime, false);
        CollectionAssert.AreEqual(initial, CaptureColors());
        InvokeDisableHandler(_controller);
        CollectionAssert.AreEqual(initial, CaptureColors());
        Assert.That(completed, Is.Zero);

        _controller.RequestTurn(DiamondTwistMove.L);
        CompleteTurn();
        CollectionAssert.AreNotEqual(initial, CaptureColors());
        Assert.That(completed, Is.EqualTo(1));
    }

    /// <summary>Invalid enum requests neither change colors nor complete a turn.</summary>
    [Test]
    public void InvalidMoveLeavesColors()
    {
        int[] initial = CaptureColors();
        _controller.RequestTurn((DiamondTwistMove)99);
        CollectionAssert.AreEqual(initial, CaptureColors());
        Assert.That(DOTween.IsTweening(_controller), Is.False);
    }

    /// <summary>Every dequeued turn commits once, while cancellation discards the remaining queue.</summary>
    [Test]
    public void QueueCommitsEachCompletedMove()
    {
        int completed = 0;
        _controller.TurnCompleted += () => completed++;
        int[] initial = CaptureColors();
        _controller.RequestTurn(DiamondTwistMove.L);
        _controller.RequestTurn(DiamondTwistMove.LPrime);
        CompleteQueuedTurns();
        CollectionAssert.AreEqual(initial, CaptureColors());
        Assert.That(completed, Is.EqualTo(2));

        _controller.RequestTurn(DiamondTwistMove.R);
        _controller.RequestTurn(DiamondTwistMove.D);
        DOTween.Goto(_controller, HalfTurnTweenTime, false);
        InvokeDisableHandler(_controller);
        CollectionAssert.AreEqual(initial, CaptureColors());
        Assert.That(DOTween.IsTweening(_controller), Is.False);
        Assert.That(completed, Is.EqualTo(2));
    }

    /// <summary>Each physical sticker lands in the same face and corner slot as the integer permutation.</summary>
    [Test]
    public void ColorsMatchImportedGeometry()
    {
        var stickers = new List<(int piece, Vector3 normal, int identity)>();
        for (int piece = 0; piece < _pieces.Length; piece++)
        {
            Mesh mesh = _pieces[piece].GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.magnitude < 0.000001f || Vector3.Dot((a + b + c) / 3f, cross.normalized) < 0.01f)
                {
                    continue;
                }

                Vector3 normal = cross.normalized;
                stickers.Add((piece, normal, FaceIndex(normal)));
            }
        }

        Assert.That(stickers.Count, Is.EqualTo(32));
        DiamondTwistMove[] moves = { DiamondTwistMove.L, DiamondTwistMove.R, DiamondTwistMove.DPrime, DiamondTwistMove.B, DiamondTwistMove.LPrime, DiamondTwistMove.RPrime, DiamondTwistMove.D, DiamondTwistMove.BPrime };
        for (int step = 0; step < 32; step++)
        {
            _controller.RequestTurn(moves[step % moves.Length]);
            CompleteTurn();
            foreach (var sticker in stickers)
            {
                Transform piece = _pieces[sticker.piece];
                Vector3 normal = _puzzleObject.transform.InverseTransformDirection(piece.TransformDirection(sticker.normal));
                Vector3 center = GetGeometryCenter(piece);
                int slot = sticker.piece >= 6 ? 0 : Mathf.Abs(center.x) > Mathf.Abs(center.y) && Mathf.Abs(center.x) > Mathf.Abs(center.z) ? 1 : Mathf.Abs(center.y) > Mathf.Abs(center.z) ? 2 : 3;
                Assert.That(_controller.NetState.GetColor(FaceIndex(normal), slot), Is.EqualTo(sticker.identity), $"Step {step}, {piece.name}");
            }
        }
    }

    /// <summary>Captures the public complete state without retaining its mutable storage.</summary>
    private int[] CaptureColors()
    {
        int[] colors = new int[32];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = _controller.NetState.GetColor(i / 4, i % 4);
        }

        return colors;
    }

    /// <summary>Identifies a physical face from its outward normal rather than object names or colors.</summary>
    private static int FaceIndex(Vector3 normal) => (normal.x > 0 ? 4 : 0) | (normal.y > 0 ? 2 : 0) | (normal.z > 0 ? 1 : 0);
}
