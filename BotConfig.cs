namespace TelegramBot
{
    public static class BotConfig
    {
        private static HashSet<long> _masterIds = new() { 5783971965 };

        public static void Configure(IEnumerable<long> masterTelegramIds)
        {
            var ids = masterTelegramIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count > 0)
                _masterIds = ids.ToHashSet();
        }

        public static bool IsMaster(long telegramUserId) => _masterIds.Contains(telegramUserId);

        public static IReadOnlyCollection<long> MasterTelegramIds => _masterIds;

        /// <summary>Куда слать уведомления мастеру (первый ID из списка).</summary>
        public static long PrimaryMasterTelegramId => _masterIds.FirstOrDefault();
    }
}
