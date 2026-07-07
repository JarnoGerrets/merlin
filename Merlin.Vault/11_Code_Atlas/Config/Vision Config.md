---
type: config
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Vision Config

| Config Key / Constant | Default | Current Value | Used By | Effect | Profile-specific later? |
| --- | --- | --- | --- | --- | --- |
| `Vision:Backend` | Auto | appsettings/Development | VisionSidecarHost/vision_worker.py | OpenCV backend/profile selection | yes |
| `Vision:CaptureProfile` | Auto | appsettings/Development | vision_worker.py | DSHOW/MSMF/MJPG profile choice | yes |
| `Vision:Width/Height/Fps` | varies | appsettings | VisionSidecarHost | Camera target resolution/fps | yes |
| `Vision:MirrorPreview` | true/false | appsettings | vision_worker.py | Mirror mapping/preview | yes |
