Merlin Structured Response Renderer

Goal:
When DeepInfra returns mixed content, Merlin should split it into:
1. spoken narrative
2. visual code blocks
3. visual tables
4. optional UI cards/lists

Example:
Assistant response contains:
- explanation paragraph
- code block
- follow-up explanation

Merlin should:
- speak the explanation naturally
- show the code block in the UI as formatted code
- not read the entire code block aloud unless the user explicitly asks
- continue speaking the explanation after the code block

Acceptance criteria:
- code fences are detected
- code blocks are preserved for UI
- TTS receives a speech-safe placeholder
- UI receives structured code block payload
- raw markdown symbols are not spoken