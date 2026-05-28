using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.UI
{
    /// <summary>
    /// Фабрика экранных клавиатур Telegram.
    /// Генерирует разметку кнопок для главного меню клиента и панели управления администратора.
    /// </summary>
    public static class Keyboards
    {
        public static ReplyKeyboardMarkup CreateMainMenuKeyboard()
        {
            var keyboard = new KeyboardButton[][]
            {
                new KeyboardButton[] { "О мастере" },
                new KeyboardButton[] { "Галерея", "Прайс" },
                new KeyboardButton[] { "График", "Записаться" },
                new KeyboardButton[] { "Мои записи" }
            };

            return new ReplyKeyboardMarkup(keyboard)
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };
        }

        public static ReplyKeyboardMarkup CreateAdminMenuKeyboard()
        {
            var keyboard = new KeyboardButton[][]
            {
                new KeyboardButton[] { "Мой профиль", "Галерея" },
                new KeyboardButton[] { "Прайс", "График" },
                new KeyboardButton[] { "Заявки", "Записи" },
                new KeyboardButton[] { "Отзывы" }
            };

            return new ReplyKeyboardMarkup(keyboard)
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };
        }
    }
}