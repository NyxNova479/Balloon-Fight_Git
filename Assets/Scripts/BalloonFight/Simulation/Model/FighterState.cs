using System.Drawing;

namespace NetworkCourse.BalloonFight.Simulation
{

    public struct FighterState
    {
        public bool IsActive;

        public SimVector2 Position;
        public SimVector2 Velocity;

        public float FlapCooldownRemaining;

        public static FighterState CreateActive(in SimVector2 position)
        {
            return new FighterState
            {
                IsActive = true,
                Position = position,
                Velocity = new SimVector2(0f, 0f),
                FlapCooldownRemaining = 0f

            };
        }

        public static FighterState CreateInactive()
        {
            return new FighterState
            {
                IsActive = false,
                Position = new SimVector2(0f, 0f),
                Velocity = new SimVector2(0f, 0f),
                FlapCooldownRemaining = 0f
            };
        }
    }
}
