using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Flows;
using TelegramBot.Services;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class ClientHandler
    {
        public static async Task HandleAsync(ITelegramBotClient botClient, long chatId, long userId, string text, CancellationToken ct)
        {
            var kb = Keyboards.CreateMainMenuKeyboard();

            switch (text)
            {
                case "◀️ меню":
                    await botClient.SendMessage(chatId, "Главное меню 👇", replyMarkup: kb, cancellationToken: ct);
                    break;

                case "галерея":
                case "о мастере":
                    var profile = await MasterService.GetMasterProfileAsync();
                    if (profile == null)
                    {
                        await botClient.SendMessage(chatId, "Информация о мастере скоро появится.", replyMarkup: kb, cancellationToken: ct);
                        break;
                    }
                    var nick = string.IsNullOrEmpty(profile.TelegramUsername) ? "" : $"@{profile.TelegramUsername.TrimStart('@')}";
                    var info = $"✨ О мастере\n\nИмя: {profile.Name}\nНик: {nick}\nОпыт: {profile.Experience}\n\n{profile.Description}";
                    await botClient.SendMessage(chatId, info, replyMarkup: kb, cancellationToken: ct);

                    var master = profile;
                    var photos = await GalleryService.GetMasterPhotosAsync(master.MasterId);
                    foreach (var photo in photos.Take(5))
                    {
                        if (!string.IsNullOrEmpty(photo.PhotoUrl))
                        {
                            try { await botClient.SendPhoto(chatId, photo.PhotoUrl, cancellationToken: ct); }
                            catch { await botClient.SendMessage(chatId, $"🖼 {photo.Description ?? photo.PhotoUrl}", cancellationToken: ct); }
                        }
                    }
                    break;

                case "прайс":
                case "записаться":
                case "услуги":
                    await BookingFlow.StartAsync(botClient, chatId, ct);
                    break;

                case "график":
                case "расписание":
                    var now = DateTime.Now;
                    var schedule = await ScheduleService.FormatMonthScheduleAsync(now.Year, now.Month);
                    await botClient.SendMessage(chatId, schedule, replyMarkup: kb, cancellationToken: ct);
                    break;

                case "мои записи":
                case "записи":
                    await ShowClientRecordsAsync(botClient, chatId, userId, ct);
                    break;

                default:
                    await botClient.SendMessage(chatId, "Выберите пункт меню 👇", replyMarkup: kb, cancellationToken: ct);
                    break;
            }
        }

        private static async Task ShowClientRecordsAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
        {
            var requests = await RequestService.GetByClientTelegramIdAsync(userId);
            var session = State.SessionStore.GetOrCreate(chatId);
            session.CachedRequestIds = requests.Select(r => r.RequestId).ToList();

            if (!requests.Any())
            {
                await bot.SendMessage(chatId, "У вас нет активных записей.", replyMarkup: Keyboards.CreateMainMenuKeyboard(), cancellationToken: ct);
                return;
            }

            foreach (var (req, i) in requests.Select((r, idx) => (r, idx)))
            {
                var services = await CatalogService.GetRequestServicesAsync(req.RequestId);
                var svcNames = string.Join(", ", services.Select(s => s.Name));
                var msg = $"📋 Запись #{i + 1}\nСтатус: {req.Status}\nУслуги: {svcNames}\nДата: {req.DesiredDate:dd.MM.yyyy} {req.DesiredTime:HH:mm}\n{req.Comment}";

                var rows = new List<InlineKeyboardButton[]>
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Отмена", $"rec_can:{i}"),
                        InlineKeyboardButton.WithCallbackData("Изменить", $"rec_chg:{i}")
                    }
                };
                await bot.SendMessage(chatId, msg, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            }
        }
    }
}
