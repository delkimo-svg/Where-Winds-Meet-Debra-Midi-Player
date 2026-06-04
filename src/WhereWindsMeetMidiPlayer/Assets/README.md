# Runtime assets (ship with releases)

Only these files are required for the UI. Other mockup PNGs are optional and pruned by `scripts/build-release.ps1`.

| File | Used by |
|------|---------|
| `debra-36-keys.json` | Key mapping |
| `default-keymap.json` | Fallback keymap |
| `debra-bg-landscape.png` | Main background (Sakura / pink theme) |
| `debra-bg-wuxia.png` | Main background (Wuxia — synthetic grey-black, `scripts/generate-wuxia-theme-assets.ps1`) |
| `debra-header-wuxia-clouds-source.png` | AI source art for Wuxia title bar (optional in portable builds) |
| `debra-header-wuxia-mist.png` | Title bar gold cloud strip 1920×48 (Wuxia, `scripts/process-wuxia-header-clouds.ps1`) |
| `Assets/Fonts/CormorantGaramond-SemiBold.ttf` | Header title font (SIL OFL) |
| `debra-wwm-header-logo.png` | Top-left title bar emblem (Where Winds Meet) |
| `debra-cherry-corner.png` | Player bar — bottom-left accent (Sakura theme) |
| `debra-player-wuxia-corner-bl.png` | Player bar — bottom-left gold branch (Wuxia, `scripts/generate-wuxia-player-corner-bl.ps1`) |
| `debra-player-sakura-corner-br.png` | Player bar — bottom-right sakura corner border (Sakura theme) |
| `debra-player-wuxia-corner-br.png` | Player bar — bottom-right gold blossom corner (Wuxia theme) |
| `debra-sakura-branch-left.png` | Now playing card — left blossom branch |
| `debra-sakura-branch-right-tag.png` | Now playing card — top-right branch + hanging tag |
| `debra-thumb-art.png` | Track thumbnail (Sakura) |
| `debra-thumb-wuxia.png` | Track thumbnail (Wuxia, `scripts/generate-wuxia-thumb.ps1`) |
| `debra-character-hero.png` | Now playing portrait (Sakura theme) |
| `debra-wuxia-hero.png` | Now playing portrait (Wuxia / dark gold theme) |
| `debra-wuxia-branch-left.png` | Wuxia now playing — left gold blossom branch |
| `debra-wuxia-branch-right.png` | Wuxia now playing — top-right gold blossom branch |
| `debra-sidebar-menu-bg.png` | **Generated** menu banner, no text (run `scripts/process-menu-banner.ps1`) |
| `debra-sidebar-menu-bg-wuxia.png` | Wuxia bookmark menu banner 149×682 (`scripts/process-wuxia-menu-banner.ps1`) |
| `debra-sidebar-menu-bg-wuxia-source.png` | Source art for Wuxia banner processing |
| `debra-nav-splatter.png` | White splatter glow for selected nav item |
| `debra-sidebar-scroll.png` | Legacy scroll fallback |
| `debra-sidebar-castle-scene.png` | Left column castle background (behind scroll) |
| `debra-sidebar-footer.png` | Castle background fallback |
| `debra-sidebar-castle-bg.png` | Castle background fallback |
| `debra-sidebar-bottom-banner.png` | Castle background fallback |

Place PNGs in this folder before building. They are not committed if missing from your clone—add your art pack locally or from your design export.
