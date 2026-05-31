using Telegram.Bot;
using TelegramBot.Constants;
using TelegramBot.Flows;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class SessionInputHandler
    {
        public static async Task<bool> TryHandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, bool isAdmin, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (session.State == SessionState.Idle) return false;

            switch (session.State)
            {
                case SessionState.Booking_EnterName:
                    session.Booking.ClientName = text.Trim();
                    session.State = SessionState.Booking_EnterUsername;
                    await bot.SendMessage(chatId, "Введите ник в Telegram (например @wannxxl):", cancellationToken: ct);
                    return true;

                case SessionState.Booking_EnterUsername:
                    session.Booking.TelegramNick = text.Trim().TrimStart('@');
                    if (!string.IsNullOrEmpty(session.Booking.TelegramNick) && !session.Booking.TelegramNick.StartsWith('@'))
                        session.Booking.TelegramNick = "@" + session.Booking.TelegramNick;
                    session.State = SessionState.Idle;
                    await BookingFlow.ShowSummaryAsync(bot, chatId, ct);
                    return true;

                case SessionState.Cancel_EnterReason:
                    if (session.TargetRequestId.HasValue)
                    {
                        await RequestService.UpdateRequestStatusAsync(session.TargetRequestId.Value, RequestStatus.Cancelled, $"Отмена клиентом: {text}");
                        try
                        {
                            await bot.SendMessage(BotConfig.PrimaryMasterTelegramId,
                                $"❌ Клиент отменил запись.\nПричина: {text}", cancellationToken: ct);
                        }
                        catch { }
                    }
                    SessionStore.Reset(chatId);
                    await bot.SendMessage(chatId, "Запись отменена.", replyMarkup: Keyboards.CreateMainMenuKeyboard(), cancellationToken: ct);
                    return true;

                case SessionState.Review_EnterText:
                    var client = await ClientService.GetOrCreateAsync(userId, null, null);
                    await ReviewService.AddAsync(client.ClientId, session.TargetAppointmentId, text);
                    SessionStore.Reset(chatId);
                    await bot.SendMessage(chatId, "Спасибо за отзыв! ⭐", replyMarkup: Keyboards.CreateMainMenuKeyboard(), cancellationToken: ct);
                    try
                    {
                        await bot.SendMessage(BotConfig.PrimaryMasterTelegramId, $"⭐ Новый отзыв от @{client.TelegramUsername}:\n{text}", cancellationToken: ct);
                    }
                    catch { }
                    return true;

                case SessionState.Admin_RejectReason:
                    if (session.TargetRequestId.HasValue)
                    {
                        await RequestService.UpdateRequestStatusAsync(session.TargetRequestId.Value, RequestStatus.Rejected, text);
                        var req = await RequestService.GetByIdAsync(session.TargetRequestId.Value);
                        if (req != null)
                        {
                            var cl = (await ClientService.GetAllClientsAsync()).FirstOrDefault(c => c.ClientId == req.ClientId);
                            if (cl != null)
                            {
                                try
                                {
                                    await bot.SendMessage(cl.TelegramId, $"Заявка отклонена.\nПричина: {text}", cancellationToken: ct);
                                }
                                catch { }
                            }
                        }
                    }
                    SessionStore.Reset(chatId);
                    await bot.SendMessage(chatId, "Заявка отклонена.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                    return true;

                case SessionState.Admin_Profile_Name:
                    session.TempText = text;
                    session.State = SessionState.Admin_Profile_Username;
                    await bot.SendMessage(chatId, "Введите ваш Telegram username (без @):", cancellationToken: ct);
                    return true;

                case SessionState.Admin_Profile_Username:
                    session.AdminEditField = text;
                    session.State = SessionState.Admin_Profile_Experience;
                    await bot.SendMessage(chatId, "Введите опыт работы:", cancellationToken: ct);
                    return true;

                case SessionState.Admin_Profile_Experience:
                    session.TargetRequestId = null;
                    var exp = text;
                    session.State = SessionState.Admin_Profile_Description;
                    session.TempText = session.TempText + "|" + session.AdminEditField + "|" + exp;
                    await bot.SendMessage(chatId, "Введите описание (о себе):", cancellationToken: ct);
                    return true;

                case SessionState.Admin_Profile_Description:
                    var parts = (session.TempText ?? "").Split('|');
                    var name = parts.Length > 0 ? parts[0] : "Мастер";
                    var user = parts.Length > 1 ? parts[1] : "";
                    var experience = parts.Length > 2 ? parts[2] : "";
                    await MasterService.SaveOrUpdateProfileAsync(Guid.NewGuid(), name, user, experience, text);
                    SessionStore.Reset(chatId);
                    await bot.SendMessage(chatId, "✅ Профиль создан!", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                    return true;

                case SessionState.Admin_EditProfile:
                    var profile = await MasterService.GetMasterProfileAsync();
                    if (profile == null) return true;
                    var field = session.AdminEditField;
                    switch (field)
                    {
                        case "name": profile.Name = text; break;
                        case "user": profile.TelegramUsername = text; break;
                        case "exp": profile.Experience = text; break;
                        case "desc": profile.Description = text; break;
                    }
                    await MasterService.SaveOrUpdateProfileAsync(profile.MasterId, profile.Name, profile.TelegramUsername, profile.Experience, profile.Description);
                    SessionStore.Reset(chatId);
                    await bot.SendMessage(chatId, "Профиль обновлён.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                    return true;

                case SessionState.Admin_Service_Name:
                    session.ServiceDraftName = text;
                    session.State = SessionState.Admin_Service_Description;
                    await bot.SendMessage(chatId, "Описание услуги:", cancellationToken: ct);
                    return true;

                case SessionState.Admin_Service_Description:
                    session.ServiceDraftDesc = text;
                    session.State = SessionState.Admin_Service_Price;
                    await bot.SendMessage(chatId, "Цена (число):", cancellationToken: ct);
                    return true;

                case SessionState.Admin_Service_Price:
                    if (!decimal.TryParse(text, out var price)) { await bot.SendMessage(chatId, "Введите число.", cancellationToken: ct); return true; }
                    session.TempText = price.ToString();
                    session.State = SessionState.Admin_Service_Duration;
                    await bot.SendMessage(chatId, "Длительность в минутах:", cancellationToken: ct);
                    return true;

                case SessionState.Admin_Service_Duration:
                    if (!int.TryParse(text, out var dur)) { await bot.SendMessage(chatId, "Введите число.", cancellationToken: ct); return true; }
                    var catId = session.AdminCategoryId ?? (await CatalogService.GetCategoriesAsync()).First().CategoryId;
                    await CatalogService.AddServiceAsync(catId, session.ServiceDraftName!, session.ServiceDraftDesc!, dur, decimal.Parse(session.TempText ?? "0"));
                    SessionStore.Reset(chatId);
                    await bot.SendMessage(chatId, "Услуга добавлена.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                    return true;

                case SessionState.Admin_Schedule_Date:
                    if (!TryParseScheduleDate(text, out var date))
                    {
                        await bot.SendMessage(chatId, "Формат даты: ГГГГ-ММ-ДД или ДД.ММ.ГГГГ (например 2026-06-01)", cancellationToken: ct);
                        return true;
                    }
                    var master = await MasterService.EnsureMasterExistsAsync("Мастер", null);
                    var wd = await ScheduleService.AddWorkingDateAsync(master.MasterId, date);
                    if (wd == null)
                    {
                        await bot.SendMessage(chatId, "Не удалось сохранить дату. Попробуйте снова.", cancellationToken: ct);
                        return true;
                    }
                    session.Booking.WorkingDateId = wd.DateId;
                    session.State = SessionState.Admin_Schedule_Time;
                    await bot.SendMessage(chatId,
                        $"✅ Дата {date:dd.MM.yyyy} сохранена.\n\nВведите время слота (ЧЧ:ММ), например 10:00.\nМожно несколько. Когда закончите — «готово».",
                        cancellationToken: ct);
                    return true;

                case SessionState.Admin_Schedule_Time:
                    if (IsScheduleDone(text))
                    {
                        var savedDate = session.Booking.WorkingDateId;
                        SessionStore.Reset(chatId);
                        await bot.SendMessage(chatId,
                            savedDate.HasValue
                                ? "✅ Расписание сохранено. Клиенты увидят дату при записи.\nДобавить ещё — /master или «Расписание»."
                                : "Готово.",
                            replyMarkup: Keyboards.CreateAdminMenuKeyboard(),
                            cancellationToken: ct);
                        return true;
                    }
                    if (!TryParseScheduleTime(text, out var time))
                    {
                        await bot.SendMessage(chatId, "Формат времени: ЧЧ:ММ (10:00, 14:30) или «готово»", cancellationToken: ct);
                        return true;
                    }
                    if (!session.Booking.WorkingDateId.HasValue)
                    {
                        session.State = SessionState.Admin_Schedule_Date;
                        await bot.SendMessage(chatId, "Сначала введите дату (ГГГГ-ММ-ДД):", cancellationToken: ct);
                        return true;
                    }
                    var slot = await ScheduleService.AddTimeSlotAsync(session.Booking.WorkingDateId.Value, time);
                    if (slot == null)
                    {
                        await bot.SendMessage(chatId, "Не удалось сохранить слот. Проверьте таблицу time_slots в Supabase.", cancellationToken: ct);
                        return true;
                    }
                    await bot.SendMessage(chatId,
                        $"✅ Слот {time:HH:mm} сохранён.\nВведите ещё время или «готово»:",
                        cancellationToken: ct);
                    return true;
            }

            return false;
        }

        private static bool TryParseScheduleDate(string text, out DateOnly date)
        {
            text = text.Trim();
            if (DateOnly.TryParse(text, out date))
                return true;

            if (DateTime.TryParseExact(text, "dd.MM.yyyy", null,
                    System.Globalization.DateTimeStyles.None, out var dt))
            {
                date = DateOnly.FromDateTime(dt);
                return true;
            }

            date = default;
            return false;
        }

        private static bool TryParseScheduleTime(string text, out TimeOnly time)
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

        private static bool IsScheduleDone(string text)
        {
            var t = text.Trim().ToLowerInvariant();
            return t is "готово" or "готово." or "done" or "ок" or "ok";
        }
    }
}
