using Surbibor.Domain.Logic;

namespace Surbibor.Api.Tests.Unit;

public class BoardLayoutTests
{
    [Fact]
    public void Randomize_PlacesEveryEventExactlyOnce_AndNeverOnFreeSpace()
    {
        var eventIds = Enumerable.Range(0, BoardLayout.NonFreeSquareCount).Select(_ => Guid.NewGuid()).ToList();

        var layout = BoardLayout.Randomize(eventIds, new Random(42));

        Assert.Equal(BoardLayout.NonFreeSquareCount, layout.Count);
        Assert.DoesNotContain(BoardLayout.FreeSpacePosition, layout.Keys);
        Assert.Equal(eventIds.ToHashSet(), layout.Values.ToHashSet());
    }

    [Fact]
    public void Randomize_Throws_WhenWrongEventCount()
    {
        var eventIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        Assert.Throws<ArgumentException>(() => BoardLayout.Randomize(eventIds, new Random(1)));
    }

    [Fact]
    public void IsValidManualLayout_True_ForCompleteDistinctPlacement()
    {
        var eventIds = Enumerable.Range(0, BoardLayout.NonFreeSquareCount).Select(_ => Guid.NewGuid()).ToList();
        var positions = Enumerable.Range(0, 25).Where(p => p != BoardLayout.FreeSpacePosition).ToList();

        var layout = positions.Zip(eventIds, (p, e) => (p, e)).ToDictionary(t => t.p, t => t.e);

        Assert.True(BoardLayout.IsValidManualLayout(layout, eventIds));
    }

    [Fact]
    public void IsValidManualLayout_False_WhenFreeSpaceUsed()
    {
        var eventIds = Enumerable.Range(0, BoardLayout.NonFreeSquareCount).Select(_ => Guid.NewGuid()).ToList();
        var positions = Enumerable.Range(0, 25).Where(p => p != BoardLayout.FreeSpacePosition).ToList();
        positions[0] = BoardLayout.FreeSpacePosition;

        var layout = positions.Zip(eventIds, (p, e) => (p, e)).ToDictionary(t => t.p, t => t.e);

        Assert.False(BoardLayout.IsValidManualLayout(layout, eventIds));
    }

    [Fact]
    public void IsValidManualLayout_False_WhenDuplicateEventUsed()
    {
        var eventIds = Enumerable.Range(0, BoardLayout.NonFreeSquareCount).Select(_ => Guid.NewGuid()).ToList();
        var positions = Enumerable.Range(0, 25).Where(p => p != BoardLayout.FreeSpacePosition).ToList();

        var layout = positions.Zip(eventIds, (p, e) => (p, e)).ToDictionary(t => t.p, t => t.e);
        var firstKey = layout.Keys.First();
        var secondKey = layout.Keys.Skip(1).First();
        layout[secondKey] = layout[firstKey];

        Assert.False(BoardLayout.IsValidManualLayout(layout, eventIds));
    }

    [Fact]
    public void IsValidManualLayout_False_WhenMissingPositions()
    {
        var eventIds = Enumerable.Range(0, BoardLayout.NonFreeSquareCount).Select(_ => Guid.NewGuid()).ToList();
        var positions = Enumerable.Range(0, 25).Where(p => p != BoardLayout.FreeSpacePosition).Take(20).ToList();

        var layout = positions.Zip(eventIds, (p, e) => (p, e)).ToDictionary(t => t.p, t => t.e);

        Assert.False(BoardLayout.IsValidManualLayout(layout, eventIds));
    }
}
