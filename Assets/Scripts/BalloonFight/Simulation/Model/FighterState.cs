using System.Drawing;

namespace NetworkCourse.BalloonFight.Simulation
{

    public enum FighterPhase
    {
        Flying = 0,
        Falling = 1,
        Eliminated = 2

    }

    public struct FighterState
    {
        public FighterPhase Phase;

        public bool IsActive;

        public SimVector2 Position;
        public SimVector2 Velocity;

        public float FlapCooldownRemaining;

        public static FighterState CreateAtSpawn(in SimVector2 position)
        {
            return new FighterState
            {
                IsActive = true,
                Position = position,
                Velocity = new SimVector2(0f, 0f),
                FlapCooldownRemaining = 0f

            };
        }



        public static FighterState CreateActive(in SimVector2 position)
        {
            return CreateAtSpawn(in position);
        }
    }
}
