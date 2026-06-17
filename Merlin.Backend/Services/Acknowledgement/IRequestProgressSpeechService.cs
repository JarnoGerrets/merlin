namespace Merlin.Backend.Services.Acknowledgement;

public interface IRequestProgressSpeechService
{
    IRequestProgressSpeechHandle Start(RequestProgressSpeechRequest request, CancellationToken cancellationToken);
}

public interface IRequestProgressSpeechHandle : IAsyncDisposable
{
    void MarkMainResponseReady();

    Task StopAsync();
}
