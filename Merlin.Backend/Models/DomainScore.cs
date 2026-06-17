namespace Merlin.Backend.Models;

public sealed record DomainScore(
    IntentDomain Domain,
    double Score,
    string Reason);
