using NetworkCourse.BalloonFight.Networking;
using NetworkCourse.BalloonFight.Simulation;
using System;
using Unity.Profiling;
using UnityEngine;

namespace NetworkCourse.BalloonFight.Engine
{

    public enum TickRunnerMode : byte
    {
        Dormant = 0,
        Server = 1,
        Client = 2
    }

    public enum TickRunnerStatus : byte
    {
        Idle = 0,
        Running = 1,
        ConfigurationError = 2,
        SimulationError = 3,
        TickDebtExceeded = 4
    }

    public sealed class BalloonTickRunner : MonoBehaviour
    {

        private const int MaxTicksPerFrame = 4;
        private const int MaxTickDebt = 50;

        private static readonly ProfilerMarker SimulationStepMarker =
            new ProfilerMarker("BalloonFight.ServerSimulationStep");
        private static readonly ProfilerMarker RenderStepMarker =
            new ProfilerMarker("BalloonFight.RenderStep");

        [Header("Dependencies")]
        [SerializeField] private BalloonFighterView _fighter0View;
        [SerializeField] private BalloonFighterView _fighter1View;

        [Header("Runtime diagnostics")]
        [SerializeField] private TickRunnerMode _mode;
        [SerializeField] private TickRunnerStatus _status;
        [SerializeField] private uint _completedServerTicks;
        [SerializeField] private uint _lastOfficialServerTick;
        [SerializeField] private int _lastFrameTickCount;
        [SerializeField] private int _maximumFrameTickCount;
        [SerializeField] private int _pendingTickDebt;
        [SerializeField] private int _rejectedOfficialSnapshots;

        private NetworkCommandInbox _inbox;
        private BalloonSimulation _simulation;
        private SimulationSnapshot _latestSimulationSnapshot;
        private RenderSnapshot _latestRenderSnapshot;
        private bool _hasSimulationSnapshot;
        private bool _hasOfficialSnapshot;
        private bool _serverSimulationEnabled;
        private double _accumulatorSeconds;
        private double _tickDeltaSeconds;

        public TickRunnerMode Mode => _mode;
        public TickRunnerStatus Status => _status;
        public uint CompletedServerTicks => _completedServerTicks;
        public uint LastOfficialServerTick => _lastOfficialServerTick;
        public int PendingTickDebt => _pendingTickDebt;
        public int RejectedOfficialSnapshots => _rejectedOfficialSnapshots;

        // TODO : On continue ici
        private void Awake()
        {

            if (!ValidateViews())
            {
                StopWithError(TickRunnerStatus.ConfigurationError, "BalloonTickRunner views are invalid.");
                return;

            }

            HideViews();
            _mode = TickRunnerMode.Dormant;
            _status = TickRunnerStatus.Idle;


            
        }


        public bool StartServer(NetworkCommandInbox inbox)
        {
            if (_status == TickRunnerStatus.ConfigurationError
                || _mode != TickRunnerMode.Dormant
                || inbox == null)
            {
                return false;
            }
            SimulationConfig config = SimulationConfig.CreateReference();

            if (!config.IsValid()) return false;

            SimulationState initialState = SimulationState.CreateTwoFighters(
                in config.Fighter0Spawn,
                in config.Fighter1Spawn);

            _simulation = new BalloonSimulation(in config, in initialState);
            _inbox = inbox;
            _tickDeltaSeconds = config.TickDeltaTime;
            _mode = TickRunnerMode.Server;
            _status = TickRunnerStatus.Running;

            return true;


        }

        public bool StartClient()
        {
            if (_status == TickRunnerStatus.ConfigurationError
                || _mode != TickRunnerMode.Dormant)
            {
                return false;
            }
            _mode = TickRunnerMode.Client;
            _status = TickRunnerStatus.Running;
            return true;
        }

        public void SetServerSimulationEnabled(bool isEnabled)
        {
            if (_mode != TickRunnerMode.Server) return;

            _serverSimulationEnabled = isEnabled;
            if (!isEnabled)
            {
                _accumulatorSeconds = 0d;
                _pendingTickDebt = 0;
            }
        }

        public void StopNetworkMode()
        {
            //1. Remet l'état au repos complet
            _mode = TickRunnerMode.Dormant;
            _status = TickRunnerStatus.Idle;

            //2. Coupe les références
            _inbox = null;
            _simulation = null;

            //3. Purge les drapeaux d'états
            _hasSimulationSnapshot = false;
            _hasOfficialSnapshot = false;
            _serverSimulationEnabled = false;

            //4. Remet à 0 l'horloge interne et la dette (en gors l'accumulateur quoi)
            _accumulatorSeconds = 0d;
            _pendingTickDebt = 0;

            //5. Remet à 0 les métriques et compteurs de ticks
            _completedServerTicks = 0u;
            _lastOfficialServerTick = 0u;
            _lastFrameTickCount = 0;
            _maximumFrameTickCount = 0;
            _rejectedOfficialSnapshots = 0;

            //6. Masque immédiatement les combattant à l'écran
            HideViews();
        }

        // En Web vous avez 2 choix : TCP ou UDP
        public bool TryReceiveOfficialSnapshot(
            uint serverTick,
            byte activeMask,
            in SimVector2 fighter0Position,
            in SimVector2 fighter1Position
            )
        {

            //1. Filtrage anti-désordre (ou Out-of-Order / Replay) :
            // Si on a déjà un snapshot, on vérifie que ce nouveau serverTick est bien STRICTEMENT DANS LE FUTUR.
            // S'il est plus vvieux ou égal, on le dégage pour éviter les saccades/retours en arrière.
            if (_hasOfficialSnapshot
                && !SequenceMath.TryGetForwardDistance(serverTick,
                _lastOfficialServerTick,
                0x7FFF_FFFFu,
                out _))
            {
                _rejectedOfficialSnapshots++;
                return false;
            }

            //2. Conversion en Snapshot de rendu
            _latestRenderSnapshot = RenderSnapshot.FromNetworkValues(
                serverTick,
                activeMask,
                in fighter0Position,
                in fighter1Position
                );

            //3. Mise à jour de la référence officiel côté client
            _lastOfficialServerTick = serverTick;
            _hasOfficialSnapshot = true;
            return true;


        }

        private void AdvanceServerSimulation()
        {

            _accumulatorSeconds += Time.unscaledDeltaTime;
            double maximumDebtSeconds = _tickDeltaSeconds * MaxTickDebt;

            if(_accumulatorSeconds > maximumDebtSeconds)
            {

                StopWithError(
                    TickRunnerStatus.TickDebtExceeded, 
                    $"Balloon Fight server tick debt exceeded {MaxTickDebt} ticks.");

                return;
            }

            _lastFrameTickCount = 0;

            while (_accumulatorSeconds >= _tickDeltaSeconds
                && _lastFrameTickCount < MaxTicksPerFrame)
            {
                using (SimulationStepMarker.Auto())
                {
                    if (!ServerSimulationStep()) return;
                }
                _accumulatorSeconds -= _tickDeltaSeconds;
                _completedServerTicks++;
                _lastFrameTickCount++;
            }

            _pendingTickDebt = (int)(_accumulatorSeconds / _tickDeltaSeconds);
            if (_lastFrameTickCount > _maximumFrameTickCount)
            {
                _maximumFrameTickCount = _lastFrameTickCount;
            }

        }

        private bool ServerSimulationStep()
        {
            uint tick = _simulation.NextTick;
            SimulationState state = _simulation.CurrentState;
            FighterInput fighter0 = _inbox.CreateInput(
                0,
                tick,
                CanExecuteFlap(in state, 0));
            FighterInput fighter1 = _inbox.CreateInput(
                1,
                tick,
                CanExecuteFlap(in state, 1));

            SimulationResult result = _simulation.Step(
                in fighter0,
                in fighter1);
            if (!result.Succeeded)
            {
                StopWithError(
                    TickRunnerStatus.SimulationError,
                    "Balloon Fight server simulation step failed.");
                return false;
            }

            _latestSimulationSnapshot = result.Snapshot;
            _hasSimulationSnapshot = true;
            return true;
        }

        private static bool CanExecuteFlap(
            in SimulationState state,
            int slot)
        {
            if (state.Match.Phase != MatchPhase.Playing
                || !state.IsSlotActive(slot))
            {
                return false;
            }

            FighterState fighter = state.Fighters.Get(slot);
            return fighter.Phase == FighterPhase.Flying
                && fighter.FlapCooldownRemaining <= 0f;
        }

        private void RenderStep()
        {
            if (!_fighter0View.RenderStep(in _latestRenderSnapshot)
                || !_fighter1View.RenderStep(in _latestRenderSnapshot))
            {
                StopWithError(
                    TickRunnerStatus.ConfigurationError,
                    "A fighter view rejected the official snapshot.");
            }
        }

        private void Update()
        {
            if (_status != TickRunnerStatus.Running) return;
            if (_mode == TickRunnerMode.Server
                && _serverSimulationEnabled)
            {
                AdvanceServerSimulation();
            }
            if (_status != TickRunnerStatus.Running
                || !_hasOfficialSnapshot)
            {
                return;
            }
            using (RenderStepMarker.Auto())
            {
                RenderStep();
            }
        }

        public bool TryGetLatestSimulationSnapshot(
            out SimulationSnapshot snapshot)
        {
            snapshot = _latestSimulationSnapshot;
            return _mode == TickRunnerMode.Server
                && _hasSimulationSnapshot;
        }

        private void HideViews()
        {
            RenderSnapshot hidden = default;
            _fighter0View.RenderStep(in hidden);
            _fighter1View.RenderStep(in hidden);
        }

        private bool ValidateViews()
        {
            return _fighter0View != null
                && _fighter0View.IsConfigured
                && _fighter0View.SlotIndex == 0
                && _fighter1View != null
                && _fighter1View.IsConfigured
                && _fighter1View.SlotIndex == 1;
        }

        private void StopWithError(
            TickRunnerStatus status,
            string message)
        {
            _status = status;
            Debug.LogError(message, this);
        }

    }

}