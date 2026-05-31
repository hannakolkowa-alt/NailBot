using System.Collections.Concurrent;

namespace TelegramBot.State
{
    public static class SessionStore
    {
        private static readonly ConcurrentDictionary<long, UserSession> _sessions = new();

        public static UserSession GetOrCreate(long chatId) =>
            _sessions.GetOrAdd(chatId, id => new UserSession { ChatId = id });

        public static void Reset(long chatId)
        {
            if (_sessions.TryGetValue(chatId, out var s))
            {
                s.State = SessionState.Idle;
                s.Booking.SelectedServiceIds.Clear();
                s.Booking.Date = null;
                s.Booking.WorkingDateId = null;
                s.Booking.TimeSlotId = null;
                s.Booking.Time = null;
                s.Booking.ClientName = null;
                s.Booking.TelegramNick = null;
                s.Booking.EditingRequestId = null;
                s.Booking.EditingAppointmentId = null;
                s.TargetRequestId = null;
                s.TargetAppointmentId = null;
                s.TempText = null;
                s.CachedCategoryIds.Clear();
                s.CachedServiceIds.Clear();
                s.CachedDateIds.Clear();
                s.CachedSlotIds.Clear();
                s.CachedRequestIds.Clear();
                s.CachedAppointmentIds.Clear();
                s.CachedClientIds.Clear();
            }
        }
    }
}
