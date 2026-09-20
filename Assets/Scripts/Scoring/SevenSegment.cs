using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Which bars of a seven-segment digit are lit, and where each bar sits. Plain data
    /// with no scene dependency, so the readout can be checked without building one.
    ///
    /// A gym scoreboard is seven bars per digit for a reason: it stays legible across a
    /// court at a glance and from an angle. Here it also means the score needs no font,
    /// no canvas, and no imported text package — it is the same handful of boxes the rest
    /// of the court is built from.
    /// </summary>
    public static class SevenSegment
    {
        /// <summary>Bars in a digit, in the order the indices below use.</summary>
        public const int Count = 7;

        public const int Top = 0;
        public const int UpperLeft = 1;
        public const int UpperRight = 2;
        public const int Middle = 3;
        public const int LowerLeft = 4;
        public const int LowerRight = 5;
        public const int Bottom = 6;

        /// <summary>Digit value meaning the cell shows nothing, used for leading zeros.</summary>
        public const int Blank = -1;

        // One bit per bar, lowest bit is Top.
        private static readonly int[] Patterns =
        {
            0b1110111, // 0
            0b0100100, // 1
            0b1011101, // 2
            0b1101101, // 3
            0b0101110, // 4
            0b1101011, // 5
            0b1111011, // 6
            0b0100101, // 7
            0b1111111, // 8
            0b1101111  // 9
        };

        /// <summary>A rectangular bar of a digit, in the digit's own space: x right, y up, origin at its centre.</summary>
        public readonly struct Bar
        {
            public readonly Vector3 Position;
            public readonly Vector3 Size;

            public Bar(Vector3 position, Vector3 size)
            {
                Position = position;
                Size = size;
            }
        }

        /// <summary>Whether <paramref name="segment"/> is lit for <paramref name="digit"/>. A blank digit lights nothing.</summary>
        public static bool IsLit(int digit, int segment)
        {
            if (digit < 0 || digit > 9 || segment < 0 || segment >= Count)
                return false;

            return (Patterns[digit] & (1 << segment)) != 0;
        }

        /// <summary>
        /// The digit shown in cell <paramref name="index"/>, counting from the left, when
        /// <paramref name="value"/> is spread across <paramref name="count"/> cells.
        /// Leading zeros are <see cref="Blank"/>, so a score of 7 reads as 7 rather than
        /// 007, and a value too large for the display is held at all nines rather than
        /// wrapping round to something smaller than the score really is.
        /// </summary>
        public static int DigitAt(int value, int count, int index)
        {
            if (count <= 0 || index < 0 || index >= count)
                return Blank;

            int largest = Largest(count);
            value = Mathf.Clamp(value, 0, largest);

            int divisor = 1;
            for (int i = 0; i < count - 1 - index; i++)
                divisor *= 10;

            if (index < count - 1 && value < divisor)
                return Blank;

            return value / divisor % 10;
        }

        /// <summary>The largest value <paramref name="count"/> cells can show.</summary>
        public static int Largest(int count)
        {
            int largest = 0;
            for (int i = 0; i < count; i++)
                largest = largest * 10 + 9;

            return largest;
        }

        /// <summary>
        /// Where one bar of a digit <paramref name="width"/> by <paramref name="height"/>
        /// goes, drawn with bars <paramref name="thickness"/> across and
        /// <paramref name="depth"/> deep. Bars overlap by half their thickness at the
        /// corners, the way the corners of a real display meet.
        /// </summary>
        public static Bar Layout(int segment, float width, float height, float thickness, float depth)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            var across = new Vector3(width - thickness, thickness, depth);
            var upright = new Vector3(thickness, height * 0.5f - thickness, depth);
            float side = halfWidth - thickness * 0.5f;
            float row = height * 0.25f;

            switch (segment)
            {
                case Top: return new Bar(new Vector3(0f, halfHeight - thickness * 0.5f, 0f), across);
                case Middle: return new Bar(Vector3.zero, across);
                case Bottom: return new Bar(new Vector3(0f, -halfHeight + thickness * 0.5f, 0f), across);
                case UpperLeft: return new Bar(new Vector3(-side, row, 0f), upright);
                case UpperRight: return new Bar(new Vector3(side, row, 0f), upright);
                case LowerLeft: return new Bar(new Vector3(-side, -row, 0f), upright);
                case LowerRight: return new Bar(new Vector3(side, -row, 0f), upright);
                default: return new Bar(Vector3.zero, Vector3.zero);
            }
        }
    }
}
