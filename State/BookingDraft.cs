namespace TelegramBot.State
{
    public class BookingDraft
    {
        public HashSet<Guid> SelectedServiceIds { get; } = new();
        public DateOnly? Date { get; set; }
        public Guid? WorkingDateId { get; set; }
        public Guid? TimeSlotId { get; set; }
        public TimeOnly? Time { get; set; }
        public string? ClientName { get; set; }
        public string? TelegramNick { get; set; }
        public Guid? EditingRequestId { get; set; }
        public Guid? EditingAppointmentId { get; set; }
    }
}
