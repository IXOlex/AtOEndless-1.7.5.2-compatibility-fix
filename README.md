# AtOEndless Compatibility Fix (ATO 1.7.5.2)

Unofficial compatibility fix for AtOEndless (original mod by Corgan).

## What this is

This is a **drop-in DLL replacement** for the original AtOEndless mod.

It restores compatibility with:

- Across the Obelisk 1.7.5.2
- BepInEx 5.4.21+

## Changes

- Updated references for current ATO version
- Fixed compilation issue in `CreateCard()`
- Runtime tested through an 11-act Endless run

## Installation

Remove the original AtOEndless DLL if installed:

```text
com.corgan.AtOEndless.dll
```

Replace it with the DLL from this release in:

```text
Across the Obelisk/BepInEx/plugins/
```

Launch the game.

## Important

Do **not** run this alongside the original AtOEndless.

Use this as a replacement.

## Notes

`CreateCard()` currently returns null.

It appears unused in runtime testing, but additional community testing is welcome.

## Credits

Original mod by Corgan.

Compatibility fix maintained by IXOlex.
