using UnityEditor;
using UnityEngine;

namespace Play.Editor
{
    /// <summary>Provides play-mode buttons for the eight standard Skewb Diamond moves.</summary>
    [CustomEditor(typeof(DiamondTwistController))]
    public sealed class DiamondTwistControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DiamondTwistController puzzle = (DiamondTwistController)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Standard Turns", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Clockwise is viewed from outside toward the puzzle. Prime moves are counterclockwise.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                DrawMoveButton(puzzle, "L Clockwise", DiamondTwistMove.L);
                DrawMoveButton(puzzle, "L' Counterclockwise", DiamondTwistMove.LPrime);
                DrawMoveButton(puzzle, "R Clockwise", DiamondTwistMove.R);
                DrawMoveButton(puzzle, "R' Counterclockwise", DiamondTwistMove.RPrime);
                DrawMoveButton(puzzle, "D Clockwise", DiamondTwistMove.D);
                DrawMoveButton(puzzle, "D' Counterclockwise", DiamondTwistMove.DPrime);
                DrawMoveButton(puzzle, "B Clockwise", DiamondTwistMove.B);
                DrawMoveButton(puzzle, "B' Counterclockwise", DiamondTwistMove.BPrime);

                if (GUILayout.Button("Random Turn"))
                {
                    puzzle.RandomTurn();
                }
            }
        }

        /// <summary>Queues one standard move when its play-mode button is pressed.</summary>
        /// <param name="puzzle">Puzzle controller receiving the request.</param>
        /// <param name="label">Visible move label.</param>
        /// <param name="move">Move sent to the controller.</param>
        private static void DrawMoveButton(
            DiamondTwistController puzzle,
            string label,
            DiamondTwistMove move)
        {
            if (GUILayout.Button(label))
            {
                puzzle.RequestTurn(move);
            }
        }
    }
}
