namespace Merlin.Backend.Services.Acknowledgement;

public interface IAcknowledgementPolicy
{
    AcknowledgementDecision Decide(AcknowledgementContext context);
}
