using NetworkCourse.BalloonFight.Common;
using NetworkCourse.BalloonFight.Networking;

namespace NetworkCourse.BalloonFight.Simulation
{
    public enum InputHistoryStatus : byte
    {
        Success = 0,
        WrongEpoch = 1,
        StaleOrDuplicate = 2,
        Gap = 3,
        HistoryFull = 4,
        AckAhead = 5,
        NothingToPurge = 6
    }

    public sealed class InputHistory
    {
        public const int Capacity = 256;

        private readonly BoundedRing<InputCommand> _commands =
            new BoundedRing<InputCommand>(Capacity);

        private uint _inputEpoch;
        private uint _nextInputTick;
        private bool _isReady;
        private bool _hasProduced;
        private uint _lastProducedTick;
        private bool _hasAck;
        private uint _lastAckTick;

        public int Count => _commands.Count;
        public uint InputEpoch => _inputEpoch;
        public uint NextInputTick => _nextInputTick;
        public bool IsReady => _isReady;
        public bool HasProduced => _hasProduced;
        public bool HasAck => _hasAck;
        public uint LastAckTick => _lastAckTick;

        // Démarre un flux pour une nouvelle epoch décidée par le serveur
        // Purge l'historique et repart à inputTick = 0
        public void BeginEpoch(uint inputEpoch)
        {
            _commands.Clear();
            _inputEpoch = inputEpoch;
            _nextInputTick = 0u;
            _isReady = true;
            _hasProduced = false;
            _lastProducedTick = 0u;
            _hasAck = false;
            _lastAckTick = 0u;
        }

        // Ferme le flux et on nettoie. Aucun input n'est accepté tant que BegnEpoch n'a pas été appelé
        public void Reset()
        {
            _commands.Clear();
            _inputEpoch = 0u;
            _nextInputTick = 0u;
            _isReady = false;
            _hasProduced = false;
            _lastProducedTick = 0u;
            _hasAck = false;
            _lastAckTick = 0u;
        }

        // Ajoute la commande du tick exact attendu.
        // WrongEpoch : le paquet n'appartient pas au flux courant
        // StaleOrDuplicate : tick déjà vu ou en retard
        // Gap : le tick attendu n'est pas celui reçu
        // HistoryFull : si c'est plein, le runner déclenche un hard resync.
        public InputHistoryStatus TryAppend(
            uint inputEpoch,
            in InputCommand command)
        {
            if (!_isReady || inputEpoch != _inputEpoch)
            {
                return InputHistoryStatus.WrongEpoch;
            }

            if (command.InputTick != _nextInputTick)
            {
                return SequenceMath.IsAtOrBefore(
                    command.InputTick,
                    _nextInputTick)
                    ? InputHistoryStatus.StaleOrDuplicate
                    : InputHistoryStatus.Gap;
            }

            if (!_commands.TryEnqueue(in command))
            {
                return InputHistoryStatus.HistoryFull;
            }

            _lastProducedTick = command.InputTick;
            _hasProduced = true;
            _nextInputTick = unchecked(_nextInputTick + 1u);
            return InputHistoryStatus.Success;
        }

        // Libère tout ce qui est <= ack server.
        // AckAhead : l'ack dépasse ce qu'on a produit.
        // NothingToPurge : ack déjà appliqué, rien à faire.
        // On inspecte la tête avec TryPeek avant de la retirer avec TryDequeue
        public InputHistoryStatus PurgeThrough(
            uint inputEpoch,
            uint acknowledgedInputTick)
        {
            if (!_isReady || inputEpoch != _inputEpoch)
            {
                return InputHistoryStatus.WrongEpoch;
            }

            if (!_hasProduced)
            {
                return InputHistoryStatus.AckAhead;
            }

            if (_hasAck
                && !SequenceMath.IsNewer(
                    acknowledgedInputTick,
                    _lastAckTick))
            {
                return InputHistoryStatus.NothingToPurge;
            }

            if (SequenceMath.IsNewer(
                acknowledgedInputTick,
                _lastProducedTick))
            {
                return InputHistoryStatus.AckAhead;
            }

            while (_commands.TryPeek(out InputCommand oldest)
                && SequenceMath.IsAtOrBefore(
                    oldest.InputTick,
                    acknowledgedInputTick))
            {
                _commands.TryDequeue(out _);
            }

            _hasAck = true;
            _lastAckTick = acknowledgedInputTick;
            return InputHistoryStatus.Success;
        }

        // Lecture par index logique (0 = plus ancien).
        // InputBatch.TryCreateNewest copie les 8 plus récentes avec ça.
        public bool TryGetAt(int logicalIndex, out InputCommand command)
        {
            return _commands.TryGetAt(logicalIndex, out command);
        }

        // Recherche par tick dans la fenêtre courante
        // (hors-chemin chaud : sert au debug/replay)
        public bool TryGet(
            uint inputEpoch,
            uint inputTick,
            out InputCommand command)
        {
            if (!_isReady || inputEpoch != _inputEpoch)
            {
                command = default;
                return false;
            }

            for (int index = 0; index < _commands.Count; index++)
            {
                _commands.TryGetAt(index, out InputCommand candidate);
                if (candidate.InputTick == inputTick)
                {
                    command = candidate;
                    return true;
                }
            }

            command = default;
            return false;
        }
    }
}