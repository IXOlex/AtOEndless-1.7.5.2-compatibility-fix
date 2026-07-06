# AtOEndless v1.0.3 - Across the Obelisk 1.7.6.1 Compatibility Fix

Unofficial compatibility update for the original **AtOEndless** mod by Corgan.

This release updates the mod for **Across the Obelisk 1.7.6.1**.

## Changes

- Ported AtOEndless to the Across the Obelisk 1.7.6.1 API changes
- Updated the card system compatibility after the `CardData` refactor
- Replaced the old `MatchManager.SetInitiatives` patch with the new `BattleMatch.InitiativesManager.SetInitiatives` patch
- Updated `EventManager.CloseEvent` destination node access
- Replaced the old DES-based save hook with independent JSON save files
- Updated runtime access to combat teams for the new battle structure
- Build verified with 0 compilation errors

## Installation

Remove the original AtOEndless DLL if it is already installed.

Place:

```text
com.corgan.AtOEndless.dll