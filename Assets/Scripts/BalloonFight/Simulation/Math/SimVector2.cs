namespace NetworkCourse.BalloonFight.Simulation
{
    // Float arithmetic is the explicit Unity Network course override documented
    // in AGENTS.md. This type remains independent from Unity and NGO.
    public readonly struct SimVector2
    {
        public float X { get; }
        public float Y { get; }

        public SimVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static SimVector2 Zero => new SimVector2(0f, 0f);

        public static SimVector2 Add(in SimVector2 left, in SimVector2 right)
        {
            return new SimVector2(left.X + right.X, left.Y + right.Y);
        }

        public static SimVector2 Subtract(in SimVector2 left, in SimVector2 right)
        {
            return new SimVector2(left.X - right.X, left.Y - right.Y);
        }

        public static SimVector2 Multiply(in SimVector2 value, float scalar)
        {
            return new SimVector2(value.X * scalar, value.Y * scalar);
        }

        public static SimVector2 Lerp(
            in SimVector2 from,
            in SimVector2 to,
            float alpha)
        {
            return new SimVector2(
                from.X + (to.X - from.X) * alpha,
                from.Y + (to.Y - from.Y) * alpha);
        }

        public float LengthSquared()
        {
            return X * X + Y * Y;
        }
    }
}
