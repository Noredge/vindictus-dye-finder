# Web preview validation

## Automated checks

Run `npm test`, `npm run build`, and `npm run check:dist` in `web`. The C# golden fixtures were exported from desktop commit `76301cac3481f4b433eec9fc81cbf2ff416c4391`. They contain only the already bundled demo and explicitly generated synthetic/scaled data; no private screenshots or feedback are in the public fixtures.

Checks cover 10 Lab values, 100 CIEDE2000 comparisons (absolute difference ≤ 1e-6), 10 search/geometry cases, slot RGB, matching-slot counts, ordered coordinates, marker masks, midpoint rounding, RGB parsing, crop bounds, desktop settings, invalid/corrupt storage, transparent-on-black composition, both feedback schemas, ZIP allowlists and verification rules. Search cases include the original demo, strict/no-match settings, easier alignment, 30 targets, a generated scaled demo, duplicate targets, uniform coordinate ties and a synthetic gradient. Synthetic/scaled cases are not independent game trials.

To reproduce the export on Windows with a .NET 10 SDK, from the repository root:

```powershell
dotnet build web/tools/Baseline/Baseline.csproj -c Release --artifacts-path .work/web-baseline --configfile NuGet.Config -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -nr:false
dotnet .work/web-baseline/bin/Baseline/release/Baseline.dll export web/tests/fixtures src/DyeFinder.App/Assets/DemoScreenshot.png
```

The exporter links the existing C# source without modifying it. Verify that source still matches the named baseline before regenerating. Review changed expectations; do not regenerate fixtures to hide a port regression. Raw `.rgb` fixtures are marked binary in `.gitattributes` so Git preserves their pixel bytes. The exporter also has `validate-feedback <json>...`, which uses the actual desktop schema readers and validators.

## Observed local browser behavior

On Windows 10.0.26300, AMD Ryzen 7 5800X3D, Node 24.19.0:

- Chrome 154.0.8037.98 and Codex's Chromium browser imported the real demo, detected the 256 × 256 board and returned ordered anchors (126,176), (34,87), (2,69), each matching 5 of 6 slots at default settings.
- Six numbered points changed the single preview, including inside/outside-tolerance information. Switching points left all recommendation coordinates unchanged.
- Custom RGB fallback, renaming and persistence after refresh worked in Chrome. Changing colors/options removed previous suggestions; importing a no-match configuration produced the explicit yellow warning.
- A 30-target search was cancelled through the UI. No suggestions remained. A corrupt PNG gave a clear error; changing a color preserved the ScreenshotUnreadable problem stage.
- Actual trial/problem ZIP downloads were opened and verified. Problem feedback without an image contained JSON and summary only. Found nearby had no game RGB labels. Optional result evidence was reviewed as a 225 × 303 crop, and the ZIP contained that crop rather than the 270 × 375 source.
- Downloaded trial board pixels matched the C# original demo crop byte for byte; this is evidence for this opaque PNG only. The file hash matched the exported board PNG bytes. Three downloaded records and five generated schema cases were accepted by desktop readers/validators.
- The hidden loopback launcher, HTTP response, authenticated stop and state cleanup passed local checks. The production bundle passed the file/privacy allowlist check.
- Manual selection of the demo's 256 × 256 board and six crosses returned the same three ordered anchors as automatic detection. Layouts at 1280 × 900, 980 × 680 and 420 × 900 retained one preview and had no horizontal document overflow; the two smaller layouts scroll vertically.

The observed demo search compute time was approximately 0.11–0.12 seconds per run; no P95 or large-image/memory claim is made.

## Acceptance still required

- Actual Edge, Firefox and Safari runs. Available browser-control connections in this session were Chrome and Codex's browser; installing a test browser is not proof of a browser run.
- Original screenshots from other panels/sessions, color-profile images, JPEG, transparent PNG and EXIF orientation comparisons against WPF.
- Real clipboard image paste and drag/drop; these paths are implemented but not fully exercised in the current browser run. Normal text paste must stay inside form fields.
- Manual board/six-point calibration on other screenshots and at different zoom levels; 100/125/150/200% zoom, physical high DPI, full keyboard and accessible-reader acceptance.
- Browser timings over multiple runs, 4K recognition, 30-target completed searches, continuous image replacement and memory measurements on a declared reference machine.
- User trial and authorization for a particular hosted deployment. The workflow currently produces a build artifact only; no CI or deployment success is claimed from the local checks.
