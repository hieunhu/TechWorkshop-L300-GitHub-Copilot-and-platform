using Azure;
using Azure.AI.ContentSafety;
using Azure.Identity;

namespace ZavaStorefront.Services;

public class ContentSafetyResult
{
    public bool IsSafe { get; set; }
    public string? BlockedCategory { get; set; }
    public int MaxSeverity { get; set; }
}

public class ContentSafetyService
{
    private readonly ContentSafetyClient _client;
    private readonly ILogger<ContentSafetyService> _logger;
    private const int SafetyThreshold = 2;

    public ContentSafetyService(IConfiguration config, ILogger<ContentSafetyService> logger)
    {
        var endpoint = config["AIServices:Endpoint"]!;
        var clientId = config["AIServices:ManagedIdentityClientId"];
        var credential = clientId != null
            ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId })
            : new DefaultAzureCredential();
        _client = new ContentSafetyClient(new Uri(endpoint), credential);
        _logger = logger;
    }

    public async Task<ContentSafetyResult> EvaluateAsync(string text)
    {
        var request = new AnalyzeTextOptions(text);
        AnalyzeTextResult response = await _client.AnalyzeTextAsync(request);

        int maxSeverity = 0;
        string? blockedCategory = null;

        foreach (var category in response.CategoriesAnalysis)
        {
            int severity = category.Severity ?? 0;
            _logger.LogInformation("ContentSafety | Category={Category} Severity={Severity}", category.Category, severity);

            if (severity > maxSeverity)
                maxSeverity = severity;

            if (severity >= SafetyThreshold && blockedCategory == null)
                blockedCategory = category.Category.ToString();
        }

        bool isSafe = blockedCategory == null;
        _logger.LogInformation("ContentSafety | Result={Result} MaxSeverity={MaxSeverity} BlockedCategory={BlockedCategory}",
            isSafe ? "Safe" : "Blocked", maxSeverity, blockedCategory ?? "none");

        return new ContentSafetyResult
        {
            IsSafe = isSafe,
            BlockedCategory = blockedCategory,
            MaxSeverity = maxSeverity
        };
    }
}
