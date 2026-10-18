using System;

namespace Play
{
    /// <summary>Stores 32 discrete stickers in fixed puzzle-local faces, independent of camera and animation.</summary>
    public sealed class DiamondNetState
    {
        private const int FaceCount = 8;
        private const int SlotsPerFace = 4;
        private readonly int[] _colors = new int[FaceCount * SlotsPerFace];
        private static readonly int[,] Axes = { { 1, -1, -1 }, { -1, 1, -1 }, { -1, -1, 1 }, { 1, 1, 1 } };

        /// <summary>Creates the imported model's solved face identities; face bits encode positive X/Y/Z.</summary>
        public DiamondNetState()
        {
            for (int index = 0; index < _colors.Length; index++)
            {
                _colors[index] = index / SlotsPerFace;
            }
        }

        /// <summary>Reads a color identity; slots are center, X corner, Y corner, Z corner.</summary>
        public int GetColor(int face, int slot) => _colors[face * SlotsPerFace + slot];

        /// <summary>Commits a full 120-degree move using exact signed-integer rotation, never tween poses.</summary>
        public void Apply(DiamondTwistMove move)
        {
            int value = (int)move;
            if (value < 0 || value >= 8)
            {
                throw new ArgumentOutOfRangeException(nameof(move));
            }

            int axis = value / 2;
            int direction = value % 2 == 0 ? -1 : 1;
            int[] next = (int[])_colors.Clone();
            for (int face = 0; face < FaceCount; face++)
            {
                int[] normal = { Sign(face, 4), Sign(face, 2), Sign(face, 1) };
                for (int slot = 0; slot < SlotsPerFace; slot++)
                {
                    int[] piece = slot == 0 ? (int[])normal.Clone() : new int[3];
                    if (slot > 0)
                    {
                        piece[slot - 1] = normal[slot - 1];
                    }

                    if (Dot(piece, axis) <= 0)
                    {
                        continue;
                    }

                    int[] rotated = Rotate(normal, axis, direction);
                    int[] position = Rotate(piece, axis, direction);
                    int targetFace = (rotated[0] > 0 ? 4 : 0) | (rotated[1] > 0 ? 2 : 0) | (rotated[2] > 0 ? 1 : 0);
                    int targetSlot = slot == 0 ? 0 : position[0] != 0 ? 1 : position[1] != 0 ? 2 : 3;
                    next[targetFace * SlotsPerFace + targetSlot] = _colors[face * SlotsPerFace + slot];
                }
            }

            Array.Copy(next, _colors, next.Length);
        }

        /// <summary>Decodes one signed component of a fixed face normal.</summary>
        private static int Sign(int face, int mask) => (face & mask) == 0 ? -1 : 1;

        /// <summary>Projects an integer piece position onto the selected tetrahedral axis.</summary>
        private static int Dot(int[] vector, int axis) => vector[0] * Axes[axis, 0] + vector[1] * Axes[axis, 1] + vector[2] * Axes[axis, 2];

        /// <summary>Rodrigues at ±120 degrees simplifies to exact integer arithmetic for these axes.</summary>
        private static int[] Rotate(int[] vector, int axis, int direction)
        {
            int dot = Dot(vector, axis);
            int[] result = new int[3];
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                int k = (i + 2) % 3;
                result[i] = (-vector[i] + Axes[axis, i] * dot + direction * (Axes[axis, j] * vector[k] - Axes[axis, k] * vector[j])) / 2;
            }

            return result;
        }
    }
}
