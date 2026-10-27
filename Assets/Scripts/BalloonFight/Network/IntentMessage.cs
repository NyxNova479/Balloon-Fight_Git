using Unity.Netcode;

namespace NetworkCourse.BalloonFight.Networking
{

    public struct IntentMessage : INetworkSerializable
    {

        public byte ClaimedSlot;
        public float Horizontal;
        public bool FlapHeld;
        public uint IntentSequence;
        public uint FlapPressSequence;

        public IntentMessage(
            byte claimedSlot,
            float horizontal,
            bool flapHeld,
            uint intentSequence,
            uint flapPressSequence)
        {
            ClaimedSlot = claimedSlot;
            Horizontal = horizontal;
            FlapHeld = flapHeld;
            IntentSequence = intentSequence;
            FlapPressSequence = flapPressSequence;
        }

        public bool IsPayloadValid()
        {
            return ClaimedSlot < 2
                && !float.IsNaN(Horizontal)
                && !float.IsInfinity(Horizontal)
                && Horizontal >= -1f
                && Horizontal <= 1f;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClaimedSlot);
            serializer.SerializeValue(ref Horizontal);
            serializer.SerializeValue(ref FlapHeld);
            serializer.SerializeValue(ref IntentSequence);
            serializer.SerializeValue(ref FlapPressSequence);
        }

    }

    public static class SequenceMath
    {
        public static bool TryGetForwardDistance(
            uint candidate,
            uint reference,
            uint maximumDistance,
            out uint distance)
        {
            distance = unchecked(candidate - reference);
            return distance > 0u
                && distance <= 0x7FFF_FFFFu
                && distance <= maximumDistance;
        }

        public static bool IsSame(uint candidate, uint reference)
        {
            return candidate == reference;
        }
    }

}