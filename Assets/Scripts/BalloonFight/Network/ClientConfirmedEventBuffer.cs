namespace NetworkCourse.BalloonFight.Networking
{
    public sealed class ClientConfirmedEventBuffer
    {
        public const int Capacity = ConfirmedEventWindow.Capacity;

        private readonly ConfirmedEvent[] _events =
            new ConfirmedEvent[Capacity];
        private int _count;

        public int Count => _count;

        public void Clear()
        {
            _count = 0;
        }

        public bool TryAppend(in ConfirmedEvent value)
        {
            if (_count >= Capacity) return false;
            _events[_count] = value;
            _count++;
            return true;
        }

        public bool TryGetAt(int index, out ConfirmedEvent value)
        {
            if (index < 0 || index >= _count)
            {
                value = default;
                return false;
            }

            value = _events[index];
            return true;
        }
    }
}