using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Edit-mode unit tests for Board.cs.
/// Board has no MonoBehaviour or scene dependencies — runs entirely in the test runner.
/// </summary>
[TestFixture]
public class BoardTests
{
    // ────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────────

    private Board MakeBoard(int rows = 9, int cols = 9, int colors = 6)
    {
        var b = new Board(rows, cols, colors);
        b.FillBoard();
        return b;
    }

    private (int row, int col) FindValidGroup(Board board)
    {
        for (int r = 0; r < board.rows; r++)
            for (int c = 0; c < board.columns; c++)
                if (board.GetGroupSize(r, c) >= board.minGroupSize)
                    return (r, c);
        return (-1, -1);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Initialization
    // ────────────────────────────────────────────────────────────────────────────

    [Test]
    public void FillBoard_EveryCell_HasValidColor()
    {
        var board = MakeBoard();
        for (int r = 0; r < board.rows; r++)
            for (int c = 0; c < board.columns; c++)
            {
                int color = board.GetColor(r, c);
                Assert.IsFalse(board.IsEmpty(r, c), $"Cell ({r},{c}) is empty after FillBoard");
                Assert.GreaterOrEqual(color, 0,               $"Cell ({r},{c}) has negative color");
                Assert.Less(color, board.colorCount,          $"Cell ({r},{c}) color out of range");
            }
    }

    [Test]
    public void FillBoard_AlwaysProducesValidMove()
    {
        // Run multiple times to catch edge-case seeds
        for (int i = 0; i < 20; i++)
        {
            var board = new Board(9, 9, 6);
            board.FillBoard();
            Assert.IsTrue(board.RecomputeGroupsAndCheckMove(),
                $"FillBoard produced a deadlocked state on iteration {i}");
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Group detection (BFS / stamp)
    // ────────────────────────────────────────────────────────────────────────────

    [Test]
    public void GroupSize_TwoAdjacentSameColor_IsAtLeastTwo()
    {
        // Build a minimal 2×1 board with a guaranteed adjacent pair
        var board = new Board(2, 1, 2);
        board.SetBlock(0, 0, 0);
        board.SetBlock(1, 0, 0);
        board.RecomputeGroupsAndCheckMove();

        Assert.GreaterOrEqual(board.GetGroupSize(0, 0), 2);
        Assert.GreaterOrEqual(board.GetGroupSize(1, 0), 2);
    }

    [Test]
    public void GroupSize_DifferentColors_IsOne()
    {
        var board = new Board(2, 1, 2);
        board.SetBlock(0, 0, 0);
        board.SetBlock(1, 0, 1);   // different colour → no group
        board.RecomputeGroupsAndCheckMove();

        Assert.Less(board.GetGroupSize(0, 0), board.minGroupSize);
        Assert.Less(board.GetGroupSize(1, 0), board.minGroupSize);
    }

    [Test]
    public void StampBFS_RepeatedSearches_DoNotCollide()
    {
        // Stamp increments each BFS run — repeated searches must stay independent.
        var board = new Board(5, 5, 3);
        board.FillBoard();

        // Run RecomputeGroupsAndCheckMove many times; group sizes must stay consistent.
        bool first = board.RecomputeGroupsAndCheckMove();
        for (int i = 0; i < 50; i++)
        {
            bool subsequent = board.RecomputeGroupsAndCheckMove();
            Assert.AreEqual(first, subsequent, $"RecomputeGroupsAndCheckMove result changed on run {i}");
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // RemoveGroupAt
    // ────────────────────────────────────────────────────────────────────────────

    [Test]
    public void RemoveGroupAt_ValidGroup_ReturnsPositiveCount()
    {
        var board = MakeBoard();
        var removed = new List<Vector2Int>();
        var (r, c) = FindValidGroup(board);

        Assert.AreNotEqual(-1, r, "No valid group found — test precondition failed");
        int count = board.RemoveGroupAt(r, c, removed);
        Assert.Greater(count, 0);
        Assert.AreEqual(count, removed.Count);
    }

    [Test]
    public void RemoveGroupAt_RemovedCells_BecomeEmpty()
    {
        var board = MakeBoard();
        var removed = new List<Vector2Int>();
        var (r, c) = FindValidGroup(board);
        Assert.AreNotEqual(-1, r);

        board.RemoveGroupAt(r, c, removed);

        foreach (var pos in removed)
            Assert.IsTrue(board.IsEmpty(pos.y, pos.x),
                $"Cell ({pos.y},{pos.x}) should be empty after removal");
    }

    [Test]
    public void RemoveGroupAt_SingleIsolatedBlock_ReturnsZero()
    {
        // A 1×1 board can never form a group of size >= 2
        var board = new Board(1, 1, 1);
        board.SetBlock(0, 0, 0);
        board.RecomputeGroupsAndCheckMove();

        var removed = new List<Vector2Int>();
        int count = board.RemoveGroupAt(0, 0, removed);
        Assert.AreEqual(0, count);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Gravity & GravityMove recording
    // ────────────────────────────────────────────────────────────────────────────

    [Test]
    public void ApplyGravity_BlockFallsToBottom_WhenCellBelowIsEmpty()
    {
        // Place a block at top row (r=1), leave bottom (r=0) empty.
        var board = new Board(2, 1, 2);
        board.SetBlock(1, 0, 0);   // top
        // row 0 stays empty
        board.ApplyGravityAll();

        Assert.IsFalse(board.IsEmpty(0, 0), "Block should have fallen to row 0");
        Assert.IsTrue(board.IsEmpty(1, 0),  "Row 1 should be empty after gravity");
    }

    [Test]
    public void ApplyGravity_RecordsCorrectFromAndToRow()
    {
        var board = new Board(3, 1, 2);
        board.SetBlock(2, 0, 0);   // top — will fall to 0
        // rows 0, 1 empty
        board.ApplyGravityAll();

        Assert.AreEqual(1, board.GetGravityMoveCount(), "Expected exactly one gravity move");
        var move = board.GetGravityMove(0);
        Assert.AreEqual(2, move.fromRow);
        Assert.AreEqual(0, move.toRow);
        Assert.AreEqual(0, move.col);
    }

    [Test]
    public void ApplyGravity_NoEmptyCells_RecordsNoMoves()
    {
        var board = new Board(2, 1, 2);
        board.SetBlock(0, 0, 0);
        board.SetBlock(1, 0, 1);
        board.ApplyGravityAll();

        Assert.AreEqual(0, board.GetGravityMoveCount());
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Dirty-column tracking
    // ────────────────────────────────────────────────────────────────────────────

    [Test]
    public void SetEmpty_MarksColumnDirty()
    {
        var board = new Board(3, 3, 2);
        board.FillBoard();
        board.RecomputeGroupsAndCheckMove(); // clears dirty flags

        board.SetEmpty(1, 1);
        Assert.IsTrue(board.IsColumnDirty(1), "Column 1 should be dirty after SetEmpty");
    }

    [Test]
    public void SetBlock_MarksColumnDirty()
    {
        var board = new Board(3, 3, 2);
        board.FillBoard();
        board.RecomputeGroupsAndCheckMove();

        board.SetBlock(0, 2, 0);
        Assert.IsTrue(board.IsColumnDirty(2), "Column 2 should be dirty after SetBlock");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Deadlock resolution
    // ────────────────────────────────────────────────────────────────────────────

    [Test]
    public void EnsureValidMoveExists_AfterAnyState_BoardIsPlayable()
    {
        for (int i = 0; i < 10; i++)
        {
            var board = new Board(9, 9, 6);
            board.FillBoard();

            // Blast every valid group until the board might deadlock
            var removed = new List<Vector2Int>();
            for (int attempt = 0; attempt < 200; attempt++)
            {
                board.RecomputeGroupsAndCheckMove();
                var (r, c) = FindValidGroup(board);
                if (r == -1) break;
                board.RemoveGroupAt(r, c, removed);
                board.ApplyGravityAll();
            }

            board.EnsureValidMoveExists();
            Assert.IsTrue(board.RecomputeGroupsAndCheckMove(),
                $"Board should always be playable after EnsureValidMoveExists (seed {i})");
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Hint: GetLargestGroup
    // ────────────────────────────────────────────────────────────────────────────

    [Test]
    public void GetLargestGroup_ReturnsNonEmptyList_WhenValidMoveExists()
    {
        var board = MakeBoard();
        var list = new List<Vector2Int>();
        board.GetLargestGroup(list);
        Assert.Greater(list.Count, 0, "GetLargestGroup should return at least one cell");
    }

    [Test]
    public void GetLargestGroup_AllCellsSameColor_GroupEqualsBoard()
    {
        int rows = 3, cols = 3;
        var board = new Board(rows, cols, 2);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                board.SetBlock(r, c, 0);   // all same colour
        board.RecomputeGroupsAndCheckMove();

        var list = new List<Vector2Int>();
        board.GetLargestGroup(list);
        Assert.AreEqual(rows * cols, list.Count,
            "Largest group should span the entire board when all cells are the same colour");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Icon tier
    // ────────────────────────────────────────────────────────────────────────────

    [Test]
    public void GetIconTier_SmallGroup_ReturnsTierZero()
    {
        var board = new Board(2, 1, 2);
        board.SetBlock(0, 0, 0);
        board.SetBlock(1, 0, 0);
        board.RecomputeGroupsAndCheckMove();

        // thresholds A=4, B=7, C=9 → group size 2 → tier 0
        int tier = board.GetIconTier(0, 0, 4, 7, 9);
        Assert.AreEqual(0, tier);
    }

    [Test]
    public void GetIconTier_LargeGroup_ReturnsTierThree()
    {
        int size = 10;
        var board = new Board(size, 1, 2);
        for (int r = 0; r < size; r++)
            board.SetBlock(r, 0, 0);
        board.RecomputeGroupsAndCheckMove();

        // thresholds A=4, B=7, C=9 → group size 10 → tier 3
        int tier = board.GetIconTier(0, 0, 4, 7, 9);
        Assert.AreEqual(3, tier);
    }
}
