using NetworkCourse.BalloonFight.Simulation;
using UnityEngine;

namespace NetworkCourse.BalloonFight.Engine
{
    public readonly struct RenderSnapshot
    {
        public readonly uint Tick;
        public readonly bool Fighter0Active;
        public readonly Vector3 Fighter0Position;
        public readonly Vector3 Fighter0OfficialPosition;
        public readonly bool Fighter0ShowsOfficialGhost;
        public readonly bool Fighter1Active;
        public readonly Vector3 Fighter1Position;
        public readonly Vector3 Fighter1OfficialPosition;
        public readonly bool Fighter1ShowsOfficialGhost;

        private RenderSnapshot(
            uint tick,
            bool fighter0Active,
            in Vector3 fighter0Position,
            in Vector3 fighter0OfficialPosition,
            bool fighter0ShowsOfficialGhost,
            bool fighter1Active,
            in Vector3 fighter1Position,
            in Vector3 fighter1OfficialPosition,
            bool fighter1ShowsOfficialGhost)
        {
            Tick = tick;
            Fighter0Active = fighter0Active;
            Fighter0Position = fighter0Position;
            Fighter0OfficialPosition = fighter0OfficialPosition;
            Fighter0ShowsOfficialGhost = fighter0ShowsOfficialGhost;
            Fighter1Active = fighter1Active;
            Fighter1Position = fighter1Position;
            Fighter1OfficialPosition = fighter1OfficialPosition;
            Fighter1ShowsOfficialGhost = fighter1ShowsOfficialGhost;
        }

        public static RenderSnapshot FromSimulationState(
            in SimulationState state)
        {
            FighterState fighter0 = state.Fighters.Get(0);
            FighterState fighter1 = state.Fighters.Get(1);
            return Create(
                state.NextTick,
                state.IsSlotActive(0),
                in fighter0,
                state.IsSlotActive(1),
                in fighter1);
        }

        public static RenderSnapshot FromSimulationSnapshot(
            in SimulationSnapshot snapshot)
        {
            FighterState fighter0 = snapshot.Fighters.Get(0);
            FighterState fighter1 = snapshot.Fighters.Get(1);
            return Create(
                snapshot.Tick,
                snapshot.IsSlotActive(0),
                in fighter0,
                snapshot.IsSlotActive(1),
                in fighter1);
        }

        public static RenderSnapshot FromNetworkValues(
            uint serverTick,
            byte activeMask,
            in SimVector2 fighter0Position,
            in SimVector2 fighter1Position)
        {
            Vector3 fighter0 = ToVector3(in fighter0Position);
            Vector3 fighter1 = ToVector3(in fighter1Position);
            return new RenderSnapshot(
                serverTick,
                IsActive(activeMask, SimulationState.Fighter0Bit),
                in fighter0,
                in fighter0,
                false,
                IsActive(activeMask, SimulationState.Fighter1Bit),
                in fighter1,
                in fighter1,
                false);
        }

        public static RenderSnapshot FromPresentation(
            uint officialTick,
            byte officialActiveMask,
            in SimVector2 officialFighter0Position,
            in SimVector2 officialFighter1Position,
            byte interpolatedActiveMask,
            in SimVector2 interpolatedFighter0Position,
            in SimVector2 interpolatedFighter1Position,
            int localSlotIndex,
            bool useOwnerPrediction,
            in FighterState predictedOwner)
        {
            Vector3 official0 = ToVector3(in officialFighter0Position);
            Vector3 official1 = ToVector3(in officialFighter1Position);
            Vector3 interpolated0 = ToVector3(
                in interpolatedFighter0Position);
            Vector3 interpolated1 = ToVector3(
                in interpolatedFighter1Position);
            Vector3 predicted = ToVector3(in predictedOwner.Position);

            bool fighter0IsOwner = localSlotIndex == 0;
            bool fighter1IsOwner = localSlotIndex == 1;
            bool official0Active = IsActive(
                officialActiveMask,
                SimulationState.Fighter0Bit);
            bool official1Active = IsActive(
                officialActiveMask,
                SimulationState.Fighter1Bit);
            bool interpolated0Active = IsActive(
                interpolatedActiveMask,
                SimulationState.Fighter0Bit);
            bool interpolated1Active = IsActive(
                interpolatedActiveMask,
                SimulationState.Fighter1Bit);

            Vector3 shown0 = fighter0IsOwner
                ? (useOwnerPrediction ? predicted : official0)
                : interpolated0;
            Vector3 shown1 = fighter1IsOwner
                ? (useOwnerPrediction ? predicted : official1)
                : interpolated1;

            return new RenderSnapshot(
                officialTick,
                fighter0IsOwner ? official0Active : interpolated0Active,
                in shown0,
                in official0,
                fighter0IsOwner && useOwnerPrediction,
                fighter1IsOwner ? official1Active : interpolated1Active,
                in shown1,
                in official1,
                fighter1IsOwner && useOwnerPrediction);
        }

        public bool TryGetFighter(
            int slotIndex,
            out bool isActive,
            out Vector3 position)
        {
            return TryGetFighter(
                slotIndex,
                out isActive,
                out position,
                out _,
                out _);
        }

        public bool TryGetFighter(
            int slotIndex,
            out bool isActive,
            out Vector3 position,
            out Vector3 officialPosition,
            out bool showOfficialGhost)
        {
            if (slotIndex == 0)
            {
                isActive = Fighter0Active;
                position = Fighter0Position;
                officialPosition = Fighter0OfficialPosition;
                showOfficialGhost = Fighter0ShowsOfficialGhost;
                return true;
            }

            if (slotIndex == 1)
            {
                isActive = Fighter1Active;
                position = Fighter1Position;
                officialPosition = Fighter1OfficialPosition;
                showOfficialGhost = Fighter1ShowsOfficialGhost;
                return true;
            }

            isActive = false;
            position = default;
            officialPosition = default;
            showOfficialGhost = false;
            return false;
        }

        private static RenderSnapshot Create(
            uint tick,
            bool fighter0Active,
            in FighterState fighter0,
            bool fighter1Active,
            in FighterState fighter1)
        {
            Vector3 fighter0Position = ToVector3(in fighter0.Position);
            Vector3 fighter1Position = ToVector3(in fighter1.Position);
            return new RenderSnapshot(
                tick,
                fighter0Active,
                in fighter0Position,
                in fighter0Position,
                false,
                fighter1Active,
                in fighter1Position,
                in fighter1Position,
                false);
        }

        private static bool IsActive(byte mask, byte bit)
        {
            return (mask & bit) != 0;
        }

        private static Vector3 ToVector3(in SimVector2 position)
        {
            return new Vector3(position.X, position.Y, 0f);
        }
    }
}