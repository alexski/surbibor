namespace Surbibor.Domain.Logic;

public enum WinLineType
{
    Row,
    Column,
    Diagonal,
}

public record WinLine(WinLineType Type, int Index, IReadOnlyList<int> Positions);

/// <summary>
/// Pure win-detection logic for a 5x5 bingo board addressed by row-major positions 0-24.
/// </summary>
public static class BingoWinChecker
{
    public const int GridSize = 5;
    public const int SquareCount = GridSize * GridSize;

    public static readonly IReadOnlyList<WinLine> Lines = BuildLines();

    /// <summary>
    /// Returns the first completed line found given a 25-element marked-state array
    /// (indexed by board position), or null if no line is complete.
    /// </summary>
    public static WinLine? FindWinningLine(IReadOnlyList<bool> isMarked)
    {
        if (isMarked.Count != SquareCount)
        {
            throw new ArgumentException($"Expected {SquareCount} squares, got {isMarked.Count}.", nameof(isMarked));
        }

        foreach (var line in Lines)
        {
            if (line.Positions.All(p => isMarked[p]))
            {
                return line;
            }
        }

        return null;
    }

    private static List<WinLine> BuildLines()
    {
        var lines = new List<WinLine>();

        for (var row = 0; row < GridSize; row++)
        {
            var positions = Enumerable.Range(0, GridSize).Select(col => row * GridSize + col).ToArray();
            lines.Add(new WinLine(WinLineType.Row, row, positions));
        }

        for (var col = 0; col < GridSize; col++)
        {
            var positions = Enumerable.Range(0, GridSize).Select(row => row * GridSize + col).ToArray();
            lines.Add(new WinLine(WinLineType.Column, col, positions));
        }

        var diagonal1 = Enumerable.Range(0, GridSize).Select(i => i * GridSize + i).ToArray();
        lines.Add(new WinLine(WinLineType.Diagonal, 0, diagonal1));

        var diagonal2 = Enumerable.Range(0, GridSize).Select(i => i * GridSize + (GridSize - 1 - i)).ToArray();
        lines.Add(new WinLine(WinLineType.Diagonal, 1, diagonal2));

        return lines;
    }
}
