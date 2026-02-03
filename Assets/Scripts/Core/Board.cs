using UnityEngine;
using System.Collections.Generic;

public enum DeadlockFix
{
    None,
    ShuffleBoard,
    BoardReset
}

public struct SpawnInfo
{
    public int row;
    public int col;
    public int color;
    public int spawnRow;
}

public class Board
{
    public int rows;
    public int columns;
    public int colorCount;
    public int minGroupSize;

    private int[] grid;
    private int[] groupSizes;
    private int[] groupRoot;
    private int[] visited;
    private int visitId;

    private int[] queue;
    private int[] groupBuffer;
    private int[] colorBag;
    private bool[] dirtyColumns;
    private int[] colorCounts;

    private int cellCount;

    private SpawnInfo[] refillSpawns;
    private int refillSpawnCount;

    private int consecutiveDeadlocks = 0;

    private static readonly int[] dirRows = { 1, -1, 0, 0 };
    private static readonly int[] dirCols = { 0, 0, 1, -1 };

    private int[] colorQueue;

    private int shuffleSeed = 0;
    private int[] shuffleTemp;

    private int refillCounter = 0;
    private bool lastRefillNeededShuffle = false;

    private int aliveCount = 0;

    private void ClampAliveCount()
    {
        if (aliveCount < 0) aliveCount = 0;
        if (aliveCount > cellCount) aliveCount = cellCount;
    }

    public Board(int rows, int columns, int colorCount)
    {
        this.rows = Mathf.Max(2, rows);
        this.columns = Mathf.Max(2, columns);
        this.colorCount = Mathf.Clamp(colorCount, 1, 6);
        this.minGroupSize = 2;

        cellCount = this.rows * this.columns;

        grid = new int[cellCount];
        groupSizes = new int[cellCount];
        groupRoot = new int[cellCount];
        visited = new int[cellCount];
        queue = new int[cellCount];
        groupBuffer = new int[cellCount];
        colorBag = new int[cellCount];

        int shuffleTempSize = Mathf.Max(cellCount, this.colorCount * 2) + cellCount;
        shuffleTemp = new int[shuffleTempSize];

        refillSpawns = new SpawnInfo[cellCount];
        dirtyColumns = new bool[this.columns];
        colorCounts = new int[this.colorCount];

        colorQueue = new int[this.colorCount];
        for (int i = 0; i < this.colorCount; i++)
            colorQueue[i] = i;

        for (int i = this.colorCount - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = colorQueue[i];
            colorQueue[i] = colorQueue[j];
            colorQueue[j] = temp;
        }

        visitId = 1;
        aliveCount = 0;

        for (int i = 0; i < cellCount; i++)
        {
            grid[i] = -1;
            groupSizes[i] = 0;
            groupRoot[i] = -1;
            visited[i] = 0;
        }
    }

    private int Idx(int r, int c)
    {
        return r * columns + c;
    }

    private int GetRow(int idx)
    {
        return idx / columns;
    }

    private int GetCol(int idx)
    {
        return idx % columns;
    }
    private bool InBounds(int r, int c)
    {
        if (r < 0) return false;
        if (r >= rows) return false;
        if (c < 0) return false;
        if (c >= columns) return false;
        return true;
    }

    public void FillBoard()
    {
        for (int i = 0; i < cellCount; i++)
        {
            grid[i] = UnityEngine.Random.Range(0, colorCount);
        }
        aliveCount = cellCount;

        MarkAllColumnsDirty();
        bool hasValidMove = RecomputeGroupsAndCheckMove();

        if (!hasValidMove)
        {
            if (GuaranteeMoveBySwapsOnly())
            {
                RecomputeGroupsAndCheckMove();
            }
            else
            {
                DeterministicPermutation();

                if (!RecomputeGroupsAndCheckMove())
                {
                    GuaranteeMoveBySwapsOnly();
                    RecomputeGroupsAndCheckMove();
                }
            }
        }

        consecutiveDeadlocks = 0;
    }

    private void DeterministicPermutation()
    {
        int alive = 0;
        for (int i = 0; i < cellCount; i++)
        {
            if (grid[i] != -1)
            {
                queue[alive] = i;
                colorBag[alive] = grid[i];
                alive++;
            }
        }

        if (alive < 2)
            return;

        shuffleSeed++;
        int offset = (shuffleSeed * 7) % alive;
        if (offset == 0) offset = 1;

        for (int i = 0; i < alive; i++)
        {
            shuffleTemp[i] = colorBag[(i + offset) % alive];
        }

        for (int i = 0; i < alive; i++)
        {
            int pos = queue[i];
            grid[pos] = shuffleTemp[i];
        }

        MarkAllColumnsDirty();
    }

    private bool GuaranteeMoveBySwapsOnly()
    {
        for (int i = 0; i < colorCount; i++)
            colorCounts[i] = 0;

        int alive = 0;
        for (int i = 0; i < cellCount; i++)
        {
            if (grid[i] != -1)
            {
                int clr = grid[i];
                if (clr >= 0 && clr < colorCount)
                    colorCounts[clr]++;
                alive++;
            }
        }

        if (alive < 2)
            return false;

        for (int chosenColor = 0; chosenColor < colorCount; chosenColor++)
        {
            if (colorCounts[chosenColor] < 2)
                continue;

            if (TryGuaranteeWithColor(chosenColor))
            {
                MarkAllColumnsDirty();
                return true;
            }
        }

        return false;
    }

    private bool TryGuaranteeWithColor(int chosenColor)
    {
        int sameColorCount = 0;
        for (int i = 0; i < cellCount; i++)
        {
            if (grid[i] == chosenColor)
            {
                queue[sameColorCount++] = i;
            }
        }

        if (sameColorCount < 2)
            return false;

        for (int i = 0; i < sameColorCount; i++)
        {
            int posA = queue[i];
            int rA = GetRow(posA);
            int cA = GetCol(posA);

            if (InBounds(rA + 1, cA) && grid[Idx(rA + 1, cA)] == chosenColor) return true;
            if (InBounds(rA - 1, cA) && grid[Idx(rA - 1, cA)] == chosenColor) return true;
            if (InBounds(rA, cA + 1) && grid[Idx(rA, cA + 1)] == chosenColor) return true;
            if (InBounds(rA, cA - 1) && grid[Idx(rA, cA - 1)] == chosenColor) return true;
        }

        int posBlockA = -1;
        int posBlockB = -1;
        int neighborIdx = -1;

        for (int i = 0; i < sameColorCount; i++)
        {
            int candidateA = queue[i];
            int rA = GetRow(candidateA);
            int cA = GetCol(candidateA);

            int foundNeighbor = FindValidNeighbor(rA, cA, chosenColor);

            if (foundNeighbor != -1)
            {
                posBlockA = candidateA;
                neighborIdx = foundNeighbor;

                for (int j = 0; j < sameColorCount; j++)
                {
                    if (queue[j] != posBlockA)
                    {
                        posBlockB = queue[j];
                        break;
                    }
                }
                break;
            }
        }

        if (posBlockA == -1 || posBlockB == -1 || neighborIdx == -1)
            return false;

        SwapGridIndices(posBlockB, neighborIdx);
        return true;
    }

    private int FindValidNeighbor(int row, int col, int excludeColor)
    {
        for (int d = 0; d < 4; d++)
        {
            int nr = row + dirRows[d];
            int nc = col + dirCols[d];

            if (!InBounds(nr, nc))
                continue;

            int nIdx = Idx(nr, nc);

            if (grid[nIdx] != -1 && grid[nIdx] != excludeColor)
            {
                return nIdx;
            }
        }

        return -1;
    }

    private void SwapGridIndices(int a, int b)
    {
        int tmp = grid[a];
        grid[a] = grid[b];
        grid[b] = tmp;

        MarkColumnDirty(GetCol(a));
        MarkColumnDirty(GetCol(b));
    }

    public bool ShuffleBoard()
    {
        ApplyGravityAll();

        if (aliveCount < minGroupSize)
        {
            return false;
        }

        if (RecomputeGroupsAndCheckMove())
        {
            consecutiveDeadlocks = 0;
            return true;
        }

        consecutiveDeadlocks++;

        if (GuaranteeMoveBySwapsOnly())
        {
            RecomputeGroupsAndCheckMove();
            consecutiveDeadlocks = 0;
            return true;
        }

        DeterministicPermutation();

        if (RecomputeGroupsAndCheckMove())
        {
            consecutiveDeadlocks = 0;
            return true;
        }

        if (GuaranteeMoveBySwapsOnly())
        {
            RecomputeGroupsAndCheckMove();
            consecutiveDeadlocks = 0;
            return true;
        }

        consecutiveDeadlocks = 0;
        return false;
    }

    private void HardResetDeterministic()
    {
        for (int i = 0; i < cellCount; i++)
        {
            int r = GetRow(i);
            int c = GetCol(i);
            grid[i] = (r + c * 3 + shuffleSeed) % colorCount;
        }

        shuffleSeed++;
        aliveCount = cellCount;
        MarkAllColumnsDirty();

        if (!RecomputeGroupsAndCheckMove())
        {
            if (GuaranteeMoveBySwapsOnly())
            {
                RecomputeGroupsAndCheckMove();
            }
            else
            {
                DeterministicPermutation();

                if (!RecomputeGroupsAndCheckMove())
                {
                    GuaranteeMoveBySwapsOnly();
                    RecomputeGroupsAndCheckMove();
                }
            }
        }

        consecutiveDeadlocks = 0;
    }

    public DeadlockFix EnsureValidMoveExists()
    {
        bool hasMove = RecomputeGroupsAndCheckMove();
        if (hasMove)
        {
            lastRefillNeededShuffle = false;
            return DeadlockFix.None;
        }

        if (ShuffleBoard())
        {
            lastRefillNeededShuffle = false;
            return DeadlockFix.ShuffleBoard;
        }

        HardResetDeterministic();
        lastRefillNeededShuffle = false;
        return DeadlockFix.BoardReset;
    }

    public int RefillEmptyCellsWithInfo(SpawnInfo[] outBuffer, int bufferSize)
    {
        refillSpawnCount = 0;
        lastRefillNeededShuffle = false;

        refillCounter++;
        RotateColorQueueDeterministic();

        int totalSpawned = 0;

        for (int c = 0; c < columns; c++)
        {
            int spawnCountInCol = 0;

            int colOffset = (c * 7 + refillCounter * 3) % colorCount;

            for (int r = 0; r < rows; r++)
            {
                int idx = Idx(r, c);
                if (grid[idx] != -1)
                    continue;

                int colorIdx = (spawnCountInCol + colOffset) % colorCount;
                int chosenColor = colorQueue[colorIdx];

                grid[idx] = chosenColor;
                MarkColumnDirty(c);

                if (refillSpawnCount < bufferSize)
                {
                    outBuffer[refillSpawnCount].row = r;
                    outBuffer[refillSpawnCount].col = c;
                    outBuffer[refillSpawnCount].color = chosenColor;
                    outBuffer[refillSpawnCount].spawnRow = rows + 2;
                    refillSpawnCount++;
                }

                spawnCountInCol++;
                totalSpawned++;
            }
        }

        aliveCount += totalSpawned;
        ClampAliveCount();

        bool hasMove = RecomputeGroupsAndCheckMove();
        lastRefillNeededShuffle = !hasMove;

        return refillSpawnCount;
    }

    public int RefillEmptyCells()
    {
        return RefillEmptyCellsWithInfo(refillSpawns, cellCount);
    }

    public SpawnInfo GetSpawnInfo(int index)
    {
        return refillSpawns[index];
    }

    public int GetSpawnCount()
    {
        return refillSpawnCount;
    }

    private void RotateColorQueueDeterministic()
    {
        if (colorCount <= 1) return;

        int rotateBy = ((refillCounter * 3) + (shuffleSeed * 5)) % colorCount;

        if (rotateBy == 0) rotateBy = 1;

        for (int i = 0; i < colorCount; i++)
        {
            shuffleTemp[i] = colorQueue[(i + rotateBy) % colorCount];
        }
        for (int i = 0; i < colorCount; i++)
        {
            colorQueue[i] = shuffleTemp[i];
        }

        if (colorCount >= 2)
        {
            int swapA = refillCounter % colorCount;
            int swapB = (refillCounter + 1) % colorCount;
            if (swapA != swapB)
            {
                int tmp = colorQueue[swapA];
                colorQueue[swapA] = colorQueue[swapB];
                colorQueue[swapB] = tmp;
            }
        }
    }

    public bool DidLastRefillNeedShuffle()
    {
        return lastRefillNeededShuffle;
    }

    public bool IsEmpty(int r, int c)
    {
        if (!InBounds(r, c)) return true;
        return grid[Idx(r, c)] == -1;
    }

    public int GetColor(int r, int c)
    {
        if (!InBounds(r, c)) return -1;
        return grid[Idx(r, c)];
    }

    public int GetGroupSize(int r, int c)
    {
        if (!InBounds(r, c)) return 0;
        return groupSizes[Idx(r, c)];
    }

    public void SetBlock(int r, int c, int color)
    {
        if (!InBounds(r, c)) return;

        if (color < 0)
        {
            SetEmpty(r, c);
            return;
        }

        int idx = Idx(r, c);
        bool wasEmpty = (grid[idx] == -1);
        grid[idx] = color;
        if (wasEmpty) aliveCount++;
        MarkColumnDirty(c);
    }

    public void SetEmpty(int r, int c)
    {
        if (!InBounds(r, c)) return;
        int idx = Idx(r, c);
        bool wasAlive = (grid[idx] != -1);
        grid[idx] = -1;
        groupSizes[idx] = 0;
        groupRoot[idx] = -1;
        if (wasAlive) aliveCount--;
        MarkColumnDirty(c);
    }

    private void MarkColumnDirty(int c)
    {
        if (c >= 0 && c < columns)
            dirtyColumns[c] = true;
    }

    private void MarkAllColumnsDirty()
    {
        for (int c = 0; c < columns; c++)
            dirtyColumns[c] = true;
    }

    public bool IsColumnDirty(int c)
    {
        if (c < 0 || c >= columns) return false;
        return dirtyColumns[c];
    }

    private void ClearDirtyFlags()
    {
        for (int c = 0; c < columns; c++)
            dirtyColumns[c] = false;
    }

    private bool HasDirtyColumns()
    {
        for (int c = 0; c < columns; c++)
            if (dirtyColumns[c]) return true;
        return false;
    }

    private void ExpandDirtyToNeighbors()
    {
        int dirtyCount = 0;
        for (int c = 0; c < columns; c++)
        {
            if (dirtyColumns[c])
                groupBuffer[dirtyCount++] = c;
        }

        for (int i = 0; i < dirtyCount; i++)
        {
            int c = groupBuffer[i];
            if (c > 0) dirtyColumns[c - 1] = true;
            if (c < columns - 1) dirtyColumns[c + 1] = true;
        }
    }

    public bool RecomputeGroupsAndCheckMove()
    {
        bool hasDirty = HasDirtyColumns();

        if (!hasDirty)
        {
            for (int i = 0; i < cellCount; i++)
            {
                if (groupSizes[i] >= minGroupSize)
                    return true;
            }
            return false;
        }

        ExpandDirtyToNeighbors();

        for (int c = 0; c < columns; c++)
        {
            if (!dirtyColumns[c]) continue;

            for (int r = 0; r < rows; r++)
            {
                int idx = Idx(r, c);
                groupSizes[idx] = 0;
                groupRoot[idx] = -1;
            }
        }

        IncrementStamp();

        bool hasValidMove = false;

        for (int c = 0; c < columns; c++)
        {
            if (!dirtyColumns[c]) continue;

            for (int r = 0; r < rows; r++)
            {
                int i = Idx(r, c);
                int color = grid[i];
                if (color == -1) continue;
                if (visited[i] == visitId) continue;

                int groupCount = FloodFillBFS(i, color);

                int root = groupBuffer[0];
                for (int k = 1; k < groupCount; k++)
                {
                    if (groupBuffer[k] < root) root = groupBuffer[k];
                }

                for (int k = 0; k < groupCount; k++)
                {
                    int cell = groupBuffer[k];
                    groupSizes[cell] = groupCount;
                    groupRoot[cell] = root;
                }

                if (!hasValidMove && groupCount >= minGroupSize)
                    hasValidMove = true;
            }
        }

        ClearDirtyFlags();
        return hasValidMove;
    }

    private int FloodFillBFS(int startIdx, int targetColor)
    {
        int head = 0;
        int tail = 0;
        int groupCount = 0;

        queue[tail++] = startIdx;
        visited[startIdx] = visitId;

        while (head < tail)
        {
            int idx = queue[head++];
            groupBuffer[groupCount++] = idx;

            int r = GetRow(idx);
            int c = GetCol(idx);

            TryEnqueueNeighbor(r + 1, c, targetColor, ref tail);
            TryEnqueueNeighbor(r - 1, c, targetColor, ref tail);
            TryEnqueueNeighbor(r, c + 1, targetColor, ref tail);
            TryEnqueueNeighbor(r, c - 1, targetColor, ref tail);
        }

        return groupCount;
    }

    private void TryEnqueueNeighbor(int r, int c, int targetColor, ref int tail)
    {
        if (!InBounds(r, c)) return;

        int idx = Idx(r, c);
        if (visited[idx] == visitId) return;
        if (grid[idx] != targetColor) return;

        visited[idx] = visitId;
        queue[tail++] = idx;
    }

    private void IncrementStamp()
    {
        visitId++;
        if (visitId == int.MaxValue)
        {
            for (int i = 0; i < cellCount; i++)
                visited[i] = 0;
            visitId = 1;
        }
    }

    public int RemoveGroupAt(int r, int c, List<Vector2Int> removedOut)
    {
        removedOut.Clear();

        if (!InBounds(r, c)) return 0;

        int idx = Idx(r, c);
        int color = grid[idx];
        if (color == -1) return 0;

        if (groupSizes[idx] < minGroupSize) return 0;

        IncrementStamp();
        int groupCount = FloodFillBFS(idx, color);

        if (groupCount < minGroupSize) return 0;

        for (int k = 0; k < groupCount; k++)
        {
            int gi = groupBuffer[k];
            int gr = GetRow(gi);
            int gc = GetCol(gi);

            tempVec.x = gc;
            tempVec.y = gr;
            removedOut.Add(tempVec);
            grid[gi] = -1;
            groupSizes[gi] = 0;
            groupRoot[gi] = -1;
            MarkColumnDirty(gc);
        }
        aliveCount -= groupCount;
        ClampAliveCount();

        return groupCount;
    }

    public void ApplyGravityAll()
    {
        for (int c = 0; c < columns; c++)
            ApplyGravityColumnInternal(c);
    }

    private void ApplyGravityColumnInternal(int c)
    {
        int writeRow = 0;
        bool moved = false;

        for (int r = 0; r < rows; r++)
        {
            int idx = Idx(r, c);
            int color = grid[idx];
            if (color == -1) continue;

            if (writeRow != r)
            {
                grid[Idx(writeRow, c)] = color;
                grid[idx] = -1;
                moved = true;
            }
            writeRow++;
        }

        if (moved)
        {
            MarkColumnDirty(c);
            if (c > 0) MarkColumnDirty(c - 1);
            if (c < columns - 1) MarkColumnDirty(c + 1);
        }
    }

    public int GetIconTier(int r, int c, int thresholdA, int thresholdB, int thresholdC)
    {
        int size = GetGroupSize(r, c);

        if (size > thresholdC) return 3;
        if (size > thresholdB) return 2;
        if (size > thresholdA) return 1;
        return 0;
    }

    public int GetIconTier(int r, int c, int[] thresholds)
    {
        if (thresholds == null || thresholds.Length < 3)
            return 0;

        return GetIconTier(r, c, thresholds[0], thresholds[1], thresholds[2]);
    }


    public int GetConsecutiveDeadlocks()
    {
        return consecutiveDeadlocks;
    }

    public void ResetDeadlockCounter()
    {
        consecutiveDeadlocks = 0;
    }


    private Vector2Int tempVec = new Vector2Int();

    public void GetLargestGroup(List<Vector2Int> outList)
    {
        outList.Clear();

        int largestSize = 0;
        int largestRoot = -1;

        for (int i = 0; i < cellCount; i++)
        {
            if (grid[i] == -1) continue;
            if (groupSizes[i] >= minGroupSize && groupSizes[i] > largestSize)
            {
                largestSize = groupSizes[i];
                largestRoot = groupRoot[i];
            }
        }

        if (largestRoot == -1 || largestSize < minGroupSize)
            return;

        for (int i = 0; i < cellCount; i++)
        {
            if (groupRoot[i] == largestRoot && grid[i] != -1)
            {
                tempVec.x = GetCol(i);
                tempVec.y = GetRow(i);
                outList.Add(tempVec);
            }
        }
    }

}