using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IAssistantResponsePresentationFormatter
{
    AssistantResponsePresentation? Format(AssistantResponse response);
}
