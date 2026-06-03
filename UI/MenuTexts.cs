namespace TelegramBot.UI
{
    public static class MenuTexts
    {
        public static readonly HashSet<string> ClientMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "о мастере", "услуги", "расписание", "мои записи", "отзывы",
            "галерея", "прайс", "график", "записаться", "◀️ меню"
        };

        public static readonly HashSet<string> AdminMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "клиентская база", "записи", "заявки", "мой профиль", "изменить профиль",
            "услуги", "расписание", "все отзывы", "отзывы", "◀️ меню"
        };

        public static readonly HashSet<string> MasterToClientSwitch = new(StringComparer.OrdinalIgnoreCase)
        {
            "🧪 клиент", "режим клиента", "/client"
        };

        public static readonly HashSet<string> ClientToMasterSwitch = new(StringComparer.OrdinalIgnoreCase)
        {
            "👩‍🎨 мастер", "режим мастера", "/master", "/admin"
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
            return ClientMenu.Contains(t) || AdminMenu.Contains(t) || CancelMenu.Contains(t)
                || MasterToClientSwitch.Contains(t) || ClientToMasterSwitch.Contains(t);
        }

        public static bool IsRoleSwitchButton(string text)
        {
            var t = Normalize(text);
            return MasterToClientSwitch.Contains(t) || ClientToMasterSwitch.Contains(t);
        }

        public static string Normalize(string text) => text.Trim().ToLowerInvariant();
    }
}
