# OMOTON MK212 integration files

This directory contains two different files for two different applications:

- `MK212-original-via.json` is the original VIA definition supplied for the
  keyboard. It describes the keyboard layout and the firmware's lighting
  controls. It is included unchanged for reference and troubleshooting.
- `OMOTON_MK212_SignalRGB.js` is the SignalRGB device plugin created while
  developing DriveGlow. Setting **Accent Bar Brightness** to `0` makes the
  plugin stop writing the accent-bar effect and color, allowing DriveGlow to
  control that light without visible color conflicts.

The SignalRGB plugin has also been submitted upstream. Until it is included in
an official SignalRGB release, these files document the exact tested behavior.

The SignalRGB plugin is covered by this repository's MIT license. The original
VIA definition remains attributable to its original author/manufacturer and is
included for interoperability and reference; no ownership is claimed.
