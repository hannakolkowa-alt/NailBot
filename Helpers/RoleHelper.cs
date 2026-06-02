using TelegramBot;
using TelegramBot.State;

namespace TelegramBot.Helpers
{
    /// <summary>
    /// Переключение роли для аккаунта мастера: панель мастера ↔ тест как клиент.
    /// </summary>
    public static class RoleHelper
    {
        public static bool IsMasterAccount(long userId) => BotConfig.IsMaster(userId);

        /// <summary>true — меню и логика мастера; false — как обычный клиент (только для аккаунта мастера).</summary>
        public static bool ActAsMaster(long chatId, long userId)
        {
            if (!IsMasterAccount(userId))
                return false;
            return SessionStore.GetOrCreate(chatId).ActAsMasterPanel;
        }

        public static void SetMasterMode(long chatId)
        {
            SessionStore.GetOrCreate(chatId).ActAsMasterPanel = true;
        }

        public static void SetClientMode(long chatId)
        {
            SessionStore.GetOrCreate(chatId).ActAsMasterPanel = false;
        }

        public static string RoleLabel(long chatId, long userId) =>
            ActAsMaster(chatId, userId) ? "мастер" : "клиент (тест)";
    }
}
