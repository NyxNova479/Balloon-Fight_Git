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
        public byte BalloonMask;
        public ushort InvulnerabilityTicksRemaining;

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
                FlapCooldownRemaining = state.FlapCooldownRemaining,
                BalloonMask = state.BalloonMask,
                InvulnerabilityTicksRemaining =
                    state.InvulnerabilityTicksRemaining
            };
        }

        public bool TryToSimulation(out FighterState state)
        {
            if (Phase > (byte)FighterPhase.Eliminated
                || !IsFinite(PositionX)
                || !IsFinite(PositionY)
                || !IsFinite(VelocityX)
                || !IsFinite(VelocityY)
                || !IsFinite(FlapCooldownRemaining)
                || FlapCooldownRemaining < 0f
                || (BalloonMask & ~FighterState.ValidBalloonMask) != 0
                || (Phase == (byte)FighterPhase.Flying
                    && BalloonMask == 0)
                || (Phase != (byte)FighterPhase.Flying
                    && BalloonMask != 0))
            {
                state = default;
                return false;
            }

            state = new FighterState
            {
                Phase = (FighterPhase)Phase,
                Position = new SimVector2(PositionX, PositionY),
                Velocity = new SimVector2(VelocityX, VelocityY),
                FlapCooldownRemaining = FlapCooldownRemaining,
                BalloonMask = BalloonMask,
                InvulnerabilityTicksRemaining =
                    InvulnerabilityTicksRemaining
            };
            return true;
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
            serializer.SerializeValue(ref BalloonMask);
            serializer.SerializeValue(ref InvulnerabilityTicksRemaining);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public struct NetworkMatchState : INetworkSerializable
    {
        public byte Phase;
        public uint RoundIndex;
        public uint ResetApplyTick;
        public uint Fighter0Score;
        public uint Fighter1Score;
        public byte WinnerSlot;
        public bool HasLastRoundResult;
        public byte LastRoundWinnerSlot;

        public static NetworkMatchState FromSimulation(in MatchState state)
        {
            return new NetworkMatchState
            {
                Phase = (byte)state.Phase,
                RoundIndex = state.RoundIndex,
                ResetApplyTick = state.ResetApplyTick,
                Fighter0Score = state.Fighter0Score,
                Fighter1Score = state.Fighter1Score,
                WinnerSlot = state.WinnerSlot,
                HasLastRoundResult = state.HasLastRoundResult,
                LastRoundWinnerSlot = state.LastRoundWinnerSlot
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref RoundIndex);
            serializer.SerializeValue(ref ResetApplyTick);
            serializer.SerializeValue(ref Fighter0Score);
            serializer.SerializeValue(ref Fighter1Score);
            serializer.SerializeValue(ref WinnerSlot);
            serializer.SerializeValue(ref HasLastRoundResult);
            serializer.SerializeValue(ref LastRoundWinnerSlot);
        }
    }

    public struct NetworkConfirmedEvent : INetworkSerializable
    {
        public uint EventId;
        public uint Tick;
        public byte Type;
        public byte ActorSlot;
        public byte TargetSlot;
        public byte BalloonIndex;

        public static NetworkConfirmedEvent FromDomain(
            in ConfirmedEvent value)
        {
            return new NetworkConfirmedEvent
            {
                EventId = value.EventId,
                Tick = value.Event.Tick,
                Type = (byte)value.Event.Type,
                ActorSlot = value.Event.ActorSlot,
                TargetSlot = value.Event.TargetSlot,
                BalloonIndex = value.Event.BalloonIndex
            };
        }

        public bool TryToDomain(out ConfirmedEvent value)
        {
            if (Type > (byte)SimulationEventType.RoundDraw
                || (ActorSlot > 1 && ActorSlot != byte.MaxValue)
                || (TargetSlot > 1 && TargetSlot != byte.MaxValue)
                || (BalloonIndex > 1 && BalloonIndex != byte.MaxValue))
            {
                value = default;
                return false;
            }

            SimulationEvent simulationEvent = new SimulationEvent(
                Tick,
                (SimulationEventType)Type,
                ActorSlot,
                TargetSlot,
                BalloonIndex);
            value = new ConfirmedEvent(EventId, in simulationEvent);
            return true;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref EventId);
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref ActorSlot);
            serializer.SerializeValue(ref TargetSlot);
            serializer.SerializeValue(ref BalloonIndex);
        }
    }

    public struct NetworkConfirmedEventWindow : INetworkSerializable
    {
        public bool HasBaseline;
        public uint BaselineEventId;
        public bool HistoryGap;
        public byte Count;
        private NetworkConfirmedEvent _event0;
        private NetworkConfirmedEvent _event1;
        private NetworkConfirmedEvent _event2;
        private NetworkConfirmedEvent _event3;
        private NetworkConfirmedEvent _event4;
        private NetworkConfirmedEvent _event5;
        private NetworkConfirmedEvent _event6;
        private NetworkConfirmedEvent _event7;

        public static NetworkConfirmedEventWindow FromDomain(
            in ConfirmedEventWindow source)
        {
            NetworkConfirmedEventWindow result = new NetworkConfirmedEventWindow
            {
                HasBaseline = source.HasBaseline,
                BaselineEventId = source.HasBaseline
                    ? source.BaselineEventId
                    : 0u,
                HistoryGap = source.HistoryGap,
                Count = 0
            };
            for (int index = 0; index < source.Count; index++)
            {
                source.TryGet(index, out ConfirmedEvent value);
                NetworkConfirmedEvent network =
                    NetworkConfirmedEvent.FromDomain(in value);
                result.TrySet(index, in network);
                result.Count++;
            }

            return result;
        }

        public bool TryToDomain(out ConfirmedEventWindow result)
        {
            result = default;
            if (Count > ConfirmedEventWindow.Capacity
                || (!HasBaseline && BaselineEventId != 0u)
                || (HistoryGap && Count == 0))
            {
                return false;
            }

            result.HasBaseline = HasBaseline;
            result.BaselineEventId = HasBaseline ? BaselineEventId : 0u;
            result.HistoryGap = HistoryGap;
            for (int index = 0; index < Count; index++)
            {
                if (!TryGet(index, out NetworkConfirmedEvent network)
                    || !network.TryToDomain(out ConfirmedEvent value)
                    || !result.TryAppend(in value))
                {
                    result = default;
                    return false;
                }
            }

            return true;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref HasBaseline);
            serializer.SerializeValue(ref BaselineEventId);
            serializer.SerializeValue(ref HistoryGap);
            serializer.SerializeValue(ref Count);
            SerializeEvent(serializer, ref _event0);
            SerializeEvent(serializer, ref _event1);
            SerializeEvent(serializer, ref _event2);
            SerializeEvent(serializer, ref _event3);
            SerializeEvent(serializer, ref _event4);
            SerializeEvent(serializer, ref _event5);
            SerializeEvent(serializer, ref _event6);
            SerializeEvent(serializer, ref _event7);
        }

        private bool TryGet(int index, out NetworkConfirmedEvent value)
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

        private bool TrySet(
            int index,
            in NetworkConfirmedEvent value)
        {
            switch (index)
            {
                case 0: _event0 = value; return true;
                case 1: _event1 = value; return true;
                case 2: _event2 = value; return true;
                case 3: _event3 = value; return true;
                case 4: _event4 = value; return true;
                case 5: _event5 = value; return true;
                case 6: _event6 = value; return true;
                case 7: _event7 = value; return true;
                default: return false;
            }
        }

        private static void SerializeEvent<T>(
            BufferSerializer<T> serializer,
            ref NetworkConfirmedEvent value)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref value);
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
        public NetworkInputAck Slot0InputAck;
        public NetworkInputAck Slot1InputAck;
        public NetworkConfirmedEventWindow Slot0ConfirmedEvents;
        public NetworkConfirmedEventWindow Slot1ConfirmedEvents;

        public static NetworkSnapshot Create(
            in SimulationSnapshot snapshot,
            byte occupiedSlotMask,
            ulong slot0ClientId,
            ulong slot1ClientId,
            in NetworkInputAck slot0InputAck,
            in NetworkInputAck slot1InputAck,
            in ConfirmedEventWindow slot0ConfirmedEvents,
            in ConfirmedEventWindow slot1ConfirmedEvents)
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
                Match = NetworkMatchState.FromSimulation(in match),
                Slot0InputAck = slot0InputAck,
                Slot1InputAck = slot1InputAck,
                Slot0ConfirmedEvents =
                    NetworkConfirmedEventWindow.FromDomain(
                        in slot0ConfirmedEvents),
                Slot1ConfirmedEvents =
                    NetworkConfirmedEventWindow.FromDomain(
                        in slot1ConfirmedEvents)
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

        public bool TryGetLocalInputAck(
            ulong localClientId,
            out NetworkInputAck ack)
        {
            if ((OccupiedSlotMask & SimulationState.Fighter0Bit) != 0
                && Slot0ClientId == localClientId)
            {
                ack = Slot0InputAck;
                return true;
            }

            if ((OccupiedSlotMask & SimulationState.Fighter1Bit) != 0
                && Slot1ClientId == localClientId)
            {
                ack = Slot1InputAck;
                return true;
            }

            ack = default;
            return false;
        }

        public bool TryGetLocalPredictionBase(
            ulong localClientId,
            out NetworkInputAck ack,
            out FighterState fighter)
        {
            if ((OccupiedSlotMask & SimulationState.Fighter0Bit) != 0
                && Slot0ClientId == localClientId)
            {
                ack = Slot0InputAck;
                return Fighter0.TryToSimulation(out fighter);
            }

            if ((OccupiedSlotMask & SimulationState.Fighter1Bit) != 0
                && Slot1ClientId == localClientId)
            {
                ack = Slot1InputAck;
                return Fighter1.TryToSimulation(out fighter);
            }

            ack = default;
            fighter = default;
            return false;
        }

        public bool TryGetLocalConfirmedEvents(
            ulong localClientId,
            out ConfirmedEventWindow window)
        {
            if ((OccupiedSlotMask & SimulationState.Fighter0Bit) != 0
                && Slot0ClientId == localClientId)
            {
                return Slot0ConfirmedEvents.TryToDomain(out window);
            }

            if ((OccupiedSlotMask & SimulationState.Fighter1Bit) != 0
                && Slot1ClientId == localClientId)
            {
                return Slot1ConfirmedEvents.TryToDomain(out window);
            }

            window = default;
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
            serializer.SerializeValue(ref Slot0InputAck);
            serializer.SerializeValue(ref Slot1InputAck);
            serializer.SerializeValue(ref Slot0ConfirmedEvents);
            serializer.SerializeValue(ref Slot1ConfirmedEvents);
        }
    }
}