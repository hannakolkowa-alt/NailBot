using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.UI
{
    public static class ScheduleCalendarKeyboard
    {
        private static readonly string[] WeekDays = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };

        private static readonly string[] MonthNames =
        {
            "", "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
            "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
        };

        public static InlineKeyboardMarkup Build(int year, int month, HashSet<DateOnly> scheduledDays, DateOnly today)
        {
            var rows = new List<InlineKeyboardButton[]>();
            var (prevY, prevM) = ShiftMonth(year, month, -1);
            var (nextY, nextM) = ShiftMonth(year, month, 1);

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️", $"sch_mp:{prevY}:{prevM}"),
                InlineKeyboardButton.WithCallbackData($"{MonthNames[month]} {year}", "sch_noop"),
                InlineKeyboardButton.WithCallbackData("▶️", $"sch_mn:{nextY}:{nextM}")
            });

            rows.Add(WeekDays.Select(d => InlineKeyboardButton.WithCallbackData(d, "sch_noop")).ToArray());

            var first = new DateOnly(year, month, 1);
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var offset = ((int)first.DayOfWeek + 6) % 7;

            var dayRow = new List<InlineKeyboardButton>();
            for (var i = 0; i < offset; i++)
                dayRow.Add(InlineKeyboardButton.WithCallbackData(" ", "sch_noop"));

            for (var day = 1; day <= daysInMonth; day++)
            {
                var d = new DateOnly(year, month, day);
                var hasSchedule = scheduledDays.Contains(d);
                var isToday = d == today;
                var label = isToday ? $"•{day}" : hasSchedule ? $"{day}✓" : $"{day}";
                dayRow.Add(InlineKeyboardButton.WithCallbackData(label, $"sch_d:{d:yyyy-MM-dd}"));

                if (dayRow.Count == 7)
                {
                    rows.Add(dayRow.ToArray());
                    dayRow = new List<InlineKeyboardButton>();
                }
            }

            if (dayRow.Count > 0)
            {
                while (dayRow.Count < 7)
                    dayRow.Add(InlineKeyboardButton.WithCallbackData(" ", "sch_noop"));
                rows.Add(dayRow.ToArray());
            }

            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🔄 Обновить", $"sch_m:{year}:{month}") });

            return new InlineKeyboardMarkup(rows);
        }

        private static (int Year, int Month) ShiftMonth(int year, int month, int delta)
        {
            var dt = new DateOnly(year, month, 1).AddMonths(delta);
            return (dt.Year, dt.Month);
        }
    }
}
