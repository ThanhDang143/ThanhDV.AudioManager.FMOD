# Changelog
All notable changes to this project will be documented in this file.

## [3.0.0] - 2026-08-01
### Breaking Changes
- Renamed the package and runtime API from AudioManager to AudioConductor.
- Replaced the previous runtime manager with `FMODConductor`; `AudioConductor` is now the optional singleton facade.
- Removed the delay parameters from BGM play and stop operations.

### Added
- Added `IAudioConductor` for dependency injection and direct `FMODConductor` registration.
- Added JSON-backed FMOD bus and event reference storage with strongly typed wrapper generation.
- Added the `VolumeDebugger` Editor window.

### Changed
- BGM fades now use unscaled delta time.
- Editor-only tools and dependencies are isolated in Editor assemblies.

### Fixed
- Fixed cancellation races and cleanup ownership during rapid BGM transitions.
- Fixed stale loop instances remaining in the managed instance dictionary.
- Added explicit `FMOD.RESULT` handling for runtime playback, fades, loops, volume controls, and Editor cleanup.
- Fixed `CancellationTokenSource` disposal and made BGM cleanup idempotent.

## [2.1.0] - 2026-03-13
### Added
- Change FMOD Platform
- Load All Buses
- Load All Event References

## [2.0.3] - 2026-01-20
### Fixed
- Fixed UnityEditor in build

## [2.0.2] - 2026-01-20
### Updated
- `PackageImporter`
- `README`

## [2.0.0] - 2026-01-19
### Updated
- Improved the `WaitForInitializeDone()` flow to ensure AudioConductor is fully initialized before any `Play`/`Pause`/`Stop` operations.
### Added
- Added a Windows Editor tool to auto-generate code for `FMODBus` and `FMODEventReference`.

## [1.0.2] - 2025-12-26
### Updated
- Update method `PlayLoop()` to easy optimize.
### Added
- `DetachInstanceFromGameObject()` when release `EventInstance`
- Add method `TryGetEventInstance()` to get created `EventInstance`

## [1.0.1] - 2025-09-22
### Fixed
- Volume debug view

## [0.0.2] - 2025-08-17
### Added
- Volume control

## [0.0.1] - 2025-08-17
### Added
- AudioConductor