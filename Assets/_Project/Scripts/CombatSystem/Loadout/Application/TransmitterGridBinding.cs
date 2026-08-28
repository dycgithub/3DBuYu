using System;

namespace CombatSystem
{
    public static class TransmitterGridBinding
    {
        public static int ResolveIndex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return -1;

            string name = value.Trim();
            if (name.Equals("Transmitter", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Bottom", StringComparison.OrdinalIgnoreCase) || name == "下")
                return 0;
            if (name.Equals("Transmitter (1)", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("BottomLeft", StringComparison.OrdinalIgnoreCase) || name == "左下")
                return 1;
            if (name.Equals("Transmitter (2)", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("BottomRight", StringComparison.OrdinalIgnoreCase) || name == "右下")
                return 2;
            if (name.Equals("Transmitter (3)", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Left", StringComparison.OrdinalIgnoreCase) || name == "左")
                return 3;
            if (name.Equals("Transmitter (4)", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Right", StringComparison.OrdinalIgnoreCase) || name == "右")
                return 4;
            if (name.Equals("Transmitter (5)", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Top", StringComparison.OrdinalIgnoreCase) || name == "上")
                return 5;
            if (name.Equals("Transmitter (6)", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("TopLeft", StringComparison.OrdinalIgnoreCase) || name == "左上")
                return 6;
            if (name.Equals("Transmitter (7)", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("TopRight", StringComparison.OrdinalIgnoreCase) || name == "右上")
                return 7;

            return -1;
        }
    }
}
