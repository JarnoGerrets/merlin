using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ICorrectionRequestBuilder
{
    CorrectionRequestBuildResult Build(CorrectionRequestBuildInput input);
}

public sealed record CorrectionRequestBuildInput(
    string CorrectionText,
    string OriginalCorrelationId,
    AssistantRequest? PreviousRequest);

public sealed record CorrectionRequestBuildResult(
    AssistantRequest Request,
    string Strategy,
    string? PreviousRequest,
    string CorrectionText,
    string OriginalCorrelationId,
    string NewCorrelationId);
