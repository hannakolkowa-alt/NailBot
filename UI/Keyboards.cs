using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.UI
{
    public static class Keyboards
    {
        public static ReplyKeyboardMarkup CreateMainMenuKeyboard()
        {
            return new ReplyKeyboardMarkup(new KeyboardButton[][]
            {
                new[] { new KeyboardButton("О мастере") },
                new[] { new KeyboardButton("Услуги") },
                new[] { new KeyboardButton("Расписание") },
                new[] { new KeyboardButton("Записи") }
            })
            { ResizeKeyboard = true };
        }

        public static ReplyKeyboardMarkup CreateAdminMenuKeyboard()
        {
            return new ReplyKeyboardMarkup(new KeyboardButton[][]
            {
                new[] { new KeyboardButton("Клиентская база") },
                new[] { new KeyboardButton("Записи"), new KeyboardButton("Заявки") },
                new[] { new KeyboardButton("Мой профиль"), new KeyboardButton("Изменить профиль") },
                new[] { new KeyboardButton("Услуги"), new KeyboardButton("Расписание") },
                new[] { new KeyboardButton("Отзывы") }
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
