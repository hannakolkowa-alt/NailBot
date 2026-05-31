using Telegram.Bot;
using Telegram.Bot.AspNetCore;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramBot;
using TelegramBot.Data;
using TelegramBot.Handlers;
using TelegramBot.Helpers;
using TelegramBot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var supabaseUrl = builder.Configuration["SupabaseUrl"]?.Trim();
var supabaseKey = builder.Configuration["SupabaseKey"]?.Trim();
var botToken = builder.Configuration["TelegramBotToken"]?.Trim();

if (supabaseUrl?.EndsWith(".supabase.com", StringComparison.OrdinalIgnoreCase) == true)
{
    Console.WriteLine("⚠️ SupabaseUrl должен заканчиваться на .supabase.co (не .com)");
}

if (supabaseKey?.StartsWith("sb_publishable_", StringComparison.Ordinal) == true)
{
    Console.WriteLine("⚠️ SupabaseKey: для сервера нужен sb_secret_... или service_role (JWT), не publishable");
}

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

BotConfig.Configure(ParseMasterTelegramIds(builder.Configuration));
Console.WriteLine($"Мастер(а) Telegram ID: {string.Join(", ", BotConfig.MasterTelegramIds)}");

try
{
    await SupabaseConfig.InitializeAsync(supabaseUrl, supabaseKey);
}
catch (Exception ex)
{
    Console.WriteLine($"Предупреждение: Supabase недоступен ({ex.Message}). Бот всё равно запустится.");
}

try
{
    await DefaultCatalogSeed.EnsureSeedAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Предупреждение: не удалось заполнить каталог ({ex.Message}).");
}

builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(botToken));
builder.Services.ConfigureTelegramBotMvc();

// Render задаёт PORT (часто 10000). Не переопределяем HTTP_PORTS вручную — слушаем только PORT.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

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

var allowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery };

if (useWebhook)
{
    var webhookUrl = $"{webhookBase.TrimEnd('/')}/bot/webhook";
    await bot.SetWebhook(webhookUrl, allowedUpdates: allowedUpdates);
    var webhookInfo = await bot.GetWebhookInfo();
    Console.WriteLine($"Webhook установлен: {webhookUrl}");
    Console.WriteLine($"Webhook status: {webhookInfo.Url}, pending: {webhookInfo.PendingUpdateCount}, last error: {webhookInfo.LastErrorMessage ?? "нет"}");
}
else
{
    await bot.DeleteWebhook(dropPendingUpdates: true);
    bot.StartReceiving(
        BotUpdateHandler.HandleUpdateAsync,
        BotUpdateHandler.HandlePollingErrorAsync,
        new ReceiverOptions { AllowedUpdates = allowedUpdates });
    Console.WriteLine("Локальный режим: long polling (не запускайте одновременно с Render).");
}

app.MapMethods("/", new[] { "GET", "HEAD" }, () => Results.Ok("Telegram bot is running"));
app.MapMethods("/health", new[] { "GET", "HEAD" }, () => Results.Ok("ok"));

if (useWebhook)
{
    app.MapPost("/bot/webhook", async (HttpRequest request, ITelegramBotClient botClient, CancellationToken ct) =>
    {
        var update = await WebhookHelper.ReadUpdateAsync(request, ct);
        if (update == null)
            return Results.Ok();

        try
        {
            await BotUpdateHandler.HandleUpdateAsync(botClient, update, ct);
        }
        catch (Exception ex)
        {
            await BotUpdateHandler.HandleErrorAsync(botClient, update, ex, ct);
        }

        return Results.Ok();
    });
}

Console.WriteLine($"HTTP-сервер слушает порт {port}");
app.Run();

static IEnumerable<long> ParseMasterTelegramIds(IConfiguration config)
{
    var raw = config["MasterTelegramId"]
        ?? config["MasterTelegramIds"]
        ?? config["AdminTelegramId"];

    if (string.IsNullOrWhiteSpace(raw))
        return new[] { 5783971965L };

    return raw.Split(',', ';', ' ', '\n', '\r')
        .Select(part => long.TryParse(part.Trim(), out var id) ? id : 0)
        .Where(id => id > 0);
}
