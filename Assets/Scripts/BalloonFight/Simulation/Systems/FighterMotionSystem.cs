namespace NetworkCourse.BalloonFight.Simulation
{
    public static class FighterMotionSystem
    {
        public static void Step(
            ref FighterState state,
            in FighterInput input,
            in SimulationConfig config)
        {
            if (!state.IsActive) return;

            float dt = config.TickDeltaTime;

            float cooldown = state.FlapCooldownRemaining - dt;
            if (cooldown < 0f) cooldown = 0f;

            float targetVelocityX = input.Horizontal
                * config.MaxHorizontalSpeed;
            float horizontalRate = Abs(input.Horizontal) > 0.001f
                ? config.HorizontalAcceleration
                : config.HorizontalDrag;
            float velocityX = MoveTowards(
                state.Velocity.X,
                targetVelocityX,
                horizontalRate * dt);

            float velocityY = state.Velocity.Y;
            if (input.FlapPressed && cooldown <= 0f)
            {
                velocityY += config.FlapImpulse;
                cooldown = config.FlapCooldown;
            }

            float verticalAcceleration = config.LiftAcceleration
                - config.GravityAcceleration;
            velocityY += verticalAcceleration * dt;
            velocityY = Clamp(
                velocityY,
                -config.MaxFallSpeed,
                config.MaxRiseSpeed);

            float positionX = state.Position.X + velocityX * dt;
            float positionY = state.Position.Y + velocityY * dt;

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

            state.Position = new SimVector2(positionX, positionY);
            state.Velocity = new SimVector2(velocityX, velocityY);
            state.FlapCooldownRemaining = cooldown;
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