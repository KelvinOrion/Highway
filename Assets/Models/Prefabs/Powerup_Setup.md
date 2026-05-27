# Powerup Prefab Setup

Grass prefab:
- `Spawn Powerup Chance`: default `0.08`.
- `Powerup Prefabs`: assign `Powerup_TehTarik` and `Powerup_Hand` for a 50/50 roll.

Powerup prefabs:
- `Player Tag`: `Player`. Collection also falls back to the existing `Character` component.
- `Collect Sfx`: optional AudioClip slot for the pickup sound. Drag a `.wav` from `Assets/Sound` here.
- `Collect Sfx Volume`: pickup sound volume, default `1`.
- `Bob Amplitude` / `Bob Speed`: readable pickup motion, defaults `0.1` and `2`.
- `SpriteRenderer`: placeholder solid square. Kelvin can replace only the sprite/color.
- `BoxCollider`: `Is Trigger = true`. This is 3D to match the current player Rigidbody/BoxCollider setup.

Teh Tarik:
- `Speed Multiplier`: default `1.6`.
- `Duration`: default `5`.
- `Shake Magnitude`: default `0.05` for the existing camera shake hook.
- `Speed Lines Texture`: optional Texture slot for the full-screen RawImage overlay. Leave empty to use the low-alpha white placeholder.

Hand:
- `Duration`: default `5`.
- `Countdown Tick`: optional AudioClip slot for the final three-second warning ticks. Playback is cut to `0.5` seconds per tick.
