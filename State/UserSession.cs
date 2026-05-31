namespace TelegramBot.State
{
    public enum SessionState
    {
        Idle,
        Booking_EnterName,
        Booking_EnterUsername,
        Cancel_EnterReason,
        Review_EnterText,
        Admin_Profile_Name,
        Admin_Profile_Username,
        Admin_Profile_Experience,
        Admin_Profile_Description,
        Admin_EditProfile,
        Admin_Service_Name,
        Admin_Service_Description,
        Admin_Service_Price,
        Admin_Service_Duration,
        Admin_Schedule_Date,
        Admin_Schedule_Time,
        Admin_RejectReason,
        Admin_ClientDeleteConfirm
    }

    public class UserSession
    {
        public long ChatId { get; init; }
        public SessionState State { get; set; } = SessionState.Idle;
        public BookingDraft Booking { get; } = new();
        public Guid? TargetRequestId { get; set; }
        public Guid? TargetAppointmentId { get; set; }
        public Guid? TargetClientId { get; set; }
        public string? TempText { get; set; }
        public string? AdminEditField { get; set; }
        public Guid? AdminCategoryId { get; set; }

        public List<Guid> CachedCategoryIds { get; set; } = new();
        public List<Guid> CachedServiceIds { get; set; } = new();
        public List<Guid> CachedDateIds { get; set; } = new();
        public List<Guid> CachedSlotIds { get; set; } = new();
        public List<Guid> CachedRequestIds { get; set; } = new();
        public List<Guid> CachedAppointmentIds { get; set; } = new();
        public List<Guid> CachedClientIds { get; set; } = new();
        public int CurrentCategoryIndex { get; set; }
        public string? ServiceDraftName { get; set; }
        public string? ServiceDraftDesc { get; set; }
    }
}
