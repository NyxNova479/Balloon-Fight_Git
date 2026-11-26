namespace NetworkCourse.BalloonFight.Simulation
{
    public static class FighterMotionSystem
    {
        public static void Step(
            ref FighterState state,
            in FighterInput input,
            in SimulationConfig config)
        {
            if (state.Phase == FighterPhase.Eliminated) return;

            float dt = config.TickDeltaTime;
            float cooldown = state.FlapCooldownRemaining - dt;
            if (cooldown < 0f) cooldown = 0f;

            bool canControl = state.Phase == FighterPhase.Flying;
            float horizontalInput = canControl ? input.Horizontal : 0f;
            float targetVelocityX = horizontalInput
                * config.MaxHorizontalSpeed;
            float horizontalRate = Abs(horizontalInput) > 0.001f
                ? config.HorizontalAcceleration
                : config.HorizontalDrag;
            float velocityX = MoveTowards(
                state.Velocity.X,
                targetVelocityX,
                horizontalRate * dt);

            float velocityY = state.Velocity.Y;
            if (canControl && input.FlapPressed && cooldown <= 0f)
            {
                velocityY += config.FlapImpulse;
                cooldown = config.FlapCooldown;
            }

            float lift = 0f;
            if (canControl)
            {
                lift = state.BalloonCount == 1
                    ? config.LiftAcceleration
                        * config.OneBalloonLiftMultiplier
                    : config.LiftAcceleration;
            }

            velocityY += (lift - config.GravityAcceleration) * dt;
            velocityY = Clamp(
                velocityY,
                -config.MaxFallSpeed,
                config.MaxRiseSpeed);

            float positionX = state.Position.X + velocityX * dt;
            float positionY = state.Position.Y + velocityY * dt;
            ClampToArena(
                ref positionX,
                ref positionY,
                ref velocityX,
                ref velocityY,
                in config);

            state.Position = new SimVector2(positionX, positionY);
            state.Velocity = new SimVector2(velocityX, velocityY);
            state.FlapCooldownRemaining = cooldown;
        }

        public static void ResetForRound(
            ref FighterState state,
            in SimVector2 spawnPosition)
        {
            state.Position = spawnPosition;
            state.Velocity = new SimVector2(0f, 0f);
            state.FlapCooldownRemaining = 0f;
        }

        private static void ClampToArena(
            ref float positionX,
            ref float positionY,
            ref float velocityX,
            ref float velocityY,
            in SimulationConfig config)
        {
            if (positionX < config.XMin)
            {
                positionX = config.XMin;
                if (velocityX < 0f) velocityX = 0f;
            }
            else if (positionX > config.XMax)
            {
                positionX = config.XMax;
                if (velocityX > 0f) velocityX = 0f;
            }

            if (positionY < config.YMin)
            {
                positionY = config.YMin;
                if (velocityY < 0f) velocityY = 0f;
            }
            else if (positionY > config.YMax)
            {
                positionY = config.YMax;
                if (velocityY > 0f) velocityY = 0f;
            }
        }

        private static float MoveTowards(
            float current,
            float target,
            float maxDelta)
        {
            float delta = target - current;
            if (Abs(delta) <= maxDelta) return target;
            return current + (delta < 0f ? -maxDelta : maxDelta);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }
    }
}