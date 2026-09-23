using Surbibor.Domain.Logic;

namespace Surbibor.Api.Tests.Unit;

public class BingoWinCheckerTests
{
    [Fact]
    public void NoWin_WhenNoLineComplete()
    {
        var marked = new bool[BingoWinChecker.SquareCount];
        marked[12] = true; // free space only

        var result = BingoWinChecker.FindWinningLine(marked);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Wins_WhenFullRowMarked(int row)
    {
        var marked = new bool[BingoWinChecker.SquareCount];
        for (var col = 0; col < 5; col++)
        {
            marked[row * 5 + col] = true;
        }

        var result = BingoWinChecker.FindWinningLine(marked);

        Assert.NotNull(result);
        Assert.Equal(WinLineType.Row, result!.Type);
        Assert.Equal(row, result.Index);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Wins_WhenFullColumnMarked(int col)
    {
        var marked = new bool[BingoWinChecker.SquareCount];
        for (var row = 0; row < 5; row++)
        {
            marked[row * 5 + col] = true;
        }

        var result = BingoWinChecker.FindWinningLine(marked);

        Assert.NotNull(result);
        Assert.Equal(WinLineType.Column, result!.Type);
        Assert.Equal(col, result.Index);
    }

    [Fact]
    public void Wins_OnTopLeftToBottomRightDiagonal()
    {
        var marked = new bool[BingoWinChecker.SquareCount];
        for (var i = 0; i < 5; i++)
        {
            marked[i * 5 + i] = true;
        }

        var result = BingoWinChecker.FindWinningLine(marked);

        Assert.NotNull(result);
        Assert.Equal(WinLineType.Diagonal, result!.Type);
        Assert.Equal(0, result.Index);
    }

    [Fact]
    public void Wins_OnTopRightToBottomLeftDiagonal()
    {
        var marked = new bool[BingoWinChecker.SquareCount];
        for (var i = 0; i < 5; i++)
        {
            marked[i * 5 + (4 - i)] = true;
        }

        var result = BingoWinChecker.FindWinningLine(marked);

        Assert.NotNull(result);
        Assert.Equal(WinLineType.Diagonal, result!.Type);
        Assert.Equal(1, result.Index);
    }

    [Fact]
    public void FreeSpaceCounts_TowardDiagonalWin()
    {
        var marked = new bool[BingoWinChecker.SquareCount];
        marked[12] = true; // free space is part of both diagonals
        marked[0] = true;
        marked[6] = true;
        marked[18] = true;
        marked[24] = true;

        var result = BingoWinChecker.FindWinningLine(marked);

        Assert.NotNull(result);
        Assert.Equal(WinLineType.Diagonal, result!.Type);
    }

    [Fact]
    public void Throws_WhenWrongSquareCount()
    {
        var marked = new bool[10];

        Assert.Throws<ArgumentException>(() => BingoWinChecker.FindWinningLine(marked));
    }

    [Fact]
    public void ExactlyTwelveLinesExist()
    {
        Assert.Equal(12, BingoWinChecker.Lines.Count);
    }
}
