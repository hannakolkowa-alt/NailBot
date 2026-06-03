using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Helpers;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class GalleryAdminFlow
    {
        public static async Task ShowMenuAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var master = await MasterService.GetMasterProfileAsync();
            if (master == null)
            {
                await bot.SendMessage(chatId,
                    "Сначала создайте профиль («Мой профиль»).",
                    replyMarkup: Keyboards.CreateAdminMenuKeyboard(),
                    cancellationToken: ct);
                return;
            }

            var photos = await GalleryService.GetMasterPhotosAsync(master.MasterId);
            var session = SessionStore.GetOrCreate(chatId);
            session.CachedGalleryPhotoIds = photos.Select(p => p.PhotoId).ToList();
            session.State = SessionState.Idle;

            var countLine = photos.Count == 0
                ? "Пока нет фотографий."
                : $"В галерее: {photos.Count} фото.";

            await bot.SendMessage(chatId, $"🖼 Галерея (портфолио для клиентов)\n{countLine}", cancellationToken: ct);

            var kb = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить фото", "gal_add") },
                new[] { InlineKeyboardButton.WithCallbackData("🗑 Удалить фото", "gal_del_menu") }
            });
            await bot.SendMessage(chatId, "Управление галереей:", replyMarkup: kb, cancellationToken: ct);
        }

        public static async Task BeginAddPhotoAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            session.State = SessionState.Admin_Gallery_WaitPhoto;

            var doneKb = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "gal_done") }
            });

            await bot.SendMessage(chatId,
                "Отправьте одно или несколько фото сообщением.\nКогда закончите — нажмите «Готово» или откройте «Галерея» снова.",
                replyMarkup: doneKb,
                cancellationToken: ct);
        }

        public static async Task<bool> TryHandlePhotoAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
        {
            if (message.Photo is not { Length: > 0 })
                return false;

            var chatId = message.Chat.Id;
            var userId = message.From?.Id ?? chatId;
            if (!RoleHelper.IsMasterAccount(userId) || !RoleHelper.ActAsMaster(chatId, userId))
                return false;

            var session = SessionStore.GetOrCreate(chatId);
            if (session.State != SessionState.Admin_Gallery_WaitPhoto)
                return false;

            var master = await MasterService.GetMasterProfileAsync();
            if (master == null)
            {
                session.State = SessionState.Idle;
                await bot.SendMessage(chatId, "Профиль мастера не найден.", cancellationToken: ct);
                return true;
            }

            var largest = message.Photo.OrderByDescending(p => p.FileSize ?? 0).First();
            var fileId = largest.FileId;

            var ok = await GalleryService.AddPhotoAsync(master.MasterId, fileId);
            if (!ok)
            {
                await bot.SendMessage(chatId, "Не удалось сохранить фото. Проверьте таблицу gallery в Supabase.", cancellationToken: ct);
                return true;
            }

            var photos = await GalleryService.GetMasterPhotosAsync(master.MasterId);
            session.CachedGalleryPhotoIds = photos.Select(p => p.PhotoId).ToList();

            await bot.SendMessage(chatId, $"✅ Фото добавлено ({photos.Count} в галерее). Можно отправить ещё.", cancellationToken: ct);
            return true;
        }

        public static async Task ShowDeletePickerAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var master = await MasterService.GetMasterProfileAsync();
            if (master == null)
            {
                await bot.SendMessage(chatId, "Профиль мастера не найден.", cancellationToken: ct);
                return;
            }

            var photos = await GalleryService.GetMasterPhotosAsync(master.MasterId);
            var session = SessionStore.GetOrCreate(chatId);
            session.State = SessionState.Idle;

            if (!photos.Any())
            {
                await bot.SendMessage(chatId, "Нет фото для удаления.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                return;
            }

            session.CachedGalleryPhotoIds = photos.Select(p => p.PhotoId).ToList();
            var rows = photos.Select((p, i) =>
            {
                var label = $"❌ Фото #{i + 1}";
                if (!string.IsNullOrWhiteSpace(p.Description) && p.Description.Length <= 40)
                    label = $"❌ {p.Description}";
                if (label.Length > 64)
                    label = label[..61] + "…";
                return new[] { InlineKeyboardButton.WithCallbackData(label, $"gal_del:{i}") };
            }).ToList();

            await bot.SendMessage(chatId, "Выберите фото для удаления:",
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct);
        }

        public static async Task ShowPortfolioForClientAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
        {
            var kb = Keyboards.GetMenuForUser(chatId, userId);
            var master = await MasterService.GetMasterProfileAsync();
            if (master == null)
            {
                await bot.SendMessage(chatId, "Портфолио скоро появится.", replyMarkup: kb, cancellationToken: ct);
                return;
            }

            var photos = await GalleryService.GetMasterPhotosAsync(master.MasterId);
            if (!photos.Any())
            {
                await bot.SendMessage(chatId, "Портфолио пока пусто. Мастер ещё не добавил работы.", replyMarkup: kb, cancellationToken: ct);
                return;
            }

            await bot.SendMessage(chatId, $"🖼 Портфолио мастера ({photos.Count} фото):", cancellationToken: ct);
            await GalleryPhotoSender.SendManyAsync(bot, chatId, photos.Select(p => p.PhotoUrl), ct);
            await bot.SendMessage(chatId, "Выберите пункт меню 👇", replyMarkup: kb, cancellationToken: ct);
        }
    }
}
