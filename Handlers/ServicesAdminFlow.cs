using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class ServicesAdminFlow
    {
        public static async Task ShowMenuAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            await CatalogService.EnsureStaticCategoriesAsync();
            var all = await CatalogService.GetAllServicesAsync();
            var cats = await CatalogService.GetCategoriesAsync();

            var lines = new List<string>();
            foreach (var cat in cats)
            {
                var inCat = all.Where(s => s.CategoryId == cat.CategoryId).ToList();
                if (inCat.Count == 0)
                    lines.Add($"📁 {cat.Name}: (пусто)");
                else
                    foreach (var s in inCat)
                        lines.Add($"• [{cat.Name}] {s.Name} — {s.Price}₽ ({s.DurationMinutes} мин)");
            }

            var text = "💰 Услуги:\n" + (lines.Count > 0 ? string.Join("\n", lines) : "(пусто)");
            if (text.Length > 4000)
                text = text[..3990] + "…";

            await bot.SendMessage(chatId, text, cancellationToken: ct);

            var kb = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить услугу", "adm_svc_add") },
                new[] { InlineKeyboardButton.WithCallbackData("🗑 Удалить услугу", "adm_svc_del_menu") }
            });
            await bot.SendMessage(chatId, "Управление услугами:", replyMarkup: kb, cancellationToken: ct);
        }

        public static async Task ShowCategoryPickerForAddAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            var cats = await CatalogService.GetCategoriesAsync();
            session.CachedCategoryIds = cats.Select(c => c.CategoryId).ToList();

            var rows = cats.Select((c, i) =>
                new[] { InlineKeyboardButton.WithCallbackData(c.Name, $"adm_cat:{i}") }).ToArray();

            await bot.SendMessage(chatId,
                "📁 Выберите категорию для новой услуги:\n(Маникюр, Педикюр или Дополнительно)",
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct);
        }

        public static async Task ShowDeletePickerAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            var all = await CatalogService.GetAllServicesAsync();
            var cats = await CatalogService.GetCategoriesAsync();

            if (!all.Any())
            {
                await bot.SendMessage(chatId, "Нет услуг для удаления.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                return;
            }

            session.CachedServiceIds = all.Select(s => s.ServiceId).ToList();
            var rows = new List<InlineKeyboardButton[]>();
            foreach (var (svc, i) in all.Select((s, idx) => (s, idx)))
            {
                var catName = cats.FirstOrDefault(c => c.CategoryId == svc.CategoryId)?.Name ?? "";
                var label = $"❌ [{catName}] {svc.Name}";
                if (label.Length > 64) label = label[..61] + "…";
                rows.Add(new[] { InlineKeyboardButton.WithCallbackData(label, $"adm_del:{i}") });
            }

            await bot.SendMessage(chatId, "Выберите услугу для удаления:",
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct);
        }

        public static async Task BeginAddInCategoryAsync(ITelegramBotClient bot, long chatId, int categoryIndex, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (categoryIndex < 0 || categoryIndex >= session.CachedCategoryIds.Count)
            {
                await bot.SendMessage(chatId, "Категория не найдена. Нажмите «Услуги» снова.", cancellationToken: ct);
                return;
            }

            var cats = await CatalogService.GetCategoriesAsync();
            var catId = session.CachedCategoryIds[categoryIndex];
            var catName = cats.FirstOrDefault(c => c.CategoryId == catId)?.Name ?? "категория";

            session.AdminCategoryId = catId;
            session.ServiceDraftName = null;
            session.ServiceDraftDesc = null;
            session.TempText = null;
            session.State = SessionState.Admin_Service_Name;

            await bot.SendMessage(chatId, $"➕ Категория: {catName}\nВведите название услуги:", cancellationToken: ct);
        }
    }
}
