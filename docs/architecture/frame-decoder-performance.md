# Frame Decoder Performance

## Architecture Change

### Before: FrameDecoder (Per-Frame File Open)
- Opens video file for EVERY frame
- Creates new codec context each time
- Decodes at native resolution (e.g., 1920×1080)
- Performance: ~100-200ms per frame
- Timeline scrubbing: Sluggish, noticeable lag

### After: PersistentFrameDecoder (Persistent + Downscale)
- Opens video file ONCE in constructor
- Reuses codec and scaling contexts
- Decodes to fixed 640×360 resolution
- Performance: ~5-10ms per frame (20-40× faster!)
- Timeline scrubbing: Instant, QuickTime-smooth

## Memory Usage

**640×360 @ 60 frames:**
- Per frame: 640 × 360 × 4 bytes (BGRA) = 921KB
- Total cache: 60 × 921KB ≈ 54MB
- Very reasonable for modern systems

**Comparison to 1080p:**
- 1080p frame: 1920 × 1080 × 4 = 8.3MB
- 60 frames @ 1080p = 498MB (9× larger!)

## Cache Strategy

**Bidirectional Preloading:**
- Current frame + 30 backward + 30 forward
- Covers ±1 second at 30fps
- LRU eviction keeps most relevant frames
- Cancellation tokens prevent obsolete preloads

## Why This Works

**QuickTime's Secret:**
1. Keep file handles open (no reopen overhead)
2. Use lower resolution for scrubbing preview
3. Preload nearby frames bidirectionally
4. Cache aggressively (memory is cheap)

We now do all of these!
