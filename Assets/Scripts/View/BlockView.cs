using UnityEngine;

public class BlockView : MonoBehaviour
{
    public int row;
    public int col;
    public int colorIndex;

    public bool inMoveList;
    public bool inBlastList;

    public System.Action<BlockView> OnMoveDone;
    public System.Action<BlockView> OnBlastDone;

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;
    private Vector3 originalScale;

    private const float BLAST_DURATION = 0.25f;
    private const float MOVE_BOUNCE_SCALE = 0.15f;
    private const float MOVE_BOUNCE_DURATION = 0.15f;
    private const float HINT_PULSE_DURATION = 0.4f;

    private bool isHinting = false;
    private bool isMoving = false;
    private bool isBlasting = false;

    private int moveTweenId = -1;
    private int blastTweenId = -1;
    private int hintTweenId = -1;
    private int punchTweenId = -1;

    private int hintLoopCount = 0;
    private const int HINT_MAX_LOOPS = 3;

    private static readonly Vector3 PUNCH_SCALE = new Vector3(MOVE_BOUNCE_SCALE, -MOVE_BOUNCE_SCALE, 0f);
    private static readonly Color COLOR_DEFAULT = Color.white;

    private System.Action onMoveEnd;
    private System.Action onBlastEnd;
    private System.Action onPunchEnd;
    private System.Action onHintUp;
    private System.Action onHintDown;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sortingLayerName = "Default";

        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
            boxCollider = gameObject.AddComponent<BoxCollider2D>();

        onMoveEnd = OnMoveAnimComplete;
        onBlastEnd = OnBlastAnimComplete;
        onPunchEnd = OnPunchComplete;
        onHintUp = OnHintScaleUpComplete;
        onHintDown = OnHintScaleDownComplete;
    }

    public void Setup(int row, int col, int colorIndex, float blockSize)
    {
        this.row = row;
        this.col = col;
        this.colorIndex = colorIndex;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.sortingOrder = row;
        }

        boxCollider.size = Vector2.one * blockSize * 0.9f;
        boxCollider.enabled = true;

        KillAllTweens();
        gameObject.SetActive(true);
    }

    public void UpdateSortingOrder()
    {
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = row;
    }

    public void SetSprite(Sprite sprite, float blockSize)
    {
        if (sprite == null)
            return;

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = COLOR_DEFAULT;

        float scale = blockSize / sprite.bounds.size.x;
        transform.localScale = Vector3.one * scale;
        originalScale = transform.localScale;
    }

    public void MoveTo(Vector3 target, float duration)
    {
        KillMoveTween();
        StopHint();

        isMoving = true;

        moveTweenId = TweenSystem.Instance.DoMove(
            transform,
            target,
            duration,
            TweenSystem.EaseType.InQuad,
            0f,
            onMoveEnd
        );
    }

    private void OnMoveAnimComplete()
    {
        if (!isMoving) return;
        isMoving = false;
        moveTweenId = -1;

        punchTweenId = TweenSystem.Instance.DoPunchScale(
            transform,
            PUNCH_SCALE,
            MOVE_BOUNCE_DURATION,
            onPunchEnd
        );
    }

    private void OnPunchComplete()
    {
        punchTweenId = -1;
        OnMoveDone?.Invoke(this);
    }

    public void StartBlast(float delay = 0f)
    {
        originalScale = transform.localScale;
        boxCollider.enabled = false;

        KillBlastTween();
        StopHint();

        isBlasting = true;

        blastTweenId = TweenSystem.Instance.DoScale(
            transform,
            Vector3.zero,
            BLAST_DURATION * 0.5f,
            TweenSystem.EaseType.InBack,
            delay,
            onBlastEnd
        );
    }

    private void OnBlastAnimComplete()
    {
        if (!isBlasting) return;
        isBlasting = false;
        blastTweenId = -1;

        transform.rotation = Quaternion.identity;
        OnBlastDone?.Invoke(this);
    }

    public void StartHint()
    {
        if (isHinting) return;
        isHinting = true;
        hintLoopCount = 0;

        KillHintTween();

        hintTweenId = TweenSystem.Instance.DoScale(
            transform,
            originalScale * 1.15f,
            HINT_PULSE_DURATION,
            TweenSystem.EaseType.InOutQuad,
            0f,
            onHintUp
        );
    }

    private void OnHintScaleUpComplete()
    {
        if (!isHinting) return;

        hintTweenId = TweenSystem.Instance.DoScale(
            transform,
            originalScale,
            HINT_PULSE_DURATION,
            TweenSystem.EaseType.InOutQuad,
            0f,
            onHintDown
        );
    }

    private void OnHintScaleDownComplete()
    {
        if (!isHinting) return;

        hintLoopCount++;

        if (hintLoopCount < HINT_MAX_LOOPS)
        {
            hintTweenId = TweenSystem.Instance.DoScale(
                transform,
                originalScale * 1.15f,
                HINT_PULSE_DURATION,
                TweenSystem.EaseType.InOutQuad,
                0f,
                onHintUp
            );
        }
        else
        {
            isHinting = false;
            hintTweenId = -1;
            transform.localScale = originalScale;
        }
    }

    public void StopHint()
    {
        if (!isHinting) return;
        isHinting = false;

        KillHintTween();
        transform.localScale = originalScale;
    }

    public bool IsHinting()
    {
        return isHinting;
    }


    public void ResetBlock()
    {
        KillAllTweens();

        inMoveList = false;
        inBlastList = false;
        isHinting = false;
        isMoving = false;
        isBlasting = false;
        moveTweenId = -1;
        blastTweenId = -1;
        hintTweenId = -1;
        punchTweenId = -1;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
        }

        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        boxCollider.enabled = true;
        gameObject.SetActive(false);
    }

    private void KillAllTweens()
    {
        if (TweenSystem.Instance != null)
        {
            TweenSystem.Instance.KillById(moveTweenId);
            TweenSystem.Instance.KillById(blastTweenId);
            TweenSystem.Instance.KillById(hintTweenId);
            TweenSystem.Instance.KillById(punchTweenId);
        }
        moveTweenId = -1;
        blastTweenId = -1;
        hintTweenId = -1;
        punchTweenId = -1;
        isMoving = false;
        isBlasting = false;
        isHinting = false;
    }

    private void KillMoveTween()
    {
        if (TweenSystem.Instance != null && moveTweenId >= 0)
        {
            TweenSystem.Instance.KillById(moveTweenId);
            moveTweenId = -1;
        }
        if (TweenSystem.Instance != null && punchTweenId >= 0)
        {
            TweenSystem.Instance.KillById(punchTweenId);
            punchTweenId = -1;
        }
        isMoving = false;
    }

    private void KillBlastTween()
    {
        if (TweenSystem.Instance != null && blastTweenId >= 0)
        {
            TweenSystem.Instance.KillById(blastTweenId);
            blastTweenId = -1;
        }
        isBlasting = false;
    }

    private void KillHintTween()
    {
        if (TweenSystem.Instance != null && hintTweenId >= 0)
        {
            TweenSystem.Instance.KillById(hintTweenId);
            hintTweenId = -1;
        }
    }

    void OnDestroy()
    {
        KillAllTweens();
    }
}
