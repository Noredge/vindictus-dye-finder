# Vindictus Dye Finder

An offline Windows app that helps you find useful dye positions from a Vindictus screenshot. Choose the colors you want, then explore a few suggested spots in game.

**Preview 0.3.2 · Windows x64 · MIT**

## Download and start

**[Download the Windows x64 ZIP from Releases](https://github.com/Noredge/vindictus-dye-finder/releases/latest)**

1. Download `VindictusDyeFinder-0.3.2-win-x64.zip` from the release assets.
2. Extract the entire ZIP into a folder.
3. Open **VindictusDyeFinder.exe**. Keep the included files together.

No installer, Python or separate .NET installation is required. Do not run the executable from inside the ZIP. Release assets include `SHA256SUMS.txt` for download verification.

## Find a dye spot

1. **Open Screenshot** — open or drag in a PNG, JPEG or BMP. You can also paste an image with **Ctrl+V**. Use an original, unscaled screenshot of the random dye board. **Try Demo** opens a real cropped game example.
2. **Choose Colors** — select one or more presets, or use **Add Color** to enter your own RGB values and an optional name. Click an existing name to rename it. Colors, names and tolerance are saved between sessions.
3. **Find Matches** — choose one of up to three **Suggested Spots**, then manually try that area in game.

Each suggestion shows six estimated RGB values in the same two-row order as the game. Cyan rings mark the suggested points; yellow rings mark the original crosses. Click any numbered suggested point to inspect it in the shared zoom preview. The selected point turns white without changing the recommendation.

**Color Tolerance** stays visible on the main screen. Increase it to allow more color variation. A pale-yellow message means no matches were found at the current tolerance; try a higher value or another color, then click **Find Matches** again.

## If a result looks off

Check that the yellow rings line up with the original six crosses. Under **Advanced**, you can select the color board and mark the six original points manually if detection is wrong. **Prefer easier alignment** gives nearby matching positions more weight within the same matching-slot count.

The app recommends useful starting points, not guaranteed exact colors. Small nearby adjustments may help. Screenshot sampling, narrow color bands, clothing materials and lighting can affect the result. Always preview in game before committing to a dye.

The custom RGB crafting picker is a different interface and is not supported by the random-board detector. The bundled demo demonstrates the workflow; your current game board needs its own screenshot.

## Share feedback

After trying a suggestion, select it and click **Record Result**:

- **Matched here** — the recommended position was useful.
- **Found nearby** — a small adjustment helped.
- **Not useful** — the recommendation did not help.

Notes and screenshots are optional. If you attach a result image, crop and review it first. **Save Feedback** creates a local ZIP; **Open Feedback Folder** opens its location. Review the ZIP before attaching it to a [GitHub issue](https://github.com/Noredge/vindictus-dye-finder/issues).

An optional exact six-color record requires confirmation that it came from the original recommended position. Nearby results are useful feedback but cannot label that original position with exact RGB.

## Privacy

The app works offline. It does not upload feedback, capture the screen, inspect game processes or control the game. It only analyzes images you open, drop or paste, plus the bundled demo.

Preferences and feedback stay in `%LOCALAPPDATA%/VindictusDyeFinder`. Nothing is shared unless you send it yourself.

## Build from source

Install the .NET 10 SDK on Windows, then run:

```powershell
./scripts/Build.ps1
./scripts/Test.ps1
```

Run `src/DyeFinder.App/bin/Release/net10.0-windows/VindictusDyeFinder.exe`.

For a self-contained Windows x64 build:

```powershell
./scripts/Publish.ps1 -Online
```

Online publishing downloads runtime packages from NuGet. For offline publishing, supply `-RuntimePackages <NuGet-cache>` with the required runtime packages already available. The CI toolchain uses SDK 10.0.400 and runtime 10.0.11. The app has no third-party application packages.

## License

Application source and the original paintbrush icon are MIT licensed. Vindictus and the bundled game artwork belong to their respective owners; see [asset notices](src/DyeFinder.App/Assets/README.md). This is an independent utility.
