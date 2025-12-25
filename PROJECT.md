# Royal Leech — Project Guide

## Project Structure

```
Assets/Project/
├── Art/Sprites/Icons/   # Card suit icons (Clubs.png, Hearts.png, etc.)
├── Materials/           # LiquidFill, ImageShadow, FluidVortex, BoilingText
├── Presets/
│   ├── Icons/           # Color presets per suit (ClubsColor, HeartsColor, etc.)
│   └── Text/            # Text animation presets (ActionText, QuestionText)
├── Prefabs/             # Reusable prefabs
├── Resources/Cards/     # cards.json (for Resources.Load)
├── Scenes/              # MainGame.unity
├── Scripts/
│   ├── Core/            # GameManager, CardLoader
│   ├── Data/            # ScriptableObjects (CardData, IconColorPreset, TextAnimatorPreset)
│   └── UI/              # UI components (LiquidFillIcon, ImageShadow, TextAnimator, etc.)
└── Shaders/             # LiquidFill, ImageShadow, FluidVortex, BoilingText
```

## Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Scripts | PascalCase, descriptive | `LiquidFillIcon.cs` |
| Shaders | Path: `RoyalLeech/Category/Name` | `RoyalLeech/UI/LiquidFill` |
| Materials | Match shader name | `LiquidFill.mat` |
| Sprites | PascalCase, no suffixes | `Clubs.png` |
| Presets | Descriptive, no "Preset" suffix | `ClubsColor.asset` |
| CreateAssetMenu | All under `Game/` | `menuName = "Game/Card Data"` |

## Key Components

- **LiquidFillIcon** — Resource icon with liquid fill shader effect
- **IconColorPreset** — Color settings per card suit
- **TextAnimator** — Per-letter text animation system
- **ImageShadow** — Dynamic perspective shadow for UI
- **FluidVortex** — Background swirl shader
- **BoilingText** — Distorted text shader

## Resources Loading

JSON card data must be in `Resources/Cards/` for `Resources.Load()` to work.
CardLoader uses path: `"Cards/cards"` (no extension).

## TMP Fonts

Custom font (RussoOne) located in:
`TextMesh Pro/Resources/Fonts & Materials/Custom/`
