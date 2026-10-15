using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Play
{
    /// <summary>Names the eight clockwise and counterclockwise moves of the Skewb Diamond.</summary>
    public enum DiamondTwistMove
    {
        // Adjacent values share an axis; each prime move reverses its clockwise partner.
        [InspectorName("L (Clockwise)")]
        L,
        [InspectorName("L' (Counterclockwise)")]
        LPrime,
        [InspectorName("R (Clockwise)")]
        R,
        [InspectorName("R' (Counterclockwise)")]
        RPrime,
        [InspectorName("D (Clockwise)")]
        D,
        [InspectorName("D' (Counterclockwise)")]
        DPrime,
        [InspectorName("B (Clockwise)")]
        B,
        [InspectorName("B' (Counterclockwise)")]
        BPrime
    }

    /// <summary>Rotates one half of the existing Skewb Diamond pieces for each standard face turn.</summary>
    public sealed class DiamondTwistController : MonoBehaviour
    {
        private const int PieceCount = 14;
        private const int PiecesPerTurn = 7;
        private const int MovesPerAxis = 2;
        private const int MoveCount = 8;
        private const int ScrambleMoveCount = 15;
        private const float TurnDegrees = 120f;
        private const float TurnDuration = 0.18f;
        private const float ScrambleInterval = 0.2f;

        [Header("Existing puzzle pieces")]
        [SerializeField] private Transform Corner_X_Minus;
        [SerializeField] private Transform Corner_X_Plus;
        [SerializeField] private Transform Corner_Y_Minus;
        [SerializeField] private Transform Corner_Y_Plus;
        [SerializeField] private Transform Corner_Z_Minus;
        [SerializeField] private Transform Corner_Z_Plus;
        [InspectorName("FaceCenter_-1_-1_-1")]
        [SerializeField] private Transform FaceCenter_NegativeX_NegativeY_NegativeZ;
        [InspectorName("FaceCenter_-1_-1_1")]
        [SerializeField] private Transform FaceCenter_NegativeX_NegativeY_PositiveZ;
        [InspectorName("FaceCenter_-1_1_-1")]
        [SerializeField] private Transform FaceCenter_NegativeX_PositiveY_NegativeZ;
        [InspectorName("FaceCenter_-1_1_1")]
        [SerializeField] private Transform FaceCenter_NegativeX_PositiveY_PositiveZ;
        [InspectorName("FaceCenter_1_-1_-1")]
        [SerializeField] private Transform FaceCenter_PositiveX_NegativeY_NegativeZ;
        [InspectorName("FaceCenter_1_-1_1")]
        [SerializeField] private Transform FaceCenter_PositiveX_NegativeY_PositiveZ;
        [InspectorName("FaceCenter_1_1_-1")]
        [SerializeField] private Transform FaceCenter_PositiveX_PositiveY_NegativeZ;
        [InspectorName("FaceCenter_1_1_1")]
        [SerializeField] private Transform FaceCenter_PositiveX_PositiveY_PositiveZ;

        private readonly Queue<DiamondTwistMove> _queuedTurns = new();
        private Transform[] _pieces;
        private Vector3[] _meshCenters;
        private Vector3[] _axes;
        private Transform[] _activePieces;
        private Vector3[] _startPositions;
        private Quaternion[] _startRotations;
        private Vector3 _activeAxis;
        private float _activeAngle;
        private Tween _turnTween;
        private Coroutine _scramble;

        /// <summary>Starts the three-second scramble only when gameplay enables the component.</summary>
        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                _scramble = StartCoroutine(ScrambleAtStart());
            }
        }

        /// <summary>Cancels the active interpolation at its starting pose and discards queued turns.</summary>
        private void OnDisable()
        {
            if (_scramble != null)
            {
                StopCoroutine(_scramble);
                _scramble = null;
            }

            DOTween.Kill(this);
            if (_activePieces != null)
            {
                ApplyAngle(0f);
            }

            _turnTween = null;
            _queuedTurns.Clear();
            ClearActiveTurn();
        }

        /// <summary>Queues one standard face turn; clockwise is viewed from outside toward the puzzle.</summary>
        /// <param name="move">One of the eight clockwise or counterclockwise face turns.</param>
        public void RequestTurn(DiamondTwistMove move)
        {
            if (!isActiveAndEnabled || !IsValidMove(move) || !EnsureInitialized())
            {
                return;
            }

            _queuedTurns.Enqueue(move);
            StartNextTurn();
        }

        /// <summary>Queues a uniformly selected clockwise or counterclockwise face turn.</summary>
        public void RandomTurn()
        {
            RequestTurn((DiamondTwistMove)Random.Range(0, MoveCount));
        }

        /// <summary>Submits fifteen queued turns at five moves per second during startup.</summary>
        private IEnumerator ScrambleAtStart()
        {
            for (int index = 0; index < ScrambleMoveCount; index++)
            {
                RandomTurn();
                yield return new WaitForSeconds(ScrambleInterval);
            }

            _scramble = null;
        }

        /// <summary>Reads the existing mesh geometry once and caches the four tetrahedral face axes.</summary>
        /// <returns>True when all fourteen serialized piece references have usable meshes.</returns>
        private bool EnsureInitialized()
        {
            if (_pieces != null)
            {
                return true;
            }

            Transform[] pieces =
            {
                Corner_X_Minus,
                Corner_X_Plus,
                Corner_Y_Minus,
                Corner_Y_Plus,
                Corner_Z_Minus,
                Corner_Z_Plus,
                FaceCenter_NegativeX_NegativeY_NegativeZ,
                FaceCenter_NegativeX_NegativeY_PositiveZ,
                FaceCenter_NegativeX_PositiveY_NegativeZ,
                FaceCenter_NegativeX_PositiveY_PositiveZ,
                FaceCenter_PositiveX_NegativeY_NegativeZ,
                FaceCenter_PositiveX_NegativeY_PositiveZ,
                FaceCenter_PositiveX_PositiveY_NegativeZ,
                FaceCenter_PositiveX_PositiveY_PositiveZ
            };
            Vector3[] meshCenters = new Vector3[PieceCount];

            for (int index = 0; index < pieces.Length; index++)
            {
                if (pieces[index] == null)
                {
                    Debug.LogError($"[{nameof(DiamondTwistController)}] Piece reference {index} is not assigned.", this);
                    return false;
                }

                MeshFilter meshFilter = pieces[index].GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    Debug.LogError($"[{nameof(DiamondTwistController)}] Piece '{pieces[index].name}' has no mesh.", pieces[index]);
                    return false;
                }

                meshCenters[index] = meshFilter.sharedMesh.bounds.center;
            }

            // These actual mesh normals form one alternating tetrahedral set; names do not encode their signs.
            Vector3[] axes =
            {
                GetGeometryCenter(FaceCenter_NegativeX_NegativeY_NegativeZ).normalized, // L: (+,-,-)
                GetGeometryCenter(FaceCenter_PositiveX_PositiveY_NegativeZ).normalized, // R: (-,+,-)
                GetGeometryCenter(FaceCenter_PositiveX_NegativeY_PositiveZ).normalized, // D: (-,-,+)
                GetGeometryCenter(FaceCenter_NegativeX_PositiveY_PositiveZ).normalized  // B: (+,+,+)
            };

            _pieces = pieces;
            _meshCenters = meshCenters;
            _axes = axes;
            return true;
        }

        /// <summary>Starts the next queued move using the pieces currently on that axis's positive side.</summary>
        private void StartNextTurn()
        {
            while (_turnTween == null && _queuedTurns.Count > 0)
            {
                DiamondTwistMove move = _queuedTurns.Dequeue();
                int moveIndex = (int)move;
                int axisIndex = moveIndex / MovesPerAxis;
                _activeAngle = moveIndex % MovesPerAxis == 0 ? -TurnDegrees : TurnDegrees;

                if (!TryPrepareTurn(_axes[axisIndex]))
                {
                    continue;
                }

                _turnTween = DOTween.To(() => 0f, ApplyAngle, _activeAngle, TurnDuration)
                    .SetEase(Ease.InOutSine)
                    .SetId(this)
                    .OnComplete(CompleteTurn);
            }
        }

        /// <summary>Captures exactly seven current pieces whose mesh centers lie on the chosen half.</summary>
        /// <param name="axis">Normalized puzzle-local face axis.</param>
        /// <returns>True when the standard seven-piece half-turn is available.</returns>
        private bool TryPrepareTurn(Vector3 axis)
        {
            Transform[] selectedPieces = new Transform[PiecesPerTurn];
            int selectedCount = 0;

            for (int index = 0; index < _pieces.Length; index++)
            {
                Vector3 currentCenter = GetGeometryCenter(_pieces[index], _meshCenters[index]);
                if (Vector3.Dot(currentCenter, axis) <= 0f)
                {
                    continue;
                }

                if (selectedCount == PiecesPerTurn)
                {
                    Debug.LogError($"[{nameof(DiamondTwistController)}] A face turn selected more than seven pieces.", this);
                    return false;
                }

                selectedPieces[selectedCount++] = _pieces[index];
            }

            if (selectedCount != PiecesPerTurn)
            {
                Debug.LogError($"[{nameof(DiamondTwistController)}] A face turn selected {selectedCount} pieces instead of seven.", this);
                return false;
            }

            _activePieces = selectedPieces;
            _startPositions = new Vector3[PiecesPerTurn];
            _startRotations = new Quaternion[PiecesPerTurn];
            _activeAxis = axis;

            for (int index = 0; index < PiecesPerTurn; index++)
            {
                _startPositions[index] = _activePieces[index].localPosition;
                _startRotations[index] = _activePieces[index].localRotation;
            }

            return true;
        }

        /// <summary>Applies an angle from captured poses so tween frames never accumulate rotation error.</summary>
        /// <param name="angle">Current signed angle around the active puzzle-local axis.</param>
        private void ApplyAngle(float angle)
        {
            Quaternion rotation = Quaternion.AngleAxis(angle, _activeAxis);
            for (int index = 0; index < _activePieces.Length; index++)
            {
                _activePieces[index].localPosition = rotation * _startPositions[index];
                _activePieces[index].localRotation = rotation * _startRotations[index];
            }
        }

        /// <summary>Snaps the completed move to its exact endpoint before starting the next queued move.</summary>
        private void CompleteTurn()
        {
            ApplyAngle(_activeAngle);
            _turnTween = null;
            ClearActiveTurn();
            StartNextTurn();
        }

        /// <summary>Clears pose data after a turn completes or is cancelled.</summary>
        private void ClearActiveTurn()
        {
            _activePieces = null;
            _startPositions = null;
            _startRotations = null;
        }

        /// <summary>Finds a mesh-bounds center in puzzle-local space, independent of its child name.</summary>
        /// <param name="piece">Existing puzzle piece transform.</param>
        /// <param name="meshCenter">Bounds center in the mesh's local coordinates.</param>
        /// <returns>The current representative point of the piece in puzzle-local coordinates.</returns>
        private Vector3 GetGeometryCenter(Transform piece, Vector3 meshCenter)
        {
            return transform.InverseTransformPoint(piece.TransformPoint(meshCenter));
        }

        /// <summary>Reads a referenced face's representative point from that piece's own mesh.</summary>
        /// <param name="piece">Existing face-center piece used as a turn axis.</param>
        /// <returns>The face-center mesh bounds center in puzzle-local coordinates.</returns>
        private Vector3 GetGeometryCenter(Transform piece)
        {
            MeshFilter meshFilter = piece.GetComponent<MeshFilter>();
            return GetGeometryCenter(piece, meshFilter.sharedMesh.bounds.center);
        }

        /// <summary>Checks that the serialized enum value maps to one of the eight standard moves.</summary>
        /// <param name="move">Requested move.</param>
        /// <returns>True when the move has a matching cached face axis.</returns>
        private static bool IsValidMove(DiamondTwistMove move)
        {
            return (int)move >= 0 && (int)move < MoveCount;
        }
    }
}
