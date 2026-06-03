using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Helpers;

namespace TelegramBot.UI
{
    public static class Keyboards
    {
        /// <summary>Клавиатура по текущему режиму (мастер / клиент), не по аккаунту.</summary>
        public static ReplyKeyboardMarkup GetMenuForUser(long chatId, long userId) =>
            RoleHelper.ActAsMaster(chatId, userId)
                ? CreateAdminMenuKeyboard()
                : CreateMainMenuKeyboard(showMasterSwitch: RoleHelper.IsMasterAccount(userId));

        public static ReplyKeyboardMarkup CreateMainMenuKeyboard(bool showMasterSwitch = false)
        {
            var rows = new List<KeyboardButton[]>
            {
                new[] { new KeyboardButton("О мастере") },
                new[] { new KeyboardButton("Портфолио мастера") },
                new[] { new KeyboardButton("Услуги") },
                new[] { new KeyboardButton("Расписание") },
                new[] { new KeyboardButton("Мои записи") },
                new[] { new KeyboardButton("Отзывы") },
                new[] { new KeyboardButton("◀️ Меню") }
            };
            if (showMasterSwitch)
                rows.Add(new[] { new KeyboardButton("👩‍🎨 Мастер") });
            return new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true };
        }

        public static ReplyKeyboardMarkup CreateAdminMenuKeyboard()
        {
            return new ReplyKeyboardMarkup(new KeyboardButton[][]
            {
                new[] { new KeyboardButton("Клиентская база") },
                new[] { new KeyboardButton("Записи"), new KeyboardButton("Заявки") },
                new[] { new KeyboardButton("Мой профиль"), new KeyboardButton("Изменить профиль") },
                new[] { new KeyboardButton("Услуги"), new KeyboardButton("Расписание") },
                new[] { new KeyboardButton("Галерея") },
                new[] { new KeyboardButton("Все отзывы") },
                new[] { new KeyboardButton("🧪 Клиент") },
                new[] { new KeyboardButton("◀️ Меню") }
            })
            { ResizeKeyboard = true };
        }

        public static InlineKeyboardMarkup ConfirmCancel(string confirmData, string cancelData) =>
            new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Подтвердить", confirmData),
                    InlineKeyboardButton.WithCallbackData("Изменить", cancelData)
                }
            });
    }
}
