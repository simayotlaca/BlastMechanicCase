# Blast Mechanic Case Study

Hi! I developed a match-based blast (collapse) game for this case study. My primary focus was **performance optimization**, but I also made deliberate decisions around **player experience, game feel, and feature design** — because optimizing a game that doesn't feel good to play is only half the work.

---

## Project Overview

| Metric | Value |
|--------|-------|
| Average Frame Time | ~0.65 ms (1500+ FPS) |
| Gameplay GC Allocation | 0 bytes |
| Draw Batches | Constant 9 |
| Spikes | Only during scene load |

*(All measurements were taken in Editor with VSync disabled, Unity 2022.3 LTS on macOS. A device build would be the next validation step for production.)*

---

## Design Decisions & Player Experience

Before diving into implementation, I want to share some of the game design thinking behind the technical choices.

### Why Performance Optimization Was the Core Focus

Blast/collapse games are inherently input-driven: a tap triggers a cascade, which triggers falling, which may trigger another group check — all within a single frame window. If any of that pipeline stutters, the player feels it immediately. Smooth frame delivery isn't just an engineering metric here; it's directly tied to whether tapping the board feels satisfying or sluggish.

That said, I was careful not to optimize in ways that hurt the *feel* of the game:

- **Scale-only animations** were chosen over color-change animations for batching reasons — but also because scale pops feel more punchy and tactile for a blast mechanic than color flashes.
- The **hint system** (5-second idle → largest group pulses) is intentionally non-intrusive. It surfaces discoverability without breaking flow or feeling like the game is nagging the player.
- The **icon thresholds** (A/B/C group sizes) reward players for building larger groups visually, which reinforces the core loop: "save up, then blast big."

### Trade-offs I Made Consciously

**Custom tween vs. DOTween:**
DOTween is battle-tested and would have been faster to implement. I chose a custom struct-based system to eliminate heap allocations — but the trade-off is real: it's less flexible, harder to extend, and needs maintenance if animation requirements grow. In a production codebase with a larger team, I'd lean toward DOTween with object pooling on top unless profiling specifically showed GC pressure.

**Deterministic permutation vs. random shuffle:**
The Layer 2 deadlock solution uses a seed-based offset rather than purely random shuffling. This avoids the subjective unfairness of the board "looking the same after shuffle," but introduces a different risk: players who replay many times could notice patterns. For a casual mobile game with high session counts, this would need playtesting to validate.

**Dirty column tracking:**
Reduces board scan from ~100 cells to ~20-30 after each blast. The trade-off: slightly more bookkeeping complexity, and if a bug causes a column to not get marked dirty, it silently misses updates. I added debug assertions during development to catch this, but it's a maintenance surface worth noting.

**0-byte allocation goal:**
Achieving 0 GC bytes during gameplay required pre-allocating everything at startup. The trade-off is that the system is less dynamic — adding a new feature (e.g., a power-up that spawns a different block type) would require updating the pool and pre-allocation logic. I'd accept some allocation in feature development and return to profiling before shipping.

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
I used a fixed array instead of Queue\<T\>. Enqueue/Dequeue operations are just index increments.

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
First, I find a color that has at least 2 blocks and try to place 2 of them adjacent to each other (single internal swap, not player-initiated). This is the least disruptive resolution — the board barely changes, and players often don't notice it happened.

**Layer 2 - Deterministic Permutation:**
If a swap isn't enough, I shift colors using a mathematical offset. It's seed-based rather than random. Each attempt yields a different result, but in a controlled manner. This avoids the "board just randomly reshuffled" feeling, though as noted above, it's a trade-off worth validating with players.

**Layer 3 - Hard Reset:**
In very rare cases (e.g., only one color left on the board), I regenerate the board from scratch. This is the most jarring for the player, so the system tries hard to avoid reaching here.

The layered approach matters for game feel: players are generally okay with invisible fixes, tolerant of visible-but-subtle reshuffles, and frustrated by full resets. The system tries to stay in the first two categories.

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

The Model has no Unity dependencies — it could be unit-tested in isolation, which I'd prioritize in a production context.

---

## Icon System

According to the PDF rules:
- Group size \< A (4): Default icon
- A \<= Group size \< B: Icon A
- B \<= Group size \< C: Icon B
- Group size \>= C: Icon C

Threshold values are configurable through GameConfig. This system makes large groups feel rewarding — the visual upgrade acts as positive reinforcement for the "save up, blast big" loop.

---

## Hint System

If no tap occurs for 5 seconds, I find the largest group and display a pulse animation. The hint disappears when the user taps.

Design intent: surface discoverability without pressuring the player. Highlighting the *largest* group (not just any valid group) also subtly teaches the player to look for bigger clusters.

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

*(Editor measurements. Device profiling on target hardware would be the production validation step.)*

---

## Why These Choices?

**Stamp instead of HashSet:** Most BFS implementations use HashSet for visited tracking, which allocates memory. I just increment an integer — if `visited[i] == currentStamp`, it's visited. No clearing needed between searches.

**Dirty columns instead of full scan:** After a blast, only some columns change. Why scan 100 cells when 20-30 are enough? I track which columns are "dirty" and only recompute those.

**Swap before shuffle:** When there's no valid move, I first try to fix it with a single swap (2 cells). Only if that fails do I escalate. Most implementations jump straight to random shuffling — mine tries the minimal, least-disruptive change first.

---

## What I Learned

1. The struct vs class distinction matters. Using structs for frequently created objects prevents GC overhead.
2. Don't optimize without measuring. The Profiler shows what's actually slow.
3. Sometimes simple solutions are better. Array + counter can outperform HashSet.
4. Performance and game feel aren't always in conflict — understanding Unity's rendering pipeline helped me find solutions that were both fast *and* felt better to play.
5. Explicit trade-off thinking matters. Every architectural choice is a bet; being clear about what you're trading away is as important as what you're gaining.

---

## Build Recommendations

- Scripting Backend: IL2CPP
- Managed Stripping Level: Medium/High
- Strip Engine Code: Enabled

---

Thank you!
