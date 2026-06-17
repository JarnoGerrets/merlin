namespace Merlin.Backend.Core.Memory.Services;

public static class MemoryDebugEndpoints
{
    public static IEndpointRouteBuilder MapMemoryDebugEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dev/memory");

        group.MapGet("/current", async (MemoryDebugService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCurrentStateAsync(cancellationToken)));

        group.MapGet("/memories", async (
            string? type,
            string? query,
            string? concept,
            int? limit,
            MemoryDebugService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListMemoriesAsync(type, query, concept, limit ?? 20, cancellationToken)));

        group.MapGet("/memories/{id}", async (string id, MemoryDebugService service, CancellationToken cancellationToken) =>
        {
            var detail = await service.GetMemoryDetailAsync(id, cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        group.MapDelete("/memories/{id}", async (string id, MemoryDebugService service, CancellationToken cancellationToken) =>
        {
            await service.DeleteMemoryAsync(id, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/concepts", async (int? limit, MemoryDebugService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListConceptsAsync(limit ?? 100, cancellationToken)));

        group.MapGet("/concepts/{id}", async (string id, MemoryDebugService service, CancellationToken cancellationToken) =>
        {
            var detail = await service.GetConceptDetailAsync(id, cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        group.MapPost("/retrieve", async (MemoryRetrieveDebugRequest request, MemoryDebugService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.RetrieveAsync(request.Query, request.MaxResults <= 0 ? 8 : request.MaxResults, cancellationToken)));

        group.MapPost("/compile-prompt", async (MemoryCompileDebugRequest request, MemoryDebugService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.CompilePromptAsync(request.Message, request.MaxInputTokens <= 0 ? 2500 : request.MaxInputTokens, cancellationToken)));

        group.MapGet("/prompt-compilations", async (int? limit, MemoryDebugService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListPromptCompilationsAsync(limit ?? 20, cancellationToken)));

        return app;
    }
}

public sealed record MemoryRetrieveDebugRequest(string Query, int MaxResults = 8);

public sealed record MemoryCompileDebugRequest(string Message, int MaxInputTokens = 2500);
