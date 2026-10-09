using UnityEngine;
using NetworkCourse.BalloonFight.Simulation;

namespace NetworkCourse.BalloonFight.Engine
{
    public struct RenderSnapshot
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

        public static RenderSnapshot FromSimulation(
            in SimulationState state)
        {
            Vector3 fighter0Position = new Vector3(
                state.Fighter0.Position.X,
                state.Fighter0.Position.Y,
                0f);
            Vector3 fighter1Position = new Vector3(
                state.Fighter1.Position.X,
                state.Fighter1.Position.Y,
                0f);

            return new RenderSnapshot(
                state.NextTick,
                state.Fighter0.IsActive,
                in fighter0Position,
                state.Fighter1.IsActive,
                in fighter1Position);
        }

        public bool TryGetFighter(
            int slotIndex,
            out bool isActive,
            out Vector3 position)
        {
            if(slotIndex == 0)
            {
                isActive = Fighter0Active;
                position = Fighter0Position;
                return true;
            }

            if(slotIndex == 1)
            {
                isActive = Fighter1Active;
                position = Fighter1Position;
                return true;
            }


            isActive = false;
            position = default;
            return false;
            
        }
    }
}
