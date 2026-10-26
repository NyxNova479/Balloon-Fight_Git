namespace NetworkCourse.BalloonFight.Simulation
{
    public struct FighterStateCollection
    {
        public const int Capacity = 2;

        private FighterState _slot0;
        private FighterState _slot1;

        public FighterState Get(int slotIndex)
        {
            if (slotIndex == 0) return _slot0;
            if (slotIndex == 1) return _slot1;
            return default;
        }

        public bool Set(int slotIndex, in FighterState value)
        {
            if (slotIndex == 0)
            {
                _slot0 = value;
                return true;
            }

            if (slotIndex == 1)
            {
                _slot1 = value;
                return true;
            }

            return false;
        }
    }
}