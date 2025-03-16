using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Infrastructure;

public sealed class EmailHandling(IConfiguration configuration) : IEmailHandling
{
    public string LoadTemplate(BuildedEmail buildedEmail)
    {
        var templateName = GetTemplateNameByType(buildedEmail.Type);
        if (string.IsNullOrEmpty(templateName))
            throw new Exception();

        var template = GetTemplate(templateName);

        var mail = ReplaceParameters(template, buildedEmail.Parameters);

        return mail;
    }

    public async Task SendAsync(BuildedEmail buildedEmail, string email)
    {
        var key =
            Environment.GetEnvironmentVariable("RESEND_KEY")
            ?? configuration["Settings:RESEND_KEY"]
            ?? throw new NullReferenceException("RESEND_KEY");

        var sender =
            Environment.GetEnvironmentVariable("EMAIL_SENDER_NAME")
            ?? configuration["Settings:EMAIL_SENDER_NAME"]
            ?? throw new NullReferenceException("EMAIL_SENDER_NAME");

        var alias =
            Environment.GetEnvironmentVariable("EMAIL_SENDER_ALIAS")
            ?? configuration["Settings:EMAIL_SENDER_ALIAS"]
            ?? throw new NullReferenceException("EMAIL_SENDER_ALIAS");

        using var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");

        var payload = new
        {
            from = $"{sender} <{alias}>",
            to = new string[] { $"{buildedEmail.Name} <{buildedEmail.Destination}>" },
            subject = buildedEmail.Subject,
            html = email,
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("https://api.resend.com/emails", content);

        if (response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Successfully sent email: {responseString}");
        }
        else
        {
            Console.WriteLine($"Failed to send email: {response.StatusCode}");
        }
    }

    private static string ReplaceParameters(string template, IDictionary<string, string> parameters)
    {
        foreach (var param in parameters)
        {
            if (!template.Contains("{{"))
                break;

            template = template.Replace("{{" + param.Key + "}}", parameters[param.Key]);
        }

        return template;
    }

    private static string GetTemplate(string path)
    {
        var fileStream = new FileStream($"./EmailTemplates/{path}", FileMode.Open);
        using StreamReader reader = new StreamReader(fileStream);
        return reader.ReadToEnd();
    }

    private static string GetTemplateNameByType(EmailType type)
    {
        string templateName = "";
        switch (type)
        {
            case EmailType.Empty:
                templateName = "Empty.html";
                break;
            case EmailType.NovoInteressadoMentoria:
                templateName = "NovoInteressadoMentoria.html";
                break;

            case EmailType.AlunoAceitoNaMentoria:
                templateName = "AlunoAceitoNaMentoria.html";
                break;
            default:
                break;
        }
        return templateName;
    }
}
