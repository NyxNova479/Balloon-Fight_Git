using System;

namespace NetworkCourse.BalloonFight.Simulation
{
    public readonly struct FighterInput
    {
        public readonly uint Tick;
        public readonly float Horizontal;
        public readonly bool FlapPressed;

        public FighterInput(uint tick, float horizontal, bool flapPressed)
        {
            Tick = tick;
            Horizontal = SanitizeHorizontal(horizontal);
            FlapPressed = flapPressed;
            
        }

        private static float SanitizeHorizontal(float value)
        {
            if (float.IsNaN(value)) return 0f;

            value = Math.Clamp(value, -1f, 1f);

            return value;
        }

        public static FighterInput Neutral(uint tick)
        {
            return new FighterInput(tick, 0f, false);
        }


    }
}
 