# AudioConductor (FMOD Integration)

Lightweight central audio controller for Unity and FMOD. It handles one-shot sounds, BGM transitions, bus volume, and managed loop instances while supporting both a lazy singleton facade and dependency injection.

## Requirements

- Unity 2022.3 or newer.
- FMOD for Unity must be installed separately.
- Tested with FMOD 2.02.41.
- Use FMOD Import Type: Streaming Assets. Asset Bundle import is not supported.

Before using string event paths or the Editor import tools, build the FMOD banks and make sure the master and strings banks are available to the Unity project.

## Features

- One-shot SFX with EventReference and string path overloads.
- Optional world position for 3D one-shots.
- Immediate BGM crossfade with configurable fade duration.
- Cancellation-safe BGM transitions using unscaled time.
- Loop instances tracked by a unique string ID.
- Optional GameObject and Rigidbody attachment for loop positioning and Doppler.
- FMOD bus volume control.
- Explicit FMOD.RESULT validation and cleanup.
- Lazy singleton facade through AudioConductor.Instance.
- Dependency injection through IAudioConductor and FMODConductor.
- Bus Manager, EventReference Manager, and Volume Debugger Editor tools.

## Installation

### Unity Package Manager

Use the versioned Git URL for a reproducible installation:

~~~
https://github.com/ThanhDV143/ThanhDV.AudioConductor.FMOD.git?path=/Assets/Packages/AudioConductor/FMOD#v3.0.0
~~~

1. Open **Window** > **Package Manager**.
2. Press the **+** button and choose **Add package from git URL**.
3. Enter the URL above and press **Add**.

### Scoped Registry

Add the following scoped registry in **Project Settings** > **Package Manager** > **Add New Scoped Registry**:

- Name: ThanhDVs
- URL: https://upm.thanhdv.com
- Scope: thanhdv

Then install package **thanhdv.audioconductor.fmod** from **My Registries** or choose **Add package by name**.

## Quick Start

### Singleton facade

AudioConductor.Instance initializes lazily. It creates its lifecycle GameObject automatically, so no prefab or initialization wait is required.

~~~csharp
using ThanhDV.AudioConductor.FMOD;

IAudioConductor audio = AudioConductor.Instance;

audio.PlayOneShot("event:/UI/Click");
audio.PlayBGM("event:/Music/Main", 1f);
audio.SetVolume(FMODBus.SFX, 0.8f);
~~~

The singleton lifecycle calls Dispose automatically when its internal GameObject is destroyed. AudioConductor.Dispose can also be called explicitly when required.

### Public API

~~~csharp
void SetVolume(Bus bus, float volume);
float GetVolume(Bus bus);

void PlayOneShot(EventReference eventReference);
void PlayOneShot(string eventPath);
void PlayOneShot(EventReference eventReference, Vector3 position);
void PlayOneShot(string eventPath, Vector3 position);

void PlayBGM(EventReference eventReference, float fadeDuration = 1f);
void PlayBGM(string eventPath, float fadeDuration = 1f);
void StopBGM(float fadeDuration = 1f);

EventInstance PlayLoop(
    string id,
    string eventPath,
    GameObject attachedObject = null,
    Rigidbody attachedRigidbody = null);

EventInstance PlayLoop(
    string id,
    EventReference eventReference,
    GameObject attachedObject = null,
    Rigidbody attachedRigidbody = null);

void PauseLoop(string id);
void ResumeLoop(string id);
void StopLoop(string id, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT);
bool TryGetEventInstance(string id, out EventInstance instance);
~~~

## Runtime Behavior

### BGM transitions

PlayBGM starts the incoming BGM and fades it in while the previous BGM fades out. There is no built-in delay parameter. Delay the caller before invoking PlayBGM or StopBGM when delayed behavior is needed.

Fade timing uses unscaled delta time, so BGM transitions continue while Time.timeScale is zero. Starting another BGM operation cancels the previous transition and transfers cleanup ownership safely.

### Managed loops

PlayLoop tracks an EventInstance by ID. Calling it again with an active ID returns the existing instance.

The FMOD event itself must be configured as a looping event in FMOD Studio. PlayLoop manages the instance lifecycle but does not turn a one-shot FMOD event into a loop.

Use TryGetEventInstance when parameters need to be changed on a managed loop:

~~~csharp
if (audio.TryGetEventInstance("engine", out EventInstance engine))
{
    engine.setParameterByName("RPM", rpm);
}
~~~

### Failure behavior

- GetVolume returns -1 when the bus is invalid or FMOD rejects the operation.
- PlayLoop returns a default invalid EventInstance when creation or start fails.
- FMOD operation failures are written to the Unity Console with the RESULT code and description.
- Cleanup continues even if an individual stop, release, or unload operation fails.

## Editor Tools

All tools are under Tools/ThanhDV/Audio Conductor - FMOD.

FMOD reference data is stored at:

~~~
Assets/Plugins/AudioConductor/FMOD/Data/FMODReferences.json
~~~

Generated accessors are written to:

~~~
Assets/Plugins/AudioConductor/FMOD/Scripts/FMODBus.cs
Assets/Plugins/AudioConductor/FMOD/Scripts/FMODEventReference.cs
~~~

### Bus Manager

Open Tools/ThanhDV/Audio Conductor - FMOD/Bus Manager.

- Enter a friendly **Key** and an FMOD **Bus Path** such as bus:/ or bus:/SFX.
- **Clean & Save** removes invalid or duplicate entries, saves the JSON data, and regenerates FMODBus.
- **Load All Buses** reads buses from the built FMOD banks and replaces the current unsaved list after confirmation.

~~~csharp
audio.SetVolume(FMODBus.SFX, 0.8f);
~~~

### EventReference Manager

Open Tools/ThanhDV/Audio Conductor - FMOD/EventReference Manager.

- Enter a friendly **Key** and select or paste an **EventReference**.
- **Clean & Save** removes invalid or duplicate entries, saves the JSON data, and regenerates FMODEventReference.
- **Load All EventReferences** reads events from the built FMOD banks and replaces the current unsaved list after confirmation.

~~~csharp
audio.PlayOneShot(FMODEventReference.UI_Click);
~~~

### Volume Debugger

Open Tools/ThanhDV/Audio Conductor - FMOD/Volume Debugger.

Volume Debugger works in Play Mode. It loads the bus list from FMODReferences.json, displays the current bus volumes, and allows them to be adjusted at runtime.

### Generated names

Generated property names come from **Key** values. Invalid characters and spaces become underscores. Duplicate names receive numeric suffixes such as _2 or _3.

FMODBus and FMODEventReference accessors can be passed directly to IAudioConductor.

## Dependency Injection

FMODConductor is a regular C# class and does not inherit MonoBehaviour. It still depends on UnityEngine and the FMOD Unity API because this package is FMOD-specific.

**How it works:** Register FMODConductor as a singleton implementation of IAudioConductor. Registration syntax depends on the DI framework.

**Example (using a generic DI container):**

~~~csharp
// Pseudocode: use the equivalent registration API in your DI framework.
container.RegisterSingleton<IAudioConductor, FMODConductor>();
~~~

The container should own and dispose the singleton because IAudioConductor implements IDisposable. When constructing it manually, dispose it explicitly:

~~~csharp
IAudioConductor audio = new FMODConductor();

try
{
    audio.PlayOneShot("event:/UI/Click");
}
finally
{
    audio.Dispose();
}
~~~

## License

This package is available under the MIT License. See LICENSE.md.
