using NetworkCourse.BalloonFight.Simulation;
using Unity.Netcode;

namespace NetworkCourse.BalloonFight.Networking
{

    // 8 commandes
    public struct InputBatch : INetworkSerializable
    {
        public const int Capacity = 8;

        //En-tête : qui parle, dans quelle générattion, combien de commandes utiles
        public byte ClaimedSlot;
        public uint InputEpoch;
        public byte Count;

        // 8 cases fixes
        private InputCommand _command0;
        private InputCommand _command1;
        private InputCommand _command2;
        private InputCommand _command3;
        private InputCommand _command4;
        private InputCommand _command5;
        private InputCommand _command6;
        private InputCommand _command7;

        // Copie des 8 commandes les plus récentes de l'historique

        public static bool TryCreateNewest(
            byte claimedSlot,
            InputHistory history,
            out InputBatch batch)
        {
            batch = default;
            if (history == null
                || !history.IsReady
                || history.Count == 0
                || claimedSlot > 1)
            {
                return false;
            }

            int count = history.Count < Capacity
                ? history.Count
                : Capacity;
            int firstIndex = history.Count - count;
            batch.ClaimedSlot = claimedSlot;
            batch.InputEpoch = history.InputEpoch;
            batch.Count = (byte)count;

            for (int index = 0; index < count; index++)
            {
                if (!history.TryGetAt(
                    firstIndex + index,
                    out InputCommand command)
                    || !batch.TrySet(index, in command))
                {
                    batch = default;
                    return false;
                }
            }

            return true;
        }

        // 
        public bool IsStructurallyValid()
        {
            if (ClaimedSlot > 1 || Count > Capacity) return false;

            uint previousTick = 0u;
            for (int index = 0; index < Count; index++)
            {
                if (!TryGet(index, out InputCommand command)
                    || !command.IsValid())
                {
                    return false;
                }

                if (index > 0
                    && command.InputTick
                        != unchecked(previousTick + 1u))
                {
                    return false;
                }

                previousTick = command.InputTick;
            }

            return true;
        }

        // Lecture d'une case active (index < Count)
        public bool TryGet(int index, out InputCommand command)
        {
            switch (index)
            {
                case 0: command = _command0; return index < Count;
                case 1: command = _command1; return index < Count;
                case 2: command = _command2; return index < Count;
                case 3: command = _command3; return index < Count;
                case 4: command = _command4; return index < Count;
                case 5: command = _command5; return index < Count;
                case 6: command = _command6; return index < Count;
                case 7: command = _command7; return index < Count;
                default: command = default; return false;
            }
        }

        // Sérialise toujours les 8 cases : tailles fixes, pas de collection dynamique.
        // Count dit combien sont réellement utiles.
        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClaimedSlot);
            serializer.SerializeValue(ref InputEpoch);
            serializer.SerializeValue(ref Count);
            SerializeCommand(serializer, 0);
            SerializeCommand(serializer, 1);
            SerializeCommand(serializer, 2);
            SerializeCommand(serializer, 3);
            SerializeCommand(serializer, 4);
            SerializeCommand(serializer, 5);
            SerializeCommand(serializer, 6);
            SerializeCommand(serializer, 7);
        }

        // Écrit tick + axe + flap d'une case
        private void SerializeCommand<T>(
            BufferSerializer<T> serializer,
            int index)
            where T : IReaderWriter
        {
            TryGetRaw(index, out InputCommand command);
            uint tick = command.InputTick;
            float horizontal = command.Horizontal;
            bool flapHeld = command.FlapHeld;
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref horizontal);
            serializer.SerializeValue(ref flapHeld);

            if (!serializer.IsReader) return;

            InputCommand received = InputCommand.FromWireUnchecked(
                tick,
                horizontal,
                flapHeld);
            TrySet(index, in received);
        }

        // Accès brut aux 8 cases, indépendemment de Count (pour la sérialisation)
        private bool TryGetRaw(int index, out InputCommand command)
        {
            switch (index)
            {
                case 0: command = _command0; return true;
                case 1: command = _command1; return true;
                case 2: command = _command2; return true;
                case 3: command = _command3; return true;
                case 4: command = _command4; return true;
                case 5: command = _command5; return true;
                case 6: command = _command6; return true;
                case 7: command = _command7; return true;
                default: command = default; return false;
            }
        }

        // Écriture dans une case fixe (création local ou décodage réseau)
        private bool TrySet(int index, in InputCommand command)
        {
            switch (index)
            {
                case 0: _command0 = command; return true;
                case 1: _command1 = command; return true;
                case 2: _command2 = command; return true;
                case 3: _command3 = command; return true;
                case 4: _command4 = command; return true;
                case 5: _command5 = command; return true;
                case 6: _command6 = command; return true;
                case 7: _command7 = command; return true;
                default: return false;
            }
        }
    }
}