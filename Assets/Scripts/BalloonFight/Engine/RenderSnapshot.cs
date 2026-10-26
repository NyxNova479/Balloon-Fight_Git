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
            in RenderSnapshot official,
            in RenderSnapshot interpolated,
            int localSlotIndex)
        {
            Vector3 official0 = official.Fighter0Position;
            Vector3 official1 = official.Fighter1Position;
            Vector3 interpolated0 = interpolated.Fighter0Position;
            Vector3 interpolated1 = interpolated.Fighter1Position;

            bool fighter0IsOwner = localSlotIndex == 0;
            bool fighter1IsOwner = localSlotIndex == 1;
            bool hasOwner = fighter0IsOwner || fighter1IsOwner;
            bool official0Active = official.Fighter0Active;
            bool official1Active = official.Fighter1Active;
            bool interpolated0Active = interpolated.Fighter0Active;
            bool interpolated1Active = interpolated.Fighter1Active;

            Vector3 shown0 = fighter0IsOwner
                ? official0
                : interpolated0;
            Vector3 shown1 = fighter1IsOwner
                ? official1
                : interpolated1;

            return new RenderSnapshot(
                official.Tick,
                fighter0IsOwner ? official0Active : interpolated0Active,
                in shown0,
                in official0,
                hasOwner && !fighter0IsOwner,
                fighter1IsOwner ? official1Active : interpolated1Active,
                in shown1,
                in official1,
                hasOwner && !fighter1IsOwner);
        }

        public bool TryGetFighter(
            int slotIndex,
            out bool isActive,
            out Vector3 position)
        {
            return TryGetFighter(
                slotIndex, out isActive, out position, out _, out _);
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

        private static bool IsActive(byte activeMask, byte fighterBit)
        {
            return (activeMask & fighterBit) != 0;
        }

        private static Vector3 ToVector3(in SimVector2 position)
        {
            return new Vector3(position.X, position.Y, 0f);
        }
    }
}
