---
type: prompt-extension
id: PE-0170
status: active
applies_to:
  - browser workspace
required_for:
  - browser workspace change
---

# PE-0170 Browser Workspace Change Rules

## Required Reading

- [[Browser Workspace]]
- [[Browser Workspace Architecture]]
- [[BrowserWorkspaceService]]
- [[BrowserWorkspaceForm]]
- [[Browser Page Action Safety Flow]]

## Rules

1. Preserve BrowserHost process communication.
2. Preserve browser state updates and ActiveSurface metadata.
3. Page-aware control must use DOM/page structure before OCR.
4. Risky page actions must pass safety/confirmation.
5. Native pointer/click/scroll behavior must not regress.
