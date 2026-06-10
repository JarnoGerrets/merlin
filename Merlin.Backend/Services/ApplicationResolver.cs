using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class ApplicationResolver : IApplicationResolver
{
    private static readonly HashSet<string> AmbiguousShortTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "vs",
        "visual",
        "studio",
        "office",
        "adobe"
    };

    private readonly ApplicationLaunchOptions _options;
    private readonly ITrustedApplicationStore _trustedApplicationStore;
    private string _lastResolutionStatus = string.Empty;

    public ApplicationResolver(
        IOptions<ApplicationLaunchOptions> options,
        ITrustedApplicationStore trustedApplicationStore)
    {
        _options = options.Value;
        _trustedApplicationStore = trustedApplicationStore;
    }

    public string LastResolutionStatus => Volatile.Read(ref _lastResolutionStatus);

    public Task<ApplicationResolutionResult> ResolveAsync(
        string applicationName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = NormalizeName(applicationName);

        if (string.IsNullOrWhiteSpace(query))
        {
            RecordStatus("Empty query");
            return Task.FromResult(NotFound());
        }

        var configuredCandidates = new List<ApplicationCandidate>();
        AddConfiguredCandidates(query, configuredCandidates);
        var exactConfigured = configuredCandidates
            .Where(candidate => candidate.Confidence >= 1)
            .OrderByDescending(candidate => candidate.Confidence)
            .ToArray();

        if (exactConfigured.Length > 0)
        {
            RecordStatus("Configured match");
            return Task.FromResult(new ApplicationResolutionResult
            {
                Found = true,
                RequiresConfirmation = false,
                IsAmbiguous = false,
                Message = "Configured application found.",
                Candidates = exactConfigured
            });
        }

        var trustedMapping = _trustedApplicationStore.FindByAlias(query);
        if (trustedMapping is not null)
        {
            RecordStatus("Trusted mapping match");
            return Task.FromResult(new ApplicationResolutionResult
            {
                Found = true,
                RequiresConfirmation = false,
                IsAmbiguous = false,
                Message = "Trusted application found.",
                Candidates =
                [
                    new ApplicationCandidate
                    {
                        DisplayName = trustedMapping.DisplayName,
                        ExecutablePath = trustedMapping.ExecutablePath,
                        Source = "Trusted",
                        Confidence = 1
                    }
                ]
            });
        }

        var candidates = new List<ApplicationCandidate>();
        candidates.AddRange(configuredCandidates);
        AddTrustedCandidates(query, candidates);
        AddStartMenuCandidates(query, candidates);
        AddPathCandidates(query, candidates);

        var orderedCandidates = candidates
            .GroupBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate.Confidence).First())
            .OrderByDescending(candidate => candidate.Confidence)
            .Take(5)
            .ToArray();

        if (orderedCandidates.Length == 0)
        {
            RecordStatus("No match");
            return Task.FromResult(NotFound());
        }

        var isAmbiguous = IsAmbiguous(query, orderedCandidates);
        if (isAmbiguous)
        {
            RecordStatus("Ambiguous match");
            return Task.FromResult(new ApplicationResolutionResult
            {
                Found = true,
                RequiresConfirmation = true,
                IsAmbiguous = true,
                Message = $"I found multiple applications matching '{applicationName}'. Which one did you mean?",
                Candidates = orderedCandidates
            });
        }

        RecordStatus(orderedCandidates.Any(candidate => candidate.Source is "Configured" or "Trusted")
            ? "Trusted/configured match"
            : "Untrusted discovered match");

        return Task.FromResult(new ApplicationResolutionResult
        {
            Found = true,
            RequiresConfirmation = orderedCandidates.Any(candidate => candidate.Source is not "Configured" and not "Trusted"),
            IsAmbiguous = false,
            Message = "Application candidate found.",
            Candidates = orderedCandidates
        });
    }

    private void AddTrustedCandidates(string query, List<ApplicationCandidate> candidates)
    {
        foreach (var mapping in _trustedApplicationStore.GetAll())
        {
            var score = Score(query, NormalizeName(mapping.Alias));
            if (score <= 0)
            {
                continue;
            }

            candidates.Add(new ApplicationCandidate
            {
                DisplayName = mapping.DisplayName,
                ExecutablePath = mapping.ExecutablePath,
                Source = "Trusted",
                Confidence = score
            });
        }
    }

    private void AddConfiguredCandidates(string query, List<ApplicationCandidate> candidates)
    {
        foreach (var application in _options.Applications)
        {
            var aliases = application.Value.Aliases.Append(application.Key);
            var score = aliases
                .Select(alias => Score(query, NormalizeName(alias)))
                .DefaultIfEmpty(0)
                .Max();

            if (score <= 0)
            {
                continue;
            }

            candidates.Add(new ApplicationCandidate
            {
                DisplayName = application.Value.DisplayName,
                ExecutablePath = application.Value.ExecutableOrUrl,
                Source = "Configured",
                Confidence = score
            });
        }
    }

    private static void AddStartMenuCandidates(string query, List<ApplicationCandidate> candidates)
    {
        foreach (var root in GetStartMenuRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var shortcut in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
            {
                var displayName = Path.GetFileNameWithoutExtension(shortcut);
                var score = Score(query, NormalizeName(displayName));
                if (score <= 0)
                {
                    continue;
                }

                candidates.Add(new ApplicationCandidate
                {
                    DisplayName = displayName,
                    ExecutablePath = shortcut,
                    Source = "StartMenu",
                    Confidence = score
                });
            }
        }
    }

    private static void AddPathCandidates(string query, List<ApplicationCandidate> candidates)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var executable in Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileNameWithoutExtension(executable);
                var displayName = ToDisplayName(fileName);
                var score = Math.Max(
                    Score(query, NormalizeName(fileName)),
                    Score(query, NormalizeName(displayName)));

                if (score <= 0)
                {
                    continue;
                }

                candidates.Add(new ApplicationCandidate
                {
                    DisplayName = displayName,
                    ExecutablePath = executable,
                    Source = "PATH",
                    Confidence = score
                });
            }
        }
    }

    private static IEnumerable<string> GetStartMenuRoots()
    {
        var programData = Environment.GetEnvironmentVariable("ProgramData");
        if (!string.IsNullOrWhiteSpace(programData))
        {
            yield return Path.Combine(programData, "Microsoft", "Windows", "Start Menu", "Programs");
        }

        var appData = Environment.GetEnvironmentVariable("AppData");
        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs");
        }
    }

    private static ApplicationResolutionResult NotFound()
    {
        return new ApplicationResolutionResult
        {
            Found = false,
            RequiresConfirmation = false,
            IsAmbiguous = false,
            Message = "No application candidate found.",
            Candidates = []
        };
    }

    private static bool IsAmbiguous(string query, IReadOnlyCollection<ApplicationCandidate> candidates)
    {
        if (candidates.Count <= 1)
        {
            return AmbiguousShortTerms.Contains(query)
                && candidates.Any(candidate => candidate.Source is not "Configured" and not "Trusted");
        }

        var topCandidates = candidates.Take(3).ToArray();
        return AmbiguousShortTerms.Contains(query)
            || topCandidates.Count(candidate => candidate.Confidence >= 0.75) > 1;
    }

    private void RecordStatus(string status)
    {
        Volatile.Write(ref _lastResolutionStatus, status);
    }

    private static double Score(string query, string candidate)
    {
        if (string.Equals(query, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (candidate.Contains(query, StringComparison.OrdinalIgnoreCase)
            || query.Contains(candidate, StringComparison.OrdinalIgnoreCase))
        {
            return 0.85;
        }

        if (candidate.EndsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0.75;
        }

        return 0;
    }


    private static string NormalizeName(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .ToLowerInvariant()
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ToDisplayName(string fileName)
    {
        if (string.Equals(fileName, "mspaint", StringComparison.OrdinalIgnoreCase))
        {
            return "Paint";
        }

        return fileName;
    }
}
