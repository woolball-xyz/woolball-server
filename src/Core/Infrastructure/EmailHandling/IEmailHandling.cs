using Contracts;
using Microsoft.AspNetCore.Http;

namespace Infrastructure;

public interface IEmailHandling
{
    string LoadTemplate(BuildedEmail buildedEmail);
    Task SendAsync(BuildedEmail buildedEmail, string email);
}
