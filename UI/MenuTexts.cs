namespace TelegramBot.UI
{
    public static class MenuTexts
    {
        public static readonly HashSet<string> ClientMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "о мастере", "услуги", "расписание", "записи",
            "галерея", "прайс", "график", "записаться", "мои записи"
        };

        public static readonly HashSet<string> AdminMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "клиентская база", "записи", "заявки", "мой профиль", "изменить профиль",
            "услуги", "расписание", "отзывы", "галерея", "прайс", "график"
        };

        public static readonly HashSet<string> CancelMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "отмена", "меню", "◀️ меню", "в меню", "/menu", "/start"
        };

        public static bool IsMenuButton(string text)
        {
            var t = text.Trim().ToLowerInvariant();
            return ClientMenu.Contains(t) || AdminMenu.Contains(t) || CancelMenu.Contains(t);
        }

        public static string Normalize(string text) => text.Trim().ToLowerInvariant();
    }
}
