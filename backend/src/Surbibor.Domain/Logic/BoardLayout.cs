namespace Surbibor.Domain.Logic;

public static class BoardLayout
{
    public const int FreeSpacePosition = 12;
    public const int NonFreeSquareCount = BingoWinChecker.SquareCount - 1;

    /// <summary>
    /// Builds a randomized position-to-event mapping for the 24 non-free squares.
    /// </summary>
    public static Dictionary<int, Guid> Randomize(IReadOnlyList<Guid> eventIds, Random random)
    {
        if (eventIds.Count != NonFreeSquareCount)
        {
            throw new ArgumentException($"Expected exactly {NonFreeSquareCount} events, got {eventIds.Count}.", nameof(eventIds));
        }

        var shuffled = eventIds.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var positions = Enumerable.Range(0, BingoWinChecker.SquareCount).Where(p => p != FreeSpacePosition).ToList();

        var layout = new Dictionary<int, Guid>();
        for (var i = 0; i < positions.Count; i++)
        {
            layout[positions[i]] = shuffled[i];
        }

        return layout;
    }

    /// <summary>
    /// Validates a manually-chosen position-to-event mapping: must cover exactly the 24
    /// non-free positions, each exactly once, using exactly the supplied event ids.
    /// </summary>
    public static bool IsValidManualLayout(IReadOnlyDictionary<int, Guid> positions, IReadOnlyCollection<Guid> allowedEventIds)
    {
        if (positions.Count != NonFreeSquareCount)
        {
            return false;
        }

        if (positions.ContainsKey(FreeSpacePosition))
        {
            return false;
        }

        if (positions.Keys.Any(p => p < 0 || p >= BingoWinChecker.SquareCount))
        {
            return false;
        }

        var expectedEvents = new HashSet<Guid>(allowedEventIds);
        var usedEvents = new HashSet<Guid>(positions.Values);

        return usedEvents.Count == NonFreeSquareCount && usedEvents.SetEquals(expectedEvents);
    }
}
