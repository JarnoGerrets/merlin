---
type: prompt-extension
id: PE-0120
status: active
applies_to:
  - browserhost
required_for:
  - browserhost change
---

# PE-0120 BrowserHost Change Rules

## Required Reading

- [[BrowserHost Architecture]]
- [[BrowserWorkspaceForm]]
- [[NativeBrowserPointerOverlayWindow]]
- [[NativeBrowserInputService]]
- [[Backend BrowserHost Commands]]

## Rules

1. Preserve stdin/stdout command protocol compatibility.
2. Preserve WebView2 lifecycle behavior.
3. Do not break native overlay transparency/click-through behavior.
4. Be careful with DPI, z-order, focus, and multi-monitor assumptions.
5. Do not add unsafe native input behavior without safety review.
