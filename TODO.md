# Next steps

## Completed in 0.3.2
- [x] Simple English UI, dark title bar/scrollbars and paintbrush icon.
- [x] Real cropped demo; screenshot import/drop and Ctrl+V.
- [x] Three suggested spots, six estimated RGB values and one clickable point preview.
- [x] Visible tolerance control; pale-yellow no-match message.
- [x] Custom color names with rename and persistence.
- [x] Local feedback ZIPs and Open Feedback Folder.
- [x] Local user acceptance and automated regression checks.

## Next
- [ ] Prepare a reproducible Windows CI build and test workflow.
- [ ] Before distributing a GitHub Release, verify the exact CI-built portable ZIP, executable and checksum. Release publication is separate from the initial source push.
- [ ] Gather a small set of trial feedback across different boards; prioritize confusing UI and hard-to-align positions.
- [ ] Compare easier-alignment ranking against the retained baseline on identical inputs. Prefer wider usable areas over isolated matching pixels.
- [ ] Investigate color error only with reliable paired samples and held-out boards; do not enable a global correction from a few examples.

Large-scale custom-dye databases and material-specific models are deferred. Keep changes small enough to compare and roll back.
