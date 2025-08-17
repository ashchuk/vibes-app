using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Vibes.API.Configuration;
using Vibes.API.Services;

namespace Vibes.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BotController(IOptions<BotConfiguration> config, ILogger<BotController> logger) : ControllerBase
{
    [HttpGet("implicitlySetWebhook")]
    public async Task<string> SetWebHookImplicitly([FromServices] ITelegramBotClient bot, CancellationToken ct)
    {
        var webhookUrl = config.Value.BotWebhookUrl.AbsoluteUri;
        logger.LogInformation("Implicit: setting webhook URL to be {URL}", webhookUrl);
        await bot.SetWebhook(webhookUrl, allowedUpdates: [], secretToken: config.Value.SecretToken, cancellationToken: ct);
        return $"Webhook set to {webhookUrl}";
    }

    [HttpGet("explicitlySetWebhook")]
    public async Task<string> SetWebHookExplicitly([FromServices] ITelegramBotClient bot, [FromQuery] string url, CancellationToken ct)
    {
        var decodedUrl = System.Web.HttpUtility.UrlDecode(url);
        logger.LogInformation("Explicit: setting webhook URL to be {URL}", decodedUrl);
        await bot.SetWebhook(url, allowedUpdates: [], secretToken: config.Value.SecretToken, cancellationToken: ct);
        return $"Webhook set to {url}";
    }
    
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] Update update,
        [FromServices] ITelegramBotClient bot,
        [FromServices] TelegramMessageHandler handleUpdateService,
        CancellationToken ct)
    {
        if (Request.Headers["X-Telegram-Bot-Api-Secret-Token"] != config.Value.SecretToken)
            return Forbid();
        try
        {
            await handleUpdateService.HandleUpdateAsync(bot, update, ct);
        }
        catch (Exception exception)
        {
            await handleUpdateService.HandleErrorAsync(bot, exception, Telegram.Bot.Polling.HandleErrorSource.HandleUpdateError, ct);
        }
        return Ok();
    }
    
    [HttpGet("/healthz")]
    public IActionResult Liveness()
    {
        // Basic liveness check - just needs to return 200 OK
        return Ok("Healthy");
    }

    [HttpGet("/readyz")]
    public IActionResult Readiness()
    {
        // Basic readiness - can be extended to check dependencies if needed
        return Ok("Ready");
    }
}
