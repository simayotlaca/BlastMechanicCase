using System.Collections.Generic;
using UnityEngine;

public class BoardController : MonoBehaviour
{
    public GameConfig config;
    public BoardView boardView;
    public GameObject blockPrefab;
    public float waitAfterGravity = 0f;

    private Board board;
    private int colorCount;

    private bool isBusy = false;
    private int step = 0;

    private int totalBlasted = 0;
    private int moveCount = 0;
    private int shuffleCount = 0;

    private float waitTimer = 0f;
    private bool needsShuffle = false;

    private List<Vector2Int> selectedBlocks;
    private Camera cam;

    private Vector3 mousePos;

    private const float HINT_DELAY = 5f;
    private float idleTimer = 0f;
    private bool hintActive = false;
    private List<Vector2Int> hintBlocks;

    void Start()
    {
        if (TweenSystem.Instance == null)
        {
            GameObject tweenObj = new GameObject("TweenSystem");
            tweenObj.AddComponent<TweenSystem>();
        }

        cam = Camera.main;

        if (cam != null)
            cam.backgroundColor = new Color(0.85f, 0.9f, 0.95f);

        StartGame();
    }

    void StartGame()
    {
        if (config == null)
        {
            return;
        }
        if (blockPrefab == null)
        {
            return;
        }

        colorCount = config.GetSafeColorCount();

        if (colorCount == 0)
        {
            return;
        }

        int maxCells = config.rows * config.columns;
        selectedBlocks = new List<Vector2Int>(maxCells);
        hintBlocks = new List<Vector2Int>(maxCells);

        board = new Board(config.rows, config.columns, colorCount);

        if (boardView == null)
            boardView = GetComponent<BoardView>();

        boardView.blockPrefab = blockPrefab;
        boardView.config = config;
        boardView.Setup(board);

        board.FillBoard();
        boardView.CreateAllBlocks();

        EnsureValidMoveExists();
        boardView.UpdateAllIcons();

        totalBlasted = 0;
        moveCount = 0;
        shuffleCount = 0;
        isBusy = false;
        step = 0;

        if (boardView == null) return;
        if (board == null) return;
    }

    void Update()
    {
        if (boardView == null) return;
        if (board == null) return;

        bool canAcceptInput = !isBusy && !boardView.isAnimating;

        if (canAcceptInput)
        {
            if (Input.GetMouseButtonDown(0))
            {
                HandleMouseClick();
                ResetIdleTimer();
            }
            else
            {
                UpdateHintSystem();
            }
        }

        if (!isBusy)
            return;

        if (step != 4 && boardView.isAnimating)
            return;

        switch (step)
        {
            case 1:
                DoGravity();
                DoSpawn();
                waitTimer = 0f;
                step = 2;
                break;

            case 2:
                waitTimer += Time.deltaTime;
                if (waitTimer < waitAfterGravity)
                    return;
                step = 3;
                break;

            case 3:
                if (needsShuffle)
                {
                    needsShuffle = false;
                    step = 4;
                    boardView.PlayShuffleAnimation(() =>
                    {
                        board.ShuffleBoard();
                        boardView.RefreshAllBlocks();
                        board.RecomputeGroupsAndCheckMove();
                        step = 5;
                    });
                    return;
                }

                EnsureValidMoveExists();
                boardView.UpdateAllIcons();
                isBusy = false;
                step = 0;
                ResetIdleTimer();
                break;

            case 4:
                break;

            case 5:
                EnsureValidMoveExists();
                boardView.UpdateAllIcons();
                isBusy = false;
                step = 0;
                ResetIdleTimer();
                break;
        }
    }

    public void OnBlockClicked(int row, int col)
    {
        if (board == null) return;
        if (isBusy)
            return;

        if (board.IsEmpty(row, col))
            return;

        int groupCount = board.RemoveGroupAt(row, col, selectedBlocks);

        if (groupCount == 0)
            return;

        boardView.BlastBlocks(selectedBlocks);

        totalBlasted += groupCount;
        moveCount++;

        isBusy = true;
        step = 1;
    }

    void HandleMouseClick()
    {
        if (cam == null) return;

        mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;

        GridPos gridPos = boardView.WorldToGrid(mousePos);

        if (gridPos.isValid)
        {
            OnBlockClicked(gridPos.row, gridPos.col);
        }
    }

    void DoGravity()
    {
        for (int col = 0; col < board.columns; col++)
        {
            int writeRow = 0;

            for (int readRow = 0; readRow < board.rows; readRow++)
            {
                if (!board.IsEmpty(readRow, col))
                {
                    if (readRow != writeRow)
                    {
                        int color = board.GetColor(readRow, col);
                        board.SetBlock(writeRow, col, color);
                        board.SetEmpty(readRow, col);

                        boardView.MoveBlock(readRow, col, writeRow, col);
                    }
                    writeRow++;
                }
            }
        }
    }

    void DoSpawn()
    {
        board.RefillEmptyCells();
        boardView.AnimateSpawnsFromBoard(board);

        needsShuffle = board.DidLastRefillNeedShuffle();
    }

    void EnsureValidMoveExists()
    {

        var fix = board.EnsureValidMoveExists();

        if (fix != DeadlockFix.None)
        {
            shuffleCount++;
            boardView.RefreshAllBlocks();
        }

        board.ResetDeadlockCounter();
    }

    void UpdateHintSystem()
    {
        idleTimer += Time.deltaTime;

        if (!hintActive && idleTimer >= HINT_DELAY)
        {
            ShowHint();
        }
    }

    void ResetIdleTimer()
    {
        idleTimer = 0f;
        if (hintActive)
        {
            HideHint();
        }
    }

    void ShowHint()
    {
        if (config == null) return;

        hintBlocks.Clear();
        board.GetLargestGroup(hintBlocks);

        if (hintBlocks.Count >= config.GetSafeMinGroupSize())
        {
            hintActive = true;
            boardView.ShowHint(hintBlocks);
        }
    }

    void HideHint()
    {
        hintActive = false;
        boardView.StopAllHints();
    }
}