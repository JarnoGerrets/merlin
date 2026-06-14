# Merlin.OrbLab

OrbLab is a lightweight Godot project for testing Merlin's orb visuals without running the backend, STT, LLM, or TTS pipeline.

It uses junctions to the live frontend orb files:

- `Scripts` -> `../Merlin.Frontend/Scripts`
- `LiveScenes` -> `../Merlin.Frontend/Scenes`

That means visual changes to the live orb are immediately visible in OrbLab.

## Run From Godot

1. Open Godot Project Manager.
2. Click `Import`.
3. Select `Merlin.OrbLab/project.godot`.
4. Open the project.
5. Press `F5` or click Run.

## Controls

- `Idle`, `Listening`, `Thinking`, `Speaking`, `Confirm`, `Error` switch orb states.
- `Speech Tick` sends one simulated speech-energy event.
- `Auto Speech` continuously sends simulated speech ticks.
- `Energy` controls the simulated speech energy sent by the speech tick controls.

No backend is required.
