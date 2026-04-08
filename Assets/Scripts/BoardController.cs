using System.Collections.Generic;
using UnityEngine;

public enum GameState
{
    Idle,
    ApplyGravityAndSpawn,
    WaitAfterGravity,
    CheckDeadlock,
    ShuffleAnimating,
    FinalizeAfterShuffle
}

public class BoardController : MonoBehaviour
{
    public GameConfig config;
    public BoardView boardView;
    public GameObject blockPrefab;
    public float waitAfterGravity = 0f;

    private Board board;
    private int colorCount;

    private bool isBusy = false;
    private GameState state = GameState.Idle;

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
        state = GameState.Idle;

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

        if (state != GameState.ShuffleAnimating && boardView.isAnimating)
            return;

        switch (state)
        {
            case GameState.ApplyGravityAndSpawn:
                DoGravity();
                DoSpawn();
                waitTimer = 0f;
                state = GameState.WaitAfterGravity;
                break;

            case GameState.WaitAfterGravity:
                waitTimer += Time.deltaTime;
                if (waitTimer < waitAfterGravity)
                    return;
                state = GameState.CheckDeadlock;
                break;

            case GameState.CheckDeadlock:
                if (needsShuffle)
                {
                    needsShuffle = false;
                    state = GameState.ShuffleAnimating;
                    boardView.PlayShuffleAnimation(() =>
                    {
                        board.ShuffleBoard();
                        boardView.RefreshAllBlocks();
                        board.RecomputeGroupsAndCheckMove();
                        state = GameState.FinalizeAfterShuffle;
                    });
                    return;
                }

                EnsureValidMoveExists();
                boardView.UpdateAllIcons();
                isBusy = false;
                state = GameState.Idle;
                ResetIdleTimer();
                break;

            case GameState.ShuffleAnimating:
                break;

            case GameState.FinalizeAfterShuffle:
                EnsureValidMoveExists();
                boardView.UpdateAllIcons();
                isBusy = false;
                state = GameState.Idle;
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
        state = GameState.ApplyGravityAndSpawn;
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
        board.ApplyGravityAll();
        int count = board.GetGravityMoveCount();
        for (int i = 0; i < count; i++)
        {
            GravityMove move = board.GetGravityMove(i);
            boardView.MoveBlock(move.fromRow, move.col, move.toRow, move.col);
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