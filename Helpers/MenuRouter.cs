using TelegramBot.UI;

namespace TelegramBot.Helpers
{
    /// <summary>
    /// Маршрутизация кнопок меню: одинаковые подписи (услуги, расписание) — по активному профилю, не по аккаунту.
    /// </summary>
    public static class MenuRouter
    {
        public static string NormalizeForProfile(string text, bool actAsMaster)
        {
            var n = MenuTexts.Normalize(text);

            // Старая клиентская клавиатура с «Записи» вместо «Мои записи»
            if (!actAsMaster && n == "записи")
                return "мои записи";

            // Старая клавиатура мастера с «Отзывы» → новая «Все отзывы»
            if (actAsMaster && n == "отзывы")
                return "все отзывы";

            // Старая клиентская кнопка «Галерея» → портфолио
            if (!actAsMaster && n == "галерея")
                return "портфолио мастера";

            return n;
        }

        public static bool ShouldUseAdminHandler(string normalized, bool actAsMaster, bool isMasterAccount)
        {
            if (!isMasterAccount)
                return false;

            if (!actAsMaster)
                return false;

            return MenuTexts.IsAdminMenuButton(normalized);
        }

        public static bool ShouldUseClientHandler(string normalized, bool actAsMaster, bool isMasterAccount)
        {
            if (!isMasterAccount)
                return MenuTexts.IsClientMenuButton(normalized);

            if (actAsMaster)
                return false;

            return MenuTexts.IsClientMenuButton(normalized);
        }
    }
}
