using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Helpers
{
    public static class GalleryPhotoSender
    {
        public static async Task SendAsync(ITelegramBotClient bot, long chatId, string? photoUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(photoUrl))
                return;

            try
            {
                if (photoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || photoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    await bot.SendPhoto(chatId, photoUrl, cancellationToken: ct);
                    return;
                }

                await bot.SendPhoto(chatId, InputFile.FromFileId(photoUrl), cancellationToken: ct);
            }
            catch
            {
                await bot.SendMessage(chatId, "🖼 Не удалось показать фото.", cancellationToken: ct);
            }
        }

        public static async Task SendManyAsync(ITelegramBotClient bot, long chatId, IEnumerable<string> urls, CancellationToken ct, int? maxCount = null)
        {
            var list = urls.Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
            if (maxCount.HasValue)
                list = list.Take(maxCount.Value).ToList();

            foreach (var url in list)
                await SendAsync(bot, chatId, url, ct);
        }
    }
}
