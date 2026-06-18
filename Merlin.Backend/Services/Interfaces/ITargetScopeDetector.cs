using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ITargetScopeDetector
{
    TargetScopeDetectionResult Detect(string userText);
}
