# Memory Layer Handoff

The SQLite + EF Core persistence foundation is ready.

Do not replace SQLite, EF Core, or the AppData database path. Build the brain-like memory architecture on top of the store interfaces and do not inject `MerlinDbContext` directly into memory behavior services.

Available interfaces:

- `IMemoryStore`
- `IConceptStore`
- `IConversationStateStore`
- `ITurnStateStore`
- `IPromptCompilationStore`
- `IMemorySearchService`
- `IConceptExtractionService`
- `IConversationRuntimeState`
- `IAssistantTurnTracker`
- `IPromptCompilationLogger`
- `ITokenEstimator`

The next layer should implement current conversation memory, topic boundary detection, medium memory behavior, long-term memory behavior, memory writing and promotion, associative retrieval, concept graph traversal, and the real MemoryCompiler on top of these stores.

The persistence layer already stores assistant turn IDs, original user messages, generated text so far, spoken text so far, interruption metadata, and prompt compilations. That data is ready for a later DeepInfra-aware interruption system, but interruption behavior itself is intentionally not implemented here.
