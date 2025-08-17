# AudioManager (FMOD Integration)

Lightweight central audio controller for Unity + FMOD. Handles one‑shots, music transitions, and managed looping sounds with safe async fade logic.

## Features
* One‑shot SFX (2D & 3D overloads)
* BGM play / stop with fade, crossfade (delay=0) or sequential (delay >= fadeDuration)
* Cancellation-safe transitions (new call cancels current fade task)
* Loop sounds tracked by ID (optional GameObject attachment for 3D positioning)
* Automatic cleanup on destroy (stop + release)
* Control volume by channel
* Lazy singleton (`AudioManager.Instance`) with `DontDestroyOnLoad`

## Quick Use
```csharp
void SetVolume(AudioType type, float volume);
float GetVolume(AudioType type);

void PlayOneShot(EventReference reference);
void PlayOneShot(string path);
void PlayOneShot(EventReference reference, Vector3 pos);
void PlayOneShot(string path, Vector3 pos);

void PlayBGM(EventReference reference, float fadeDuration = 1f, float delay = 0f);
void PlayBGM(string path, float fadeDuration = 1f, float delay = 0f);
void StopBGM(float fadeDuration = 1f, float delay = 0f);

void PlayLoop(string id, string path, GameObject attachedObject = null);
void PlayLoop(string id, EventReference reference, GameObject attachedObject = null);
void PauseLoop(string id);
void ResumeLoop(string id);
void StopLoop(string id, STOP_MODE stopMode = STOP_MODE.ALLOWFADEOUT);
```

