using Merlin.Backend.Next.Kernel.Events;
using Merlin.Backend.Next.Kernel.Requests;
using Merlin.Backend.Next.Kernel.Routing;
using Merlin.Backend.Next.Kernel.Surfaces;

namespace Merlin.Backend.Next.Kernel.Turns;

public sealed class MerlinTurnContext
{
    private readonly List<MerlinEvent> _events = [];

    public MerlinTurnContext(
        string turnId,
        MerlinRequest request,
        CancellationToken cancellationToken = default)
    {
        TurnId = string.IsNullOrWhiteSpace(turnId)
            ? Guid.NewGuid().ToString("N")
            : turnId;
        Request = request;
        CancellationToken = cancellationToken;
    }

    public string TurnId { get; }

    public MerlinRequest Request { get; }

    public SurfaceSnapshot? Surface { get; private set; }

    public RouteDecision? Route { get; private set; }

    public CancellationToken CancellationToken { get; }

    public IReadOnlyList<MerlinEvent> Events => _events;

    public void SetSurface(SurfaceSnapshot surface) => Surface = surface;

    public void SetRoute(RouteDecision route) => Route = route;

    public void AddEvent(MerlinEvent evt) => _events.Add(evt);
}
