using UnityEngine;
using System;

public class TweenSystem : MonoBehaviour
{
    public static TweenSystem Instance { get; private set; }

    public enum TweenType : byte
    {
        None = 0,
        Move = 1,
        Scale = 2,
        PunchScale = 3
    }

    public enum EaseType : byte
    {
        Linear = 0,
        InQuad = 1,
        OutQuad = 2,
        InOutQuad = 3,
        OutBack = 4,
        InBack = 5,
        OutElastic = 6
    }

    [System.Serializable]
    public struct TweenData
    {
        public Transform target;
        public TweenType type;
        public EaseType ease;

        public Vector3 startValue;
        public Vector3 endValue;
        public Vector3 originalValue;

        public float duration;
        public float elapsed;
        public float delay;

        public bool active;
        public bool hasCallback;
        public int callbackId;

        public float overshoot;
    }

    private const int MAX_TWEENS = 200;
    private const int MAX_CALLBACKS = 200;

    private TweenData[] tweens;
    private int activeTweenCount = 0;

    private Action[] callbackPool;
    private bool[] callbackActive;
    private int nextCallbackId = 0;

    private Vector3 tempVector;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        tweens = new TweenData[MAX_TWEENS];
        callbackPool = new Action[MAX_CALLBACKS];
        callbackActive = new bool[MAX_CALLBACKS];

        for (int i = 0; i < MAX_TWEENS; i++)
        {
            tweens[i] = new TweenData { active = false };
        }
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        for (int i = 0; i < MAX_TWEENS; i++)
        {
            if (!tweens[i].active) continue;

            ref TweenData t = ref tweens[i];

            if (t.delay > 0f)
            {
                t.delay -= deltaTime;
                continue;
            }

            t.elapsed += deltaTime;
            float progress = Mathf.Clamp01(t.elapsed / t.duration);
            float easedProgress = ApplyEase(progress, t.ease, t.overshoot);

            if (t.target == null)
            {
                CompleteTween(ref t, i);
                continue;
            }

            switch (t.type)
            {
                case TweenType.Move:
                    t.target.position = Vector3.LerpUnclamped(t.startValue, t.endValue, easedProgress);
                    break;

                case TweenType.Scale:
                    t.target.localScale = Vector3.LerpUnclamped(t.startValue, t.endValue, easedProgress);
                    break;

                case TweenType.PunchScale:
                    float punchProgress = 1f - progress;
                    float punchValue = Mathf.Sin(progress * Mathf.PI) * punchProgress;
                    tempVector.x = t.originalValue.x + t.endValue.x * punchValue;
                    tempVector.y = t.originalValue.y + t.endValue.y * punchValue;
                    tempVector.z = t.originalValue.z + t.endValue.z * punchValue;
                    t.target.localScale = tempVector;
                    break;
            }

            if (progress >= 1f)
            {
                CompleteTween(ref t, i);
            }
        }
    }

    private void CompleteTween(ref TweenData t, int index)
    {
        if (t.target != null)
        {
            switch (t.type)
            {
                case TweenType.Move:
                    t.target.position = t.endValue;
                    break;
                case TweenType.Scale:
                    t.target.localScale = t.endValue;
                    break;
                case TweenType.PunchScale:
                    t.target.localScale = t.originalValue;
                    break;
            }
        }

        if (t.hasCallback && t.callbackId >= 0 && t.callbackId < MAX_CALLBACKS)
        {
            if (callbackActive[t.callbackId])
            {
                callbackPool[t.callbackId]?.Invoke();
                callbackActive[t.callbackId] = false;
                callbackPool[t.callbackId] = null;
            }
        }

        t.active = false;
        t.target = null;
        activeTweenCount--;
    }

    private float ApplyEase(float t, EaseType ease, float overshoot = 1.70158f)
    {
        switch (ease)
        {
            case EaseType.Linear:
                return t;

            case EaseType.InQuad:
                return t * t;

            case EaseType.OutQuad:
                return t * (2f - t);

            case EaseType.InOutQuad:
                return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

            case EaseType.OutBack:
                float t1 = t - 1f;
                return t1 * t1 * ((overshoot + 1f) * t1 + overshoot) + 1f;

            case EaseType.InBack:
                return t * t * ((overshoot + 1f) * t - overshoot);

            case EaseType.OutElastic:
                if (t <= 0f) return 0f;
                if (t >= 1f) return 1f;
                return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - 0.1f) * (2f * Mathf.PI) / 0.4f) + 1f;

            default:
                return t;
        }
    }

    public int DoMove(Transform target, Vector3 endPosition, float duration, EaseType ease = EaseType.Linear, float delay = 0f, Action onComplete = null)
    {
        int slot = GetFreeSlot();
        if (slot < 0) return -1;

        ref TweenData t = ref tweens[slot];
        t.target = target;
        t.type = TweenType.Move;
        t.ease = ease;
        t.startValue = target.position;
        t.endValue = endPosition;
        t.duration = duration;
        t.elapsed = 0f;
        t.delay = delay;
        t.active = true;
        t.overshoot = 1.70158f;

        SetupCallback(ref t, onComplete);
        activeTweenCount++;

        return slot;
    }

    public int DoScale(Transform target, Vector3 endScale, float duration, EaseType ease = EaseType.Linear, float delay = 0f, Action onComplete = null)
    {
        int slot = GetFreeSlot();
        if (slot < 0) return -1;

        ref TweenData t = ref tweens[slot];
        t.target = target;
        t.type = TweenType.Scale;
        t.ease = ease;
        t.startValue = target.localScale;
        t.endValue = endScale;
        t.duration = duration;
        t.elapsed = 0f;
        t.delay = delay;
        t.active = true;
        t.overshoot = 2f;

        SetupCallback(ref t, onComplete);
        activeTweenCount++;

        return slot;
    }

    public int DoPunchScale(Transform target, Vector3 punch, float duration, Action onComplete = null)
    {
        int slot = GetFreeSlot();
        if (slot < 0) return -1;

        ref TweenData t = ref tweens[slot];
        t.target = target;
        t.type = TweenType.PunchScale;
        t.ease = EaseType.Linear;
        t.originalValue = target.localScale;
        t.endValue = punch;
        t.duration = duration;
        t.elapsed = 0f;
        t.delay = 0f;
        t.active = true;

        SetupCallback(ref t, onComplete);
        activeTweenCount++;

        return slot;
    }

    public void Kill(Transform target)
    {
        if (target == null) return;

        for (int i = 0; i < MAX_TWEENS; i++)
        {
            if (tweens[i].active && tweens[i].target == target)
            {
                ClearTween(ref tweens[i]);
                activeTweenCount--;
            }
        }
    }

    public void KillById(int id)
    {
        if (id < 0 || id >= MAX_TWEENS) return;
        if (!tweens[id].active) return;

        ClearTween(ref tweens[id]);
        activeTweenCount--;
    }



    private int GetFreeSlot()
    {
        for (int i = 0; i < MAX_TWEENS; i++)
        {
            if (!tweens[i].active)
                return i;
        }


        return -1;
    }

    private void SetupCallback(ref TweenData t, Action callback)
    {
        if (callback == null)
        {
            t.hasCallback = false;
            t.callbackId = -1;
            return;
        }

        for (int i = 0; i < MAX_CALLBACKS; i++)
        {
            int idx = (nextCallbackId + i) % MAX_CALLBACKS;
            if (!callbackActive[idx])
            {
                callbackPool[idx] = callback;
                callbackActive[idx] = true;
                t.hasCallback = true;
                t.callbackId = idx;
                nextCallbackId = (idx + 1) % MAX_CALLBACKS;
                return;
            }
        }

        t.hasCallback = false;
        t.callbackId = -1;

    }

    private void ClearTween(ref TweenData t)
    {
        if (t.hasCallback && t.callbackId >= 0 && t.callbackId < MAX_CALLBACKS)
        {
            callbackActive[t.callbackId] = false;
            callbackPool[t.callbackId] = null;
        }
        t.active = false;
        t.target = null;
        t.hasCallback = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
