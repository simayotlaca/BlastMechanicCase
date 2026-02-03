using System.Collections.Generic;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    public GameObject blockPrefab;
    public GameConfig config;

    private Board board;
    private BlockView[,] blocks;
    private BoardController controller;

    private const int MAX_POOL_SIZE = 150;

    private BlockView[] poolArray;
    private int poolCount = 0;

    private int activeBlastCount = 0;
    private int activeMoveCount = 0;

    public bool isAnimating = false;

    private int cachedThresholdA;
    private int cachedThresholdB;
    private int cachedThresholdC;
    private float cachedCellSizeX;
    private float cachedCellSizeY;
    private float cachedOffsetX;
    private float cachedOffsetY;

    private Vector3 tempWorldPos = new Vector3();

    private WaitForSeconds shuffleWait;

    public void Setup(Board board)
    {
        this.board = board;
        this.controller = GetComponent<BoardController>();

        if (controller != null)
        {
            blockPrefab = controller.blockPrefab;
            config = controller.config;
        }

        if (config == null)
            return;

        config.GetSafeThresholds(out cachedThresholdA, out cachedThresholdB, out cachedThresholdC);

        cachedCellSizeX = config.blockSize + config.blockSpacing;
        cachedCellSizeY = config.blockSize + config.verticalSpacing;
        float totalWidth = board.columns * cachedCellSizeX;
        float totalHeight = board.rows * cachedCellSizeY;
        cachedOffsetX = -totalWidth / 2f + config.blockSize / 2f;
        cachedOffsetY = -totalHeight / 2f + config.blockSize / 2f;

        if (blocks != null)
        {
            for (int r = 0; r < blocks.GetLength(0); r++)
            {
                for (int c = 0; c < blocks.GetLength(1); c++)
                {
                    if (blocks[r, c] != null)
                    {
                        HideBlock(blocks[r, c]);
                    }
                }
            }
        }

        blocks = new BlockView[board.rows, board.columns];

        if (poolArray == null)
        {
            poolArray = new BlockView[MAX_POOL_SIZE];
            poolCount = 0;
        }

        if (shuffleWait == null)
        {
            shuffleWait = new WaitForSeconds(0.1f);
        }

        activeBlastCount = 0;
        activeMoveCount = 0;

        WarmupPool(board.rows * board.columns);

        isAnimating = false;
    }

    void WarmupPool(int targetCount)
    {
        if (poolCount >= targetCount) return;

        int needed = Mathf.Min(targetCount, MAX_POOL_SIZE) - poolCount;

        for (int i = 0; i < needed; i++)
        {
            if (blockPrefab == null) break;
            if (poolCount >= MAX_POOL_SIZE) break;

            GameObject obj = Instantiate(blockPrefab, transform);
            BlockView block = obj.GetComponent<BlockView>();
            if (block == null)
                block = obj.AddComponent<BlockView>();

            block.gameObject.SetActive(false);
            poolArray[poolCount++] = block;
        }
    }

    public void CreateAllBlocks()
    {
        for (int r = 0; r < board.rows; r++)
        {
            for (int c = 0; c < board.columns; c++)
            {
                if (!board.IsEmpty(r, c))
                {
                    CreateBlock(r, c, board.GetColor(r, c));
                }
            }
        }
        UpdateAllIcons();
    }

    void CreateBlock(int row, int col, int colorIndex)
    {
        BlockView block = GetBlock();
        if (block == null)
            return;

        block.transform.position = GridToWorld(row, col);
        block.Setup(row, col, colorIndex, config.blockSize);
        blocks[row, col] = block;

        SetBlockSprite(block, colorIndex, row, col);
    }

    void SetBlockSprite(BlockView block, int colorIndex, int row, int col)
    {
        if (config.colorDefinitions == null)
            return;
        if (colorIndex < 0 || colorIndex >= config.colorDefinitions.Length)
            return;

        ColorDefinition colorDef = config.colorDefinitions[colorIndex];
        if (colorDef == null)
            return;

        int tier = board.GetIconTier(row, col, cachedThresholdA, cachedThresholdB, cachedThresholdC);

        Sprite sprite = colorDef.defaultIcon;

        if (tier == 3)
        {
            if (colorDef.iconC != null)
            {
                sprite = colorDef.iconC;
            }
            else
            {
                sprite = colorDef.defaultIcon;
            }
        }
        else if (tier == 2)
        {
            if (colorDef.iconB != null)
            {
                sprite = colorDef.iconB;
            }
            else
            {
                sprite = colorDef.defaultIcon;
            }
        }
        else if (tier == 1)
        {
            if (colorDef.iconA != null)
            {
                sprite = colorDef.iconA;
            }
            else
            {
                sprite = colorDef.defaultIcon;
            }
        }

        block.SetSprite(sprite, config.blockSize);
    }

    public void UpdateAllIcons()
    {

        bool hasDirty = false;
        for (int c = 0; c < board.columns; c++)
        {
            if (board.IsColumnDirty(c))
            {
                hasDirty = true;
                break;
            }
        }

        if (!hasDirty)
        {
            UpdateAllIconsForced();
            return;
        }

        for (int c = 0; c < board.columns; c++)
        {
            if (!board.IsColumnDirty(c)) continue;

            for (int r = 0; r < board.rows; r++)
            {
                if (blocks[r, c] != null && !board.IsEmpty(r, c))
                {
                    int color = board.GetColor(r, c);
                    SetBlockSprite(blocks[r, c], color, r, c);
                }
            }
        }
    }

    private void UpdateAllIconsForced()
    {
        for (int r = 0; r < board.rows; r++)
        {
            for (int c = 0; c < board.columns; c++)
            {
                if (blocks[r, c] != null && !board.IsEmpty(r, c))
                {
                    int color = board.GetColor(r, c);
                    SetBlockSprite(blocks[r, c], color, r, c);
                }
            }
        }
    }

    public void BlastBlocks(List<Vector2Int> positions)
    {

        float centerX = 0f, centerY = 0f;
        int validCount = 0;

        for (int i = 0; i < positions.Count; i++)
        {
            centerX += positions[i].x;
            centerY += positions[i].y;
            validCount++;
        }

        if (validCount > 0)
        {
            centerX /= validCount;
            centerY /= validCount;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            int row = positions[i].y;
            int col = positions[i].x;

            BlockView block = blocks[row, col];
            if (block == null) continue;
            if (block.inBlastList) continue;

            float dist = Mathf.Sqrt((col - centerX) * (col - centerX) + (row - centerY) * (row - centerY));
            float delay = dist * 0.03f;

            block.inBlastList = true;
            block.OnBlastDone = OnBlockBlastDone;
            block.StartBlast(delay);
            activeBlastCount++;
            blocks[row, col] = null;
        }

        UpdateAnimatingState();
    }

    void OnBlockBlastDone(BlockView block)
    {
        if (!block.inBlastList) return;
        block.inBlastList = false;
        activeBlastCount--;
        HideBlock(block);
        UpdateAnimatingState();
    }

    public void MoveBlock(int fromRow, int fromCol, int toRow, int toCol)
    {
        if (blocks[fromRow, fromCol] == null)
            return;

        BlockView block = blocks[fromRow, fromCol];

        blocks[fromRow, fromCol] = null;
        blocks[toRow, toCol] = block;

        block.row = toRow;
        block.col = toCol;
        block.UpdateSortingOrder();
        block.OnMoveDone = OnBlockMoveDone;
        block.MoveTo(GridToWorld(toRow, toCol), config.fallDuration);

        if (!block.inMoveList)
        {
            block.inMoveList = true;
            activeMoveCount++;
        }

        UpdateAnimatingState();
    }

    void OnBlockMoveDone(BlockView block)
    {
        if (!block.inMoveList) return;
        block.inMoveList = false;
        activeMoveCount--;
        UpdateAnimatingState();
    }

    void UpdateAnimatingState()
    {
        isAnimating = (activeMoveCount > 0) || (activeBlastCount > 0);
    }

    public void SpawnBlock(int row, int col, int colorIndex, int spawnRow)
    {
        BlockView block = GetBlock();
        if (block == null) return;

        block.transform.position = GridToWorld(spawnRow, col);
        block.Setup(row, col, colorIndex, config.blockSize);

        blocks[row, col] = block;

        SetBlockSprite(block, colorIndex, row, col);

        block.OnMoveDone = OnBlockMoveDone;
        block.MoveTo(GridToWorld(row, col), config.fallDuration);

        if (!block.inMoveList)
        {
            block.inMoveList = true;
            activeMoveCount++;
        }

        UpdateAnimatingState();
    }

    public void AnimateSpawnsFromBoard(Board board)
    {
        int count = board.GetSpawnCount();
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            SpawnInfo info = board.GetSpawnInfo(i);
            SpawnBlock(info.row, info.col, info.color, info.spawnRow);
        }
    }

    public void RefreshAllBlocks()
    {
        for (int r = 0; r < board.rows; r++)
        {
            for (int c = 0; c < board.columns; c++)
            {
                if (blocks[r, c] != null)
                {
                    HideBlock(blocks[r, c]);
                    blocks[r, c] = null;
                }
            }
        }

        CreateAllBlocks();
    }

    Vector3 GridToWorld(int row, int col)
    {
        tempWorldPos.x = col * cachedCellSizeX + cachedOffsetX;
        tempWorldPos.y = row * cachedCellSizeY + cachedOffsetY;
        tempWorldPos.z = 0;
        return tempWorldPos;
    }

    public GridPos WorldToGrid(Vector3 worldPos)
    {
        GridPos result = new GridPos(-1, -1, false);

        if (board == null || config == null)
            return result;

        float gridStartX = cachedOffsetX - config.blockSize / 2f;
        float gridStartY = cachedOffsetY - config.blockSize / 2f;

        float relX = worldPos.x - gridStartX;
        float relY = worldPos.y - gridStartY;

        if (relX < 0 || relY < 0)
            return result;

        int col = Mathf.FloorToInt(relX / cachedCellSizeX);
        int row = Mathf.FloorToInt(relY / cachedCellSizeY);

        if (row < 0 || row >= board.rows || col < 0 || col >= board.columns)
            return result;

        result.row = row;
        result.col = col;
        result.isValid = true;
        return result;
    }

    BlockView GetBlock()
    {
        if (poolCount > 0)
        {
            poolCount--;
            BlockView b = poolArray[poolCount];

            if (b == null)
                return CreateNewBlock();

            poolArray[poolCount] = null;
            b.gameObject.SetActive(true);
            return b;
        }

        return CreateNewBlock();
    }

    BlockView CreateNewBlock()
    {
        if (blockPrefab == null)
            return null;

        GameObject obj = Instantiate(blockPrefab, transform);
        BlockView bNew = obj.GetComponent<BlockView>();
        if (bNew == null)
            bNew = obj.AddComponent<BlockView>();
        return bNew;
    }

    void HideBlock(BlockView b)
    {
        if (b == null) return;

        b.OnMoveDone = null;
        b.OnBlastDone = null;
        b.inMoveList = false;
        b.inBlastList = false;
        b.ResetBlock();
        b.gameObject.SetActive(false);

        if (poolCount < MAX_POOL_SIZE)
            poolArray[poolCount++] = b;
    }

    public void ShowHint(List<Vector2Int> positions)
    {
        if (positions == null || positions.Count == 0) return;

        for (int i = 0; i < positions.Count; i++)
        {
            int row = positions[i].y;
            int col = positions[i].x;

            if (row >= 0 && row < board.rows && col >= 0 && col < board.columns)
            {
                BlockView block = blocks[row, col];
                if (block != null && !block.IsHinting())
                {
                    block.StartHint();
                }
            }
        }
    }

    public void StopAllHints()
    {
        for (int r = 0; r < board.rows; r++)
        {
            for (int c = 0; c < board.columns; c++)
            {
                if (blocks[r, c] != null && blocks[r, c].IsHinting())
                {
                    blocks[r, c].StopHint();
                }
            }
        }
    }

    public void PlayShuffleAnimation(System.Action onComplete = null)
    {
        StopAllHints();

        UpdateAllIcons();

        StartCoroutine(ShuffleDelayCoroutine(onComplete));
    }

    private System.Collections.IEnumerator ShuffleDelayCoroutine(System.Action onComplete)
    {
        yield return shuffleWait;
        onComplete?.Invoke();
    }
}