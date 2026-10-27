using NetworkCourse.BalloonFight.Simulation;
using Unity.Netcode;

namespace NetworkCourse.BalloonFight.Networking
{
    public struct NetworkFighterState : INetworkSerializable
    {
        public byte Phase;
        public float PositionX;
        public float PositionY;
        public float VelocityX;
        public float VelocityY;
        public float FlapCooldownRemaining;

        public static NetworkFighterState FromSimulation(
            in FighterState state)
        {
            return new NetworkFighterState
            {
                Phase = (byte)state.Phase,
                PositionX = state.Position.X,
                PositionY = state.Position.Y,
                VelocityX = state.Velocity.X,
                VelocityY = state.Velocity.Y,
                FlapCooldownRemaining = state.FlapCooldownRemaining
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref PositionX);
            serializer.SerializeValue(ref PositionY);
            serializer.SerializeValue(ref VelocityX);
            serializer.SerializeValue(ref VelocityY);
            serializer.SerializeValue(ref FlapCooldownRemaining);
        }
    }

    public struct NetworkMatchState : INetworkSerializable
    {
        public byte Phase;
        public uint RoundIndex;
        public uint ResetApplyTick;

        public static NetworkMatchState FromSimulation(in MatchState state)
        {
            return new NetworkMatchState
            {
                Phase = (byte)state.Phase,
                RoundIndex = state.RoundIndex,
                ResetApplyTick = state.ResetApplyTick
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref RoundIndex);
            serializer.SerializeValue(ref ResetApplyTick);
        }
    }

    public struct NetworkSnapshot : INetworkSerializable
    {
        public uint ServerTick;
        public byte SimulationActiveMask;
        public byte OccupiedSlotMask;
        public ulong Slot0ClientId;
        public ulong Slot1ClientId;
        public NetworkFighterState Fighter0;
        public NetworkFighterState Fighter1;
        public NetworkMatchState Match;

        public static NetworkSnapshot Create(
            in SimulationSnapshot snapshot,
            byte occupiedSlotMask,
            ulong slot0ClientId,
            ulong slot1ClientId)
        {
            FighterState fighter0 = snapshot.Fighters.Get(0);
            FighterState fighter1 = snapshot.Fighters.Get(1);
            MatchState match = snapshot.Match;

            return new NetworkSnapshot
            {
                ServerTick = snapshot.Tick,
                SimulationActiveMask = snapshot.ActiveMask,
                OccupiedSlotMask = occupiedSlotMask,
                Slot0ClientId = slot0ClientId,
                Slot1ClientId = slot1ClientId,
                Fighter0 = NetworkFighterState.FromSimulation(in fighter0),
                Fighter1 = NetworkFighterState.FromSimulation(in fighter1),
                Match = NetworkMatchState.FromSimulation(in match)
            };
        }

        public bool TryGetLocalSlot(ulong localClientId, out int slot)
        {
            if ((OccupiedSlotMask & SimulationState.Fighter0Bit) != 0
                && Slot0ClientId == localClientId)
            {
                slot = 0;
                return true;
            }

            if ((OccupiedSlotMask & SimulationState.Fighter1Bit) != 0
                && Slot1ClientId == localClientId)
            {
                slot = 1;
                return true;
            }

            slot = -1;
            return false;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ServerTick);
            serializer.SerializeValue(ref SimulationActiveMask);
            serializer.SerializeValue(ref OccupiedSlotMask);
            serializer.SerializeValue(ref Slot0ClientId);
            serializer.SerializeValue(ref Slot1ClientId);
            serializer.SerializeValue(ref Fighter0);
            serializer.SerializeValue(ref Fighter1);
            serializer.SerializeValue(ref Match);
        }
    }
}