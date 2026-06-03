namespace TelegramBot.UI
{
    public static class MenuTexts
    {
        public static readonly HashSet<string> ClientMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "о мастере", "услуги", "расписание", "мои записи", "отзывы",
            "портфолио мастера", "партфолио мастера",
            "галерея", "прайс", "график", "записаться", "◀️ меню"
        };

        public static readonly HashSet<string> AdminMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "клиентская база", "записи", "заявки", "мой профиль", "изменить профиль",
            "услуги", "расписание", "галерея", "все отзывы", "отзывы", "◀️ меню"
        };

        public static readonly HashSet<string> CancelMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "отмена", "меню", "◀️ меню", "в меню", "/menu", "/start"
        };

        public static bool IsClientMenuButton(string text) =>
            ClientMenu.Contains(Normalize(text));

        public static bool IsAdminMenuButton(string text) =>
            AdminMenu.Contains(Normalize(text));

        public static bool IsMenuButton(string text)
        {
            var t = Normalize(text);
            return ClientMenu.Contains(t) || AdminMenu.Contains(t) || CancelMenu.Contains(t);
        }

        public static string Normalize(string text) => text.Trim().ToLowerInvariant();
    }
}

