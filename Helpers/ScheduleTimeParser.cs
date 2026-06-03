namespace TelegramBot.Helpers
{
    public static class ScheduleTimeParser
    {
        public static bool TryParse(string text, out TimeOnly time)
        {
            text = text.Trim().Replace('.', ':');
            if (TimeOnly.TryParse(text, out time))
                return true;

            if (int.TryParse(text, out var hour) && hour is >= 0 and <= 23)
            {
                time = new TimeOnly(hour, 0);
                return true;
            }

            time = default;
            return false;
        }

        public static bool TryParseFromCallback(string hhmm, out TimeOnly time)
        {
            time = default;
            if (hhmm.Length != 4 || !int.TryParse(hhmm, out var n))
                return false;
            var h = n / 100;
            var m = n % 100;
            if (h is < 0 or > 23 || m is < 0 or > 59)
                return false;
            time = new TimeOnly(h, m);
            return true;
        }

        public static string ToCallbackKey(TimeOnly time) => $"{time.Hour:D2}{time.Minute:D2}";
    }
}
