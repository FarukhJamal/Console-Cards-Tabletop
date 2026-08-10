namespace ConsoleCards.Core.Randomness
{
    /// <summary>
    /// Supplies injectable integer randomness without coupling Unity-free rules to a static generator.
    /// </summary>
    public interface IRandomValueSource
    {
        int NextInt(int minimumInclusive, int maximumExclusive);
    }
}
