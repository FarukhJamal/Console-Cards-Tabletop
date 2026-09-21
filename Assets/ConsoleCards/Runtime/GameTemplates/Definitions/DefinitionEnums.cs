namespace ConsoleCards.GameTemplates.Definitions
{
    public enum ControllerInput
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
        A = 4,
        B = 5,
        X = 6,
        Y = 7,
    }

    public enum CardOrientation
    {
        Portrait = 0,
        Landscape = 1,
    }

    public enum ModeBehavior
    {
        Team = 0,
        Survival = 1,
    }

    public enum CollapseScheduleKind
    {
        None = 0,
        RoundBased = 1,
        RealTime = 2,
    }

    public enum ConsoleStackingPolicy
    {
        SingleCard = 0,
        Stack = 1,
    }

    public enum ControllerMappingKind
    {
        None = 0,
        Fixed = 1,
        PlayerConfigured = 2,
        ControllerCards = 3,
    }
}
