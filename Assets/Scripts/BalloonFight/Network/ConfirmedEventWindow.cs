namespace NetworkCourse.BalloonFight.Networking
{
    public struct ConfirmedEventWindow
    {
        public const int Capacity = 8;

        public bool HasBaseline;
        public uint BaselineEventId;
        public bool HistoryGap;
        public byte Count;
        private ConfirmedEvent _event0;
        private ConfirmedEvent _event1;
        private ConfirmedEvent _event2;
        private ConfirmedEvent _event3;
        private ConfirmedEvent _event4;
        private ConfirmedEvent _event5;
        private ConfirmedEvent _event6;
        private ConfirmedEvent _event7;

        public bool TryGet(int index, out ConfirmedEvent value)
        {
            switch (index)
            {
                case 0: value = _event0; return index < Count;
                case 1: value = _event1; return index < Count;
                case 2: value = _event2; return index < Count;
                case 3: value = _event3; return index < Count;
                case 4: value = _event4; return index < Count;
                case 5: value = _event5; return index < Count;
                case 6: value = _event6; return index < Count;
                case 7: value = _event7; return index < Count;
                default: value = default; return false;
            }
        }

        internal bool TryAppend(in ConfirmedEvent value)
        {
            if (Count >= Capacity) return false;

            switch (Count)
            {
                case 0: _event0 = value; break;
                case 1: _event1 = value; break;
                case 2: _event2 = value; break;
                case 3: _event3 = value; break;
                case 4: _event4 = value; break;
                case 5: _event5 = value; break;
                case 6: _event6 = value; break;
                case 7: _event7 = value; break;
                default: return false;
            }

            Count++;
            return true;
        }
    }
}