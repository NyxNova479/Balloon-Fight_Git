using NetworkCourse.BalloonFight.Simulation;
using UnityEngine;

namespace NetworkCourse.BalloonFight.Engine
{
    public readonly struct RenderSnapshot
    {
        public readonly uint Tick;
        public readonly bool Fighter0Active;
        public readonly Vector3 Fighter0Position;
        public readonly bool Fighter1Active;
        public readonly Vector3 Fighter1Position;

        private RenderSnapshot(
            uint tick,
            bool fighter0Active,
            in Vector3 fighter0Position,
            bool fighter1Active,
            in Vector3 fighter1Position)
        {
            Tick = tick;
            Fighter0Active = fighter0Active;
            Fighter0Position = fighter0Position;
            Fighter1Active = fighter1Active;
            Fighter1Position = fighter1Position;
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
            Vector3 fighter0 = new Vector3(
                fighter0Position.X,
                fighter0Position.Y,
                0f);
            Vector3 fighter1 = new Vector3(
                fighter1Position.X,
                fighter1Position.Y,
                0f);

            return new RenderSnapshot(
                serverTick,
                (activeMask & SimulationState.Fighter0Bit) != 0,
                in fighter0,
                (activeMask & SimulationState.Fighter1Bit) != 0,
                in fighter1);
        }

        public bool TryGetFighter(
            int slotIndex,
            out bool isActive,
            out Vector3 position)
        {
            if (slotIndex == 0)
            {
                isActive = Fighter0Active;
                position = Fighter0Position;
                return true;
            }

            if (slotIndex == 1)
            {
                isActive = Fighter1Active;
                position = Fighter1Position;
                return true;
            }

            isActive = false;
            position = default;
            return false;
        }

        private static RenderSnapshot Create(
            uint tick,
            bool fighter0Active,
            in FighterState fighter0,
            bool fighter1Active,
            in FighterState fighter1)
        {
            Vector3 fighter0Position = new Vector3(
                fighter0.Position.X,
                fighter0.Position.Y,
                0f);
            Vector3 fighter1Position = new Vector3(
                fighter1.Position.X,
                fighter1.Position.Y,
                0f);

            return new RenderSnapshot(
                tick,
                fighter0Active,
                in fighter0Position,
                fighter1Active,
                in fighter1Position);
        }
    }
}