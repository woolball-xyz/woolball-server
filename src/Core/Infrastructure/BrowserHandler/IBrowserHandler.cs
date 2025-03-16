namespace Infrastructure.Browser;

public interface IBrowserHandler
{
    Task<string> BuildImageFromHtml(string html, string target);
}
