using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using Azure.AI.OpenAI;
using ZavaStorefront.Services;

namespace ZavaStorefront.Controllers;

public class ChatController : Controller
{
    private readonly ContentSafetyService _safety;
    private readonly IConfiguration _config;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ContentSafetyService safety, IConfiguration config, ILogger<ChatController> logger)
    {
        _safety = safety;
        _config = config;
        _logger = logger;
    }

    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> Send(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Json(new { reply = "Please enter a message." });

        // Content safety check
        var safetyResult = await _safety.EvaluateAsync(message);
        if (!safetyResult.IsSafe)
        {
            _logger.LogWarning("ContentSafety | Prompt blocked. Category={Category} Severity={Severity}",
                safetyResult.BlockedCategory, safetyResult.MaxSeverity);
            return Json(new { reply = $"⚠️ Your message was blocked due to unsafe content ({safetyResult.BlockedCategory}). Please rephrase." });
        }

        // Forward to model
        try
        {
            var endpoint = _config["AIServices:Endpoint"]!;
            var deployment = _config["AIServices:DeploymentName"]!;

            var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
            var chatClient = client.GetChatClient(deployment);

            var result = await chatClient.CompleteChatAsync(
                new SystemChatMessage("You are a helpful assistant for Zava Storefront, a retail shop."),
                new UserChatMessage(message)
            );

            return Json(new { reply = result.Value.Content[0].Text });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat model call failed");
            return Json(new { reply = "Sorry, the AI model is currently unavailable. Please try again later." });
        }
    }
}
