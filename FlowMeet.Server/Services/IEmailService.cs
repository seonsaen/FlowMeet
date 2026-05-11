namespace FlowMeet.Server.Services;

public interface IEmailService
{
    Task<bool> SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default);
}
