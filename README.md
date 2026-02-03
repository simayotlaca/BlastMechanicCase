# Blast Mechanic Case Study

Hi! I developed a match-based blast (collapse) game for this case study. Since the main focus was **performance optimization**, I put significant effort into Memory, CPU, and GPU efficiency.

---

## Project Overview

| Metric | Value |
|--------|-------|
| Average Frame Time | ~0.65 ms (1500+ FPS) |
| Gameplay GC Allocation | 0 bytes |
| Draw Batches | Constant 9 |
| Spikes | Only during scene load |

*(All measurements were taken in Editor with VSync disabled, Unity 2022.3 LTS on macOS)*

---

## What I Did

### Memory Optimizations

**1. Custom Tween System**

Instead of using DOTween, I wrote my own struct-based tween system. This way, there's no heap allocation during animations. Tweens are stored in a fixed-size array, and no new objects are created at runtime.

**2. Object Pooling**

Rather than constantly using Instantiate/Destroy for blocks, I retrieve them from a pool and return them when done. The pool operates with a fixed-size array.

**3. Pre-allocated Everything at Start**

All arrays are created once when the game starts. I don't use `new` during gameplay.

**4. Cached Unity Objects**

Instead of repeatedly creating objects like WaitForSeconds and Vector3, I create them once and reuse them.

---

### CPU Optimizations

**1. Stamp-Based Visited Tracking**

In the BFS algorithm, I used an integer stamp instead of HashSet. The stamp increments with each search, eliminating the need to reset the array.

**2. Dirty Column Tracking**

Instead of scanning the entire board after each blast, I only scan the columns that changed. On a 10x10 board, this means checking ~20-30 cells instead of ~100.

**3. Array-Based BFS**

I used a fixed array instead of Queue<T>. Enqueue/Dequeue operations are just index increments.

**4. Counter-Based Animation Tracking**

For animation tracking, I used simple integer counters instead of HashSet.

---

### GPU Optimizations

**1. Sprite Batching**

I used SpriteRenderer with a single shared material to keep draw calls constant. Result: constant 9 draw calls.

**2. Scale-Only Animations**

Color changes break batching, but scale animations don't. That's why I implemented animations using scale transformations.

**3. No Runtime Material Changes**

Colors are defined as sprites. I don't modify material properties at runtime.

---

## Deadlock Handling System

Sometimes there are no blastable groups left on the board. I built a 3-layer system to handle this:

**Layer 1 - Single Swap Solution:**
First, I find a color that has at least 2 blocks and try to place 2 of them adjacent to each other (single internal swap, not player-initiated). For cases that can be solved with a single swap, there's no need to shuffle the board.

**Layer 2 - Deterministic Permutation:**
If swap isn't enough, I shift colors using a mathematical offset. It's not random, but seed-based. Each attempt yields a different result, but in a controlled manner.

**Layer 3 - Hard Reset:**
In very rare cases (e.g., only one color left on the board), I regenerate the board from scratch.

---

## Board Size

The board system supports the sizes specified in the PDF (e.g. 10x12) and is architecturally scalable to larger boards. You can change rows/columns through GameConfig in the Inspector.

---

## File Structure

```
Assets/Scripts/
├── Core/
│   ├── Board.cs          # Game logic, BFS, group finding
│   ├── TweenSystem.cs    # Custom tween system
│   └── GridPos.cs        # Grid position struct
├── View/
│   ├── BoardView.cs      # Board rendering, pooling
│   └── BlockView.cs      # Block animations
├── Config/
│   ├── GameConfig.cs     # Settings (ScriptableObject)
│   └── ColorDefinition.cs
└── BoardController.cs    # Game control, state machine
```

---

## Architecture

I used the MVC pattern:

- **Model (Board.cs):** Game logic. Grid data, group finding, and deadlock detection are here.
- **View (BoardView.cs, BlockView.cs):** Visuals and animations. Independent from the Model.
- **Controller (BoardController.cs):** Input handling and state machine. Coordinates between Model and View.

---

## Icon System

According to the PDF rules:
- Group size < A (4): Default icon
- A <= Group size < B: Icon A
- B <= Group size < C: Icon B
- Group size >= C: Icon C

Threshold values are configurable through GameConfig.

---

## Hint System

If no tap occurs for 5 seconds, I find the largest group and display a pulse animation. The hint disappears when the user taps. This improves discoverability without forcing the player.

---

## Profiler Results

I tested with Unity Profiler:

```
Recording: ~95 seconds, 145,000+ frames

Frame Time:
  Average: 0.66 ms (1523 FPS)
  P95: 0.90 ms | P99: 5.49 ms

Spikes: Only during scene load
GPU: Constant 9 batches, 8 SetPass calls
```

---

## Why These Choices?

A few decisions I made that might not be obvious:

**Stamp instead of HashSet:** Most BFS implementations use HashSet for visited tracking, which allocates memory. I just increment an integer - if `visited[i] == currentStamp`, it's visited. No clearing needed between searches.

**Dirty columns instead of full scan:** After a blast, only some columns change. Why scan 100 cells when 20-30 are enough? I track which columns are "dirty" and only recompute those.

**Swap before shuffle:** When there's no valid move, I first try to fix it with a single swap (2 cells). Only if that fails, I shuffle. Most implementations jump straight to random shuffling - mine tries the minimal change first.

---

## What I Learned

1. The struct vs class distinction matters. Using structs for frequently created objects prevents GC overhead.
2. Don't optimize without measuring. The Profiler shows what's actually slow.
3. Sometimes simple solutions are better. Array + counter can outperform HashSet.
4. I understood how Unity's batching system works. Same material = fewer draw calls.

---

## Build Recommendations

- Scripting Backend: IL2CPP
- Managed Stripping Level: Medium/High
- Strip Engine Code: Enabled

---

Thank you!
