using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.UI
{
    public static class Keyboards
    {
        public static ReplyKeyboardMarkup CreateMainMenuKeyboard(bool showMasterSwitch = false)
        {
            var rows = new List<KeyboardButton[]>
            {
                new[] { new KeyboardButton("О мастере") },
                new[] { new KeyboardButton("Услуги") },
                new[] { new KeyboardButton("Расписание") },
                new[] { new KeyboardButton("Записи") },
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
                new[] { new KeyboardButton("Отзывы") },
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
