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

        public const byte Balloon0Bit = 1 << 0;
        public const byte Balloon1Bit = 1 << 0;
        public const byte ValidBalloonMask  = Balloon0Bit | Balloon1Bit;

        public FighterPhase Phase;

        public bool IsActive;

        public SimVector2 Position;
        public SimVector2 Velocity;

        public float FlapCooldownRemaining;

        public byte BalloonMask;
        public ushort InvulnerabilityTicksRemaining;

        public int BalloonCount
        {

            get
            {
                int count = 0;
                if ((BalloonMask & Balloon0Bit) != 0) count++;
                if ((BalloonMask & Balloon1Bit) != 0) count++;
                return count;
            }

        }

        public bool IsBalloonActive (int balloonIndex)
        {
            if (balloonIndex == 0)
            {
                return (BalloonMask & Balloon0Bit) != 0;
            }
            if (balloonIndex == 1)
            {
                return (BalloonMask & Balloon1Bit) != 0;
            }

            return false;
        }

        public static FighterState CreateAtSpawn(in SimVector2 position)
        {
            return new FighterState
            {
                Phase = FighterPhase.Flying,
                Position = position,
                Velocity = new SimVector2(0f, 0f),
                FlapCooldownRemaining = 0f,
                BalloonMask = ValidBalloonMask,
                InvulnerabilityTicksRemaining = 0

            };
        }



        public static FighterState CreateActive(in SimVector2 position)
        {
            return CreateAtSpawn(in position);
        }
    }
}
