# Vindictus Dye Finder

A small, offline Windows app that suggests useful dye positions from a Vindictus screenshot. **Preview 0.3.2** · Windows x64 · MIT.

## Use

1. Open a screenshot, drag it in, or paste with **Ctrl+V**. **Try Demo** opens a real cropped example.
2. Choose colors and adjust **Color Tolerance**. Custom colors can have names; click a name to rename it.
3. Click **Find Matches**, choose a suggested spot, and try it in game. Click any numbered point to inspect it in the shared zoom preview.

Colors are estimates. Small adjustments may help, and material or lighting can change the result. A yellow message means no matches were found at the chosen tolerance.

## Feedback

Use **Record Result** after trying a spot, then **Open Feedback Folder** to find its ZIP. Notes and cropped screenshots are optional. Review the contents before attaching them to a GitHub issue.

The app works offline and never uploads feedback, captures the screen, or controls the game. Preferences and feedback stay in `%LOCALAPPDATA%/VindictusDyeFinder`.

## Build

Install the .NET 10 SDK on Windows, then run:

```powershell
./scripts/Build.ps1
./scripts/Test.ps1
```

Run `src/DyeFinder.App/bin/Release/net10.0-windows/VindictusDyeFinder.exe`. The repository contains source, not a packaged download. For an offline portable build, use `scripts/Publish.ps1 -RuntimePackages <NuGet-cache>` with the Windows x64 runtime packages 10.0.11 available in that cache.

See [TODO](TODO.md) for next steps and [HANDOFF](HANDOFF.md) for development status. The bundled game image remains its owner's artwork; see [asset notices](src/DyeFinder.App/Assets/README.md).
