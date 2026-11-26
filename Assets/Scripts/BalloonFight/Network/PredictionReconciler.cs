namespace NetworkCourse.BalloonFight.Simulation
{
    public enum ReconciliationStatus : byte
    {
        NoCorrection = 0,
        Replayed = 1,
        InvalidConfiguration = 2,
        WrongInputEpoch = 3,
        StaleAck = 4,
        MissingPrediction = 5,
        InvalidInputSequence = 6,
        RestoreFailed = 7,
        ReplayFailed = 8,
        PurgeFailed = 9,
        InvalidState = 10
    }

    public readonly struct PredictionReconciliationConfig
    {
        public readonly float PositionEpsilonSquared;
        public readonly float VelocityEpsilonSquared;

        public PredictionReconciliationConfig(
            float positionEpsilon,
            float velocityEpsilon)
        {
            bool valid = IsFinite(positionEpsilon)
                && IsFinite(velocityEpsilon)
                && positionEpsilon >= 0f
                && velocityEpsilon >= 0f;
            PositionEpsilonSquared = valid
                ? positionEpsilon * positionEpsilon
                : -1f;
            VelocityEpsilonSquared = valid
                ? velocityEpsilon * velocityEpsilon
                : -1f;
        }

        public bool IsValid()
        {
            return IsFinite(PositionEpsilonSquared)
                && IsFinite(VelocityEpsilonSquared)
                && PositionEpsilonSquared >= 0f
                && VelocityEpsilonSquared >= 0f;
        }

        public static PredictionReconciliationConfig CreateReference()
        {
            return new PredictionReconciliationConfig(
                positionEpsilon: 0.01f,
                velocityEpsilon: 0.02f);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct ReconciliationResult
    {
        public readonly ReconciliationStatus Status;
        public readonly uint InputEpoch;
        public readonly uint AcknowledgedInputTick;
        public readonly int ReplayedInputCount;
        public readonly float PositionErrorSquared;
        public readonly float VelocityErrorSquared;
        public readonly FighterState PreviousPredictedCurrent;
        public readonly FighterState NewPredictedCurrent;

        public bool Succeeded => Status == ReconciliationStatus.NoCorrection
            || Status == ReconciliationStatus.Replayed;
        public bool Corrected => Status == ReconciliationStatus.Replayed;

        public ReconciliationResult(
            ReconciliationStatus status,
            uint inputEpoch,
            uint acknowledgedInputTick,
            int replayedInputCount,
            float positionErrorSquared,
            float velocityErrorSquared,
            in FighterState previousPredictedCurrent,
            in FighterState newPredictedCurrent)
        {
            Status = status;
            InputEpoch = inputEpoch;
            AcknowledgedInputTick = acknowledgedInputTick;
            ReplayedInputCount = replayedInputCount;
            PositionErrorSquared = positionErrorSquared;
            VelocityErrorSquared = velocityErrorSquared;
            PreviousPredictedCurrent = previousPredictedCurrent;
            NewPredictedCurrent = newPredictedCurrent;
        }
    }

    public sealed class PredictionReconciler
    {
        private readonly PredictionReconciliationConfig _config;
        private readonly bool _configurationIsValid;

        public PredictionReconciler(
            in PredictionReconciliationConfig config)
        {
            _config = config;
            _configurationIsValid = config.IsValid();
        }

        public ReconciliationResult Reconcile(
            uint inputEpoch,
            uint acknowledgedInputTick,
            in FighterState officialAtAck,
            OwnerPredictionSimulation prediction,
            InputHistory inputHistory,
            PredictionHistory predictionHistory)
        {
            FighterState previousCurrent = prediction == null
                ? default
                : prediction.CurrentFighter;

            // 1. Garde-fous de configuration
            if (!_configurationIsValid
                || prediction == null
                || inputHistory == null
                || predictionHistory == null)
            {
                return Result(
                    ReconciliationStatus.InvalidConfiguration,
                    inputEpoch,
                    acknowledgedInputTick,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 2. Une seule Epoch valide, les anciennes générations n'ont pas droit de cité
            if (!prediction.IsInitialized
                || prediction.InputEpoch != inputEpoch
                || inputHistory.InputEpoch != inputEpoch
                || predictionHistory.InputEpoch != inputEpoch)
            {
                return Result(
                    ReconciliationStatus.WrongInputEpoch,
                    inputEpoch,
                    acknowledgedInputTick,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 3. Si le Ack est plus ancien que le dernier traité alors on l'ignore
            if (predictionHistory.HasAck
                && !Networking.SequenceMath.IsNewer(
                    acknowledgedInputTick,
                    predictionHistory.LastAckTick))
            {
                return Result(
                    ReconciliationStatus.StaleAck,
                    inputEpoch,
                    acknowledgedInputTick,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 4. État invalide (NaN / infini / autres états inconnus) sont refusés
            if (!OwnerPredictionSimulation.IsValidFighterState(
                    in previousCurrent)
                || !OwnerPredictionSimulation.IsValidFighterState(
                    in officialAtAck))
            {
                return Result(
                    ReconciliationStatus.InvalidState,
                    inputEpoch,
                    acknowledgedInputTick,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 5. Retrouver le snapshot prédit EXACTEMENT au tick aquitté
            PredictionHistoryStatus lookupStatus = predictionHistory.TryGet(
                inputEpoch,
                acknowledgedInputTick,
                out PredictionSnapshot predictedAtAck);
            if (lookupStatus != PredictionHistoryStatus.Success)
            {
                return Result(
                    ReconciliationStatus.MissingPrediction,
                    inputEpoch,
                    acknowledgedInputTick,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 6. Invalidation d'une attaque non légale
            FighterState predictedFighterAtAck = predictedAtAck.Fighter;
            if (!OwnerPredictionSimulation.IsValidFighterState(
                in predictedFighterAtAck))
            {
                return Result(
                    ReconciliationStatus.InvalidState,
                    inputEpoch,
                    acknowledgedInputTick,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 7. Comparaison:
            // - décision sur la phase et les cooldown
            // - ensuite des positions
            CalculateSpatialErrors(
                in predictedFighterAtAck,
                in officialAtAck,
                out float positionErrorSquared,
                out float velocityErrorSquared);

            bool decisionStateMatches =
                predictedFighterAtAck.Phase == officialAtAck.Phase
                && predictedFighterAtAck.FlapCooldownRemaining
                    == officialAtAck.FlapCooldownRemaining
                && predictedFighterAtAck.BalloonMask 
                   == officialAtAck.BalloonMask;

            bool spatialStateMatches = positionErrorSquared
                    <= _config.PositionEpsilonSquared
                && velocityErrorSquared
                    <= _config.VelocityEpsilonSquared;

            // 8. Si toutes les vérités sont les mêmes alors simple purge, AUCUN replay
            if (decisionStateMatches && spatialStateMatches)
            {
                if (!PurgeAcknowledged(
                    inputEpoch,
                    acknowledgedInputTick,
                    inputHistory,
                    predictionHistory))
                {
                    return new ReconciliationResult(
                        ReconciliationStatus.PurgeFailed,
                        inputEpoch,
                        acknowledgedInputTick,
                        0,
                        positionErrorSquared,
                        velocityErrorSquared,
                        in previousCurrent,
                        in previousCurrent);
                }

                return new ReconciliationResult(
                    ReconciliationStatus.NoCorrection,
                    inputEpoch,
                    acknowledgedInputTick,
                    0,
                    positionErrorSquared,
                    velocityErrorSquared,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 9. Prévalidation du replay
            if (!ValidateReplaySequence(
                inputEpoch,
                acknowledgedInputTick,
                inputHistory,
                out int replayCount))
            {
                return new ReconciliationResult(
                    ReconciliationStatus.InvalidInputSequence,
                    inputEpoch,
                    acknowledgedInputTick,
                    0,
                    positionErrorSquared,
                    velocityErrorSquared,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 10. Restore de l'état officiel APRES l'ack
            uint nextInputTick = unchecked(acknowledgedInputTick + 1u);
            if (prediction.Restore(
                inputEpoch,
                nextInputTick,
                in officialAtAck) != OwnerPredictionStatus.Success)
            {
                return new ReconciliationResult(
                    ReconciliationStatus.RestoreFailed,
                    inputEpoch,
                    acknowledgedInputTick,
                    0,
                    positionErrorSquared,
                    velocityErrorSquared,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 11. Le relay reconstruit les snapshots prédits à partir d'ici
            if (predictionHistory.PrepareReplayAfter(
                inputEpoch,
                acknowledgedInputTick)
                != PredictionHistoryStatus.Success)
            {
                return new ReconciliationResult(
                    ReconciliationStatus.RestoreFailed,
                    inputEpoch,
                    acknowledgedInputTick,
                    0,
                    positionErrorSquared,
                    velocityErrorSquared,
                    in previousCurrent,
                    in previousCurrent);
            }

            // 12. Rejouer SEULEMENT les inputs > ack
            int replayed = 0;
            for (int index = 0; index < inputHistory.Count; index++)
            {
                inputHistory.TryGetAt(index, out InputCommand command);
                if (!Networking.SequenceMath.IsNewer(
                    command.InputTick,
                    acknowledgedInputTick))
                {
                    continue;
                }

                OwnerPredictionStatus stepStatus = prediction.Step(
                    inputEpoch,
                    in command,
                    out PredictionSnapshot replayedSnapshot);
                if (stepStatus != OwnerPredictionStatus.Success
                    || predictionHistory.TryAppend(in replayedSnapshot)
                        != PredictionHistoryStatus.Success)
                {
                    FighterState failedCurrent = prediction.CurrentFighter;
                    return new ReconciliationResult(
                        ReconciliationStatus.ReplayFailed,
                        inputEpoch,
                        acknowledgedInputTick,
                        replayed,
                        positionErrorSquared,
                        velocityErrorSquared,
                        in previousCurrent,
                        in failedCurrent);
                }

                replayed++;
            }

            // 13. Purge les historiques qui sont au nombre de deux dans notre cas (<= ack)
            if (replayed != replayCount
                || inputHistory.PurgeThrough(
                    inputEpoch,
                    acknowledgedInputTick) != InputHistoryStatus.Success)
            {
                FighterState failedCurrent = prediction.CurrentFighter;
                return new ReconciliationResult(
                    ReconciliationStatus.PurgeFailed,
                    inputEpoch,
                    acknowledgedInputTick,
                    replayed,
                    positionErrorSquared,
                    velocityErrorSquared,
                    in previousCurrent,
                    in failedCurrent);
            }

            FighterState newCurrent = prediction.CurrentFighter;
            return new ReconciliationResult(
                ReconciliationStatus.Replayed,
                inputEpoch,
                acknowledgedInputTick,
                replayed,
                positionErrorSquared,
                velocityErrorSquared,
                in previousCurrent,
                in newCurrent);
        }

        // Vérifie la contiguïté du replay SANS rien modifier
        private static bool ValidateReplaySequence(
            uint inputEpoch,
            uint acknowledgedInputTick,
            InputHistory inputHistory,
            out int replayCount)
        {
            uint expectedTick = unchecked(acknowledgedInputTick + 1u);
            replayCount = 0;
            for (int index = 0; index < inputHistory.Count; index++)
            {
                inputHistory.TryGetAt(index, out InputCommand command);
                if (!Networking.SequenceMath.IsNewer(
                    command.InputTick,
                    acknowledgedInputTick))
                {
                    continue;
                }

                if (command.InputTick != expectedTick)
                {
                    replayCount = 0;
                    return false;
                }

                expectedTick = unchecked(expectedTick + 1u);
                replayCount++;
            }

            return inputHistory.InputEpoch == inputEpoch;
        }

        // Purge des deux historiques (input + prédit)
        private static bool PurgeAcknowledged(
            uint inputEpoch,
            uint acknowledgedInputTick,
            InputHistory inputHistory,
            PredictionHistory predictionHistory)
        {
            InputHistoryStatus inputStatus = inputHistory.PurgeThrough(
                inputEpoch,
                acknowledgedInputTick);
            PredictionHistoryStatus predictionStatus =
                predictionHistory.PurgeThrough(
                    inputEpoch,
                    acknowledgedInputTick);
            return (inputStatus == InputHistoryStatus.Success
                    || inputStatus == InputHistoryStatus.NothingToPurge)
                && (predictionStatus == PredictionHistoryStatus.Success
                    || predictionStatus
                        == PredictionHistoryStatus.NothingToPurge);
        }

        private static void CalculateSpatialErrors(
            in FighterState predicted,
            in FighterState official,
            out float positionErrorSquared,
            out float velocityErrorSquared)
        {
            float positionX = predicted.Position.X - official.Position.X;
            float positionY = predicted.Position.Y - official.Position.Y;
            float velocityX = predicted.Velocity.X - official.Velocity.X;
            float velocityY = predicted.Velocity.Y - official.Velocity.Y;
            positionErrorSquared = positionX * positionX
                + positionY * positionY;
            velocityErrorSquared = velocityX * velocityX
                + velocityY * velocityY;
        }

        private static ReconciliationResult Result(
            ReconciliationStatus status,
            uint inputEpoch,
            uint acknowledgedInputTick,
            in FighterState previousCurrent,
            in FighterState newCurrent)
        {
            return new ReconciliationResult(
                status,
                inputEpoch,
                acknowledgedInputTick,
                0,
                0f,
                0f,
                in previousCurrent,
                in newCurrent);
        }
    }
}