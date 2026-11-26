namespace NetworkCourse.BalloonFight.Simulation
{


    public readonly struct InputCommand
    {

        public readonly uint InputTick;
        public readonly float Horizontal;
        public readonly bool FlapHeld;

        public InputCommand(
            uint inputTick,
            float horizontal,
            bool flapHeld)
        {
            FighterInput sanitized = new FighterInput(inputTick, horizontal, flapHeld);
            InputTick = inputTick;
            Horizontal = horizontal;
            FlapHeld = flapHeld;


        }

        public static InputCommand FromWireUnchecked(
            uint inputTick,
            float horizontal,
            bool flapHeld)
        {
            return new InputCommand(
                inputTick, horizontal, flapHeld);
        }

        public bool IsValid()
        {
            return !float.IsNaN(Horizontal)
                && Horizontal >= -1f
                && Horizontal <= 1f;
        }
        

    }


}