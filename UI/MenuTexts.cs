namespace TelegramBot.UI
{
    public static class MenuTexts
    {
        public static readonly HashSet<string> ClientMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "о мастере", "услуги", "расписание", "записи", "отзывы",
            "галерея", "прайс", "график", "записаться", "мои записи"
        };

        public static readonly HashSet<string> AdminMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "клиентская база", "записи", "заявки", "мой профиль", "изменить профиль",
            "услуги", "расписание", "отзывы", "галерея", "прайс", "график",
            "🧪 клиент", "режим клиента"
        };

        public static readonly HashSet<string> MasterToClientSwitch = new(StringComparer.OrdinalIgnoreCase)
        {
            "🧪 клиент", "режим клиента", "/client"
        };

        public static readonly HashSet<string> ClientToMasterSwitch = new(StringComparer.OrdinalIgnoreCase)
        {
            "👩‍🎨 мастер", "режим мастера", "/master", "/admin"
        };

        /// <summary>Кнопки только клиентского меню — для админа тоже идут в ClientHandler.</summary>
        public static readonly HashSet<string> ClientOnlyMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "о мастере", "галерея", "прайс", "график", "записаться", "мои записи", "отзывы"
        };

        public static bool IsClientOnlyButton(string text) => ClientOnlyMenu.Contains(text.Trim().ToLowerInvariant());

        public static readonly HashSet<string> CancelMenu = new(StringComparer.OrdinalIgnoreCase)
        {
            "отмена", "меню", "◀️ меню", "в меню", "/menu", "/start"
        };

        public static bool IsMenuButton(string text)
        {
            var t = text.Trim().ToLowerInvariant();
            return ClientMenu.Contains(t) || AdminMenu.Contains(t) || CancelMenu.Contains(t)
                || MasterToClientSwitch.Contains(t) || ClientToMasterSwitch.Contains(t);
        }

        public static bool IsRoleSwitchButton(string text)
        {
            var t = text.Trim().ToLowerInvariant();
            return MasterToClientSwitch.Contains(t) || ClientToMasterSwitch.Contains(t);
        }

        public static string Normalize(string text) => text.Trim().ToLowerInvariant();
    }
}
