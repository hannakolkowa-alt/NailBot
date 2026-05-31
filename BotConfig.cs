namespace TelegramBot
{
    public static class BotConfig
    {
        public const long AdminTelegramId = 5783971965;

        public static bool IsMaster(long telegramUserId) => telegramUserId == AdminTelegramId;
    }
}
