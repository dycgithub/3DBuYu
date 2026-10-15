using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using Play;
using UnityEditor;
using UnityEngine;

/// <summary>验证标准菱形魔方转动行为。</summary>
public sealed class DiamondTwistControllerTests
{
    private const string ModelPath = "Assets/_Project/Art/Model/SkewbDiamond.fbx";
    private const float PoseTolerance = 0.1f;
    private const float HalfTurnTweenTime = 0.09f;

    private static readonly string[] PieceFieldNames =
    {
        "Corner_X_Minus",
        "Corner_X_Plus",
        "Corner_Y_Minus",
        "Corner_Y_Plus",
        "Corner_Z_Minus",
        "Corner_Z_Plus",
        "FaceCenter_NegativeX_NegativeY_NegativeZ",
        "FaceCenter_NegativeX_NegativeY_PositiveZ",
        "FaceCenter_NegativeX_PositiveY_NegativeZ",
        "FaceCenter_NegativeX_PositiveY_PositiveZ",
        "FaceCenter_PositiveX_NegativeY_NegativeZ",
        "FaceCenter_PositiveX_NegativeY_PositiveZ",
        "FaceCenter_PositiveX_PositiveY_NegativeZ",
        "FaceCenter_PositiveX_PositiveY_PositiveZ"
    };

    private GameObject _puzzleObject;
    private DiamondTwistController _controller;
    private Transform[] _pieces;

    /// <summary>Creates an isolated controller fixture from the project's imported puzzle meshes.</summary>
    [SetUp]
    public void SetUp()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Assert.That(model, Is.Not.Null, "The imported Skewb Diamond model must be available.");

        _puzzleObject = Object.Instantiate(model);
        _controller = _puzzleObject.AddComponent<DiamondTwistController>();
        _pieces = new Transform[PieceFieldNames.Length];

        SerializedObject serializedController = new(_controller);
        for (int index = 0; index < PieceFieldNames.Length; index++)
        {
            string pieceName = GetPieceName(index);
            _pieces[index] = _puzzleObject.transform.Find(pieceName);
            Assert.That(_pieces[index], Is.Not.Null, pieceName);
            serializedController.FindProperty(PieceFieldNames[index]).objectReferenceValue = _pieces[index];
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Stops test tweens and destroys only the temporary imported-model instance.</summary>
    [TearDown]
    public void TearDown()
    {
        if (_controller != null)
        {
            DOTween.Kill(_controller);
        }

        if (_puzzleObject != null)
        {
            Object.DestroyImmediate(_puzzleObject);
        }
    }

    /// <summary>Each of the four actual face normals belongs to one alternating tetrahedral set.</summary>
    [Test]
    public void AxesFormTetrahedron()
    {
        int[] axisPieceIndices = { 6, 12, 11, 9 };
        Vector3[] axes = new Vector3[axisPieceIndices.Length];
        for (int index = 0; index < axisPieceIndices.Length; index++)
        {
            axes[index] = GetGeometryCenter(_pieces[axisPieceIndices[index]]).normalized;
        }

        for (int first = 0; first < axes.Length; first++)
        {
            for (int second = first + 1; second < axes.Length; second++)
            {
                Assert.That(Vector3.Dot(axes[first], axes[second]),
                    Is.EqualTo(-1f / 3f).Within(0.001f));
            }
        }
    }

    /// <summary>Each clockwise and inverse move affects the matching seven-piece half of the imported model.</summary>
    [TestCase(DiamondTwistMove.L, 6)]
    [TestCase(DiamondTwistMove.LPrime, 6)]
    [TestCase(DiamondTwistMove.R, 12)]
    [TestCase(DiamondTwistMove.RPrime, 12)]
    [TestCase(DiamondTwistMove.D, 11)]
    [TestCase(DiamondTwistMove.DPrime, 11)]
    [TestCase(DiamondTwistMove.B, 9)]
    [TestCase(DiamondTwistMove.BPrime, 9)]
    public void MoveSelectsSevenCurrentPieces(DiamondTwistMove move, int axisPieceIndex)
    {
        Vector3 axis = GetGeometryCenter(_pieces[axisPieceIndex]).normalized;
        HashSet<string> expectedPieces = GetPiecesOnPositiveSide(axis);
        Quaternion[] startRotations = CaptureRotations();
        _controller.RequestTurn(move);
        CompleteTurn();

        HashSet<string> movedPieces = GetChangedPieces(startRotations);
        CollectionAssert.AreEquivalent(expectedPieces, movedPieces);

        int movedCenters = 0;
        foreach (string pieceName in movedPieces)
        {
            if (pieceName.StartsWith("FaceCenter_"))
            {
                movedCenters++;
            }
        }

        Assert.That(movedPieces, Has.Count.EqualTo(7));
        Assert.That(movedCenters, Is.EqualTo(4));

        for (int index = 0; index < _pieces.Length; index++)
        {
            if (!movedPieces.Contains(_pieces[index].name))
            {
                continue;
            }

            Assert.That(Quaternion.Angle(startRotations[index], _pieces[index].localRotation),
                Is.EqualTo(120f).Within(PoseTolerance), _pieces[index].name);
        }
    }

    /// <summary>An inverse move and three identical moves both return all pieces to their starting poses.</summary>
    [Test]
    public void InverseAndTripleRestorePoses()
    {
        Quaternion[] startRotations = CaptureRotations();
        Vector3[] startPositions = CapturePositions();

        _controller.RequestTurn(DiamondTwistMove.L);
        CompleteTurn();
        _controller.RequestTurn(DiamondTwistMove.LPrime);
        CompleteTurn();
        AssertStartingPoses(startRotations, startPositions);

        for (int move = 0; move < 3; move++)
        {
            _controller.RequestTurn(DiamondTwistMove.R);
            CompleteTurn();
        }

        AssertStartingPoses(startRotations, startPositions);
    }

    /// <summary>A turn queued during animation is applied after the active move rather than dropped.</summary>
    [Test]
    public void QueuedInverseRestoresPose()
    {
        Quaternion[] startRotations = CaptureRotations();
        Vector3[] startPositions = CapturePositions();

        _controller.RequestTurn(DiamondTwistMove.L);
        _controller.RequestTurn(DiamondTwistMove.LPrime);
        CompleteQueuedTurns();

        AssertStartingPoses(startRotations, startPositions);
    }

    /// <summary>A second axis selects pieces from their current mesh positions after the first turn.</summary>
    [Test]
    public void SecondAxisUsesCurrentCenters()
    {
        Vector3 rightAxis = GetGeometryCenter(_pieces[12]).normalized;
        _controller.RequestTurn(DiamondTwistMove.L);
        CompleteTurn();

        HashSet<string> expectedPieces = GetPiecesOnPositiveSide(rightAxis);
        Quaternion[] beforeSecondTurn = CaptureRotations();
        _controller.RequestTurn(DiamondTwistMove.R);
        CompleteTurn();

        HashSet<string> movedPieces = GetChangedPieces(beforeSecondTurn);
        CollectionAssert.AreEquivalent(expectedPieces, movedPieces);
        Assert.That(movedPieces, Has.Count.EqualTo(7));
    }

    /// <summary>The disable handler restores the captured starting pose after a partial tween.</summary>
    [Test]
    public void DisableHandlerRestoresPartialTurn()
    {
        Quaternion[] startRotations = CaptureRotations();
        Vector3[] startPositions = CapturePositions();
        _controller.RequestTurn(DiamondTwistMove.L);
        Assert.That(DOTween.Goto(_controller, HalfTurnTweenTime, false), Is.GreaterThan(0));

        HashSet<string> partiallyMovedPieces = GetChangedPieces(startRotations);
        Assert.That(partiallyMovedPieces, Has.Count.EqualTo(7));
        for (int index = 0; index < _pieces.Length; index++)
        {
            if (partiallyMovedPieces.Contains(_pieces[index].name))
            {
                float angle = Quaternion.Angle(startRotations[index], _pieces[index].localRotation);
                Assert.That(angle, Is.GreaterThan(PoseTolerance).And.LessThan(120f - PoseTolerance));
            }
        }

        InvokeDisableHandler(_controller);

        AssertStartingPoses(startRotations, startPositions);
    }

    /// <summary>Default port indexes match the ordered central-core configurations and requested inverse mapping.</summary>
    [Test]
    public void DefaultPortMappingIsOrdered()
    {
        TransmitterFireController fireController = _puzzleObject.AddComponent<TransmitterFireController>();
        SerializedObject serializedController = new(fireController);
        SerializedProperty moves = serializedController.FindProperty("_puzzleMoves");
        DiamondTwistMove[] expectedMoves =
        {
            DiamondTwistMove.LPrime,
            DiamondTwistMove.L,
            DiamondTwistMove.RPrime,
            DiamondTwistMove.R,
            DiamondTwistMove.DPrime,
            DiamondTwistMove.D,
            DiamondTwistMove.BPrime,
            DiamondTwistMove.B
        };

        Assert.That(moves.arraySize, Is.EqualTo(8));
        for (int index = 0; index < expectedMoves.Length; index++)
        {
            Assert.That(moves.GetArrayElementAtIndex(index).enumValueIndex,
                Is.EqualTo((int)expectedMoves[index]), $"Port index {index}");
        }
    }

    private void CompleteTurn()
    {
        Assert.That(DOTween.Complete(_controller, true), Is.GreaterThan(0));
    }

    /// <summary>Invokes the private runtime handler because EditMode does not dispatch its lifecycle callbacks.</summary>
    private static void InvokeDisableHandler(DiamondTwistController controller)
    {
        MethodInfo onDisable = typeof(DiamondTwistController).GetMethod(
            "OnDisable",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(onDisable, Is.Not.Null);
        onDisable.Invoke(controller, null);
    }

    private void CompleteQueuedTurns()
    {
        Assert.That(DOTween.Complete(_controller, true), Is.GreaterThan(0));
        DOTween.Complete(_controller, true);
    }

    private Quaternion[] CaptureRotations()
    {
        Quaternion[] rotations = new Quaternion[_pieces.Length];
        for (int index = 0; index < _pieces.Length; index++)
        {
            rotations[index] = _pieces[index].localRotation;
        }

        return rotations;
    }

    private Vector3[] CapturePositions()
    {
        Vector3[] positions = new Vector3[_pieces.Length];
        for (int index = 0; index < _pieces.Length; index++)
        {
            positions[index] = _pieces[index].localPosition;
        }

        return positions;
    }

    private void AssertStartingPoses(Quaternion[] rotations, Vector3[] positions)
    {
        for (int index = 0; index < _pieces.Length; index++)
        {
            Assert.That(Quaternion.Angle(rotations[index], _pieces[index].localRotation),
                Is.LessThan(PoseTolerance), _pieces[index].name);
            Assert.That(Vector3.Distance(positions[index], _pieces[index].localPosition),
                Is.LessThan(0.0001f), _pieces[index].name);
        }
    }

    private HashSet<string> GetPiecesOnPositiveSide(Vector3 axis)
    {
        HashSet<string> selected = new();
        for (int index = 0; index < _pieces.Length; index++)
        {
            Vector3 center = GetGeometryCenter(_pieces[index]);
            if (Vector3.Dot(center, axis) > 0f)
            {
                selected.Add(_pieces[index].name);
            }
        }

        return selected;
    }

    private HashSet<string> GetChangedPieces(Quaternion[] rotations)
    {
        HashSet<string> changed = new();
        for (int index = 0; index < _pieces.Length; index++)
        {
            if (Quaternion.Angle(rotations[index], _pieces[index].localRotation) >= PoseTolerance)
            {
                changed.Add(_pieces[index].name);
            }
        }

        return changed;
    }

    private Vector3 GetGeometryCenter(Transform piece)
    {
        MeshFilter meshFilter = piece.GetComponent<MeshFilter>();
        return _puzzleObject.transform.InverseTransformPoint(
            piece.TransformPoint(meshFilter.sharedMesh.bounds.center));
    }

    private static string GetPieceName(int index)
    {
        return index switch
        {
            0 => "Corner_X_Minus",
            1 => "Corner_X_Plus",
            2 => "Corner_Y_Minus",
            3 => "Corner_Y_Plus",
            4 => "Corner_Z_Minus",
            5 => "Corner_Z_Plus",
            6 => "FaceCenter_-1_-1_-1",
            7 => "FaceCenter_-1_-1_1",
            8 => "FaceCenter_-1_1_-1",
            9 => "FaceCenter_-1_1_1",
            10 => "FaceCenter_1_-1_-1",
            11 => "FaceCenter_1_-1_1",
            12 => "FaceCenter_1_1_-1",
            _ => "FaceCenter_1_1_1"
        };
    }
}
