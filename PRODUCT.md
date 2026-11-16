# Product

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop (WinForms)

## Users

Friends who play Euro Truck Simulator 2 and use the Milton Fretes mod. Some are not comfortable managing mod files manually.

## Product Purpose

Install and update the Milton Fretes ETS2 mod with a few clear actions, so players can get the current version without manually downloading, renaming, and replacing package files.

## Positioning

A small launcher for one ETS2 mod. It checks GitHub releases, verifies downloads with SHA-256, normalizes the installed package name, and can update itself from a separate launcher release channel.

## Operating Context

Players run it on Windows beside ETS2. The mod is installed under the game's data folder; players may use the Documents default or select a custom folder. The game must be closed before package changes. Players activate the mod in the ETS2 Mod Manager and use matching versions for Convoy.

## Capabilities and Constraints

- The launcher is a C# Windows Forms application compiled against the .NET Framework.
- Mod releases use GitHub tags such as `v1.13.0`; launcher releases use `launcher-vX.Y.Z` tags and do not replace GitHub's `latest` mod release.
- The launcher verifies package and executable downloads before installing them.
- It identifies Milton Fretes packages from `manifest.sii`, moves duplicates and replaced versions to local backups, and can restore a selected backup.
- The launcher is distributed as an unsigned Windows executable.
- It must preserve unrelated mods and user files in the ETS2 mod folder.

## Brand Commitments

- Keep the Milton Fretes name and supplied launcher icon.
- Preserve the confirmed Milton Fretes navy and blue identity while refining how it is applied.
- Use clear, direct Portuguese labels for players who do not have programming experience.

## Evidence on Hand

- Windows Forms implementation in `launcher/MainForm.cs`.
- Supplied icon at `launcher/milton-fretes.ico`.
- Public source repository and published mod and launcher releases.
- No telemetry, user testimonials, or game screenshots are supplied for the launcher; do not invent any.

## Product Principles

- Make update status and the next useful action clear.
- Verify files before changing the game installation.
- Preserve user data and provide a recoverable path when updating or restoring.
- Keep common tasks understandable without programming knowledge.
