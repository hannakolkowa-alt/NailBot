using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Helpers
{
    public static class WebhookHelper
    {
        public static async Task<Update?> ReadUpdateAsync(HttpRequest request, CancellationToken ct)
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(body))
            {
                Console.WriteLine("Webhook: пустое тело запроса");
                return null;
            }

            try
            {
                var update = JsonSerializer.Deserialize<Update>(body, JsonBotAPI.Options);
                if (update == null)
                    Console.WriteLine($"Webhook: Update=null, body начало: {body[..Math.Min(120, body.Length)]}");
                else
                    Console.WriteLine($"Webhook: type={update.Type}, msg={update.Message?.Text}, cb={update.CallbackQuery?.Data}");
                return update;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Webhook deserialize error: {ex.Message}");
                return null;
            }
        }
    }
}
