namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The games with code this build ships with (CART-1b). This is the one place the table names a game: each
    /// line registers one code cartridge. A new game with code adds its cartridge here; a game made of data only
    /// (a game box, a game a player made) needs no line.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private void RegisterBuiltInCartridges(GameCartridgeRegistry registry)
        {
            registry.Register(TrapFloorGame); // Trap Floor (Games/TrapFloor; its table half is TabletopComposition.TrapFloor.cs)
        }
    }
}
