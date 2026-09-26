namespace Surbibor.Infrastructure;

public class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Public origin of the frontend; used for CORS and for links in emails.</summary>
    public string Origin { get; set; } = "http://localhost:5173";
}
