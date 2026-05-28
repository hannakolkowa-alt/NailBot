using Microsoft.AspNetCore.Http.Json;
using Telegram.Bot;
using Telegram.Bot.AspNetCore;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Handlers;
using TelegramBot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var supabaseUrl = builder.Configuration["SupabaseUrl"];
var supabaseKey = builder.Configuration["SupabaseKey"];
var botToken = builder.Configuration["TelegramBotToken"];

if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
{
    Console.WriteLine("Ошибка: задайте SupabaseUrl и SupabaseKey.");
    return;
}

if (string.IsNullOrWhiteSpace(botToken))
{
    Console.WriteLine("Ошибка: задайте TelegramBotToken.");
    return;
}

await SupabaseConfig.InitializeAsync(supabaseUrl, supabaseKey);

builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(botToken));
builder.Services.ConfigureTelegramBot<JsonOptions>(opt => opt.SerializerOptions);

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();
var bot = app.Services.GetRequiredService<ITelegramBotClient>();

try
{
    var me = await bot.GetMe();
    Console.WriteLine($"Бот запущен: @{me.Username} | ID: {me.Id}");
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка Telegram API: {ex.Message}");
    return;
}

// На Render free: RENDER_EXTERNAL_URL задаётся автоматически → webhook
// Локально без URL — long polling (для разработки)
var webhookBase = builder.Configuration["RENDER_EXTERNAL_URL"]
    ?? builder.Configuration["WebhookUrl"];
var useWebhook = !string.IsNullOrWhiteSpace(webhookBase);

if (useWebhook)
{
    var webhookUrl = $"{webhookBase.TrimEnd('/')}/bot/webhook";
    await bot.SetWebhook(webhookUrl, allowedUpdates: []);
    Console.WriteLine($"Webhook установлен: {webhookUrl}");
}
else
{
    await bot.DeleteWebhook(dropPendingUpdates: true);
    bot.StartReceiving(
        BotUpdateHandler.HandleUpdateAsync,
        BotUpdateHandler.HandleErrorAsync,
        new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() });
    Console.WriteLine("Локальный режим: long polling (не запускайте одновременно с Render).");
}

app.MapGet("/", () => Results.Ok("Telegram bot is running"));
app.MapGet("/health", () => Results.Ok("ok"));

if (useWebhook)
{
    app.MapPost("/bot/webhook", async (Update update, ITelegramBotClient botClient, CancellationToken ct) =>
    {
        try
        {
            await BotUpdateHandler.HandleUpdateAsync(botClient, update, ct);
        }
        catch (Exception ex)
        {
            await BotUpdateHandler.HandleErrorAsync(botClient, ex, ct);
        }

        return Results.Ok();
    });
}

Console.WriteLine($"HTTP-сервер слушает порт {port}");
app.Run();
