using Telegram.Bot;
using TelegramBot.Constants;
using TelegramBot.Helpers;
using TelegramBot.Flows;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class SessionInputHandler
    {
        public static async Task<bool> TryHandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, bool actAsMaster, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);
            if (session.State == SessionState.Idle) return false;

            if (IsScheduleCancel(text))
            {
                SessionStore.Reset(chatId);
                var kb = actAsMaster
                    ? Keyboards.CreateAdminMenuKeyboard()
                    : Keyboards.CreateMainMenuKeyboard(RoleHelper.IsMasterAccount(userId));
                await bot.SendMessage(chatId, "Ввод отменён. /master | /client — смена роли.", replyMarkup: kb, cancellationToken: ct);
                return true;
            }

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
                    if (!session.AdminCategoryId.HasValue)
                    {
                        await bot.SendMessage(chatId, "Сначала выберите категорию: «Услуги» → «Добавить услугу».", cancellationToken: ct);
                        SessionStore.Reset(chatId);
                        return true;
                    }
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
                    if (!session.AdminCategoryId.HasValue)
                    {
                        SessionStore.Reset(chatId);
                        await bot.SendMessage(chatId, "Категория не выбрана. Начните с «Услуги» → «Добавить услугу».", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                        return true;
                    }
                    var added = await CatalogService.AddServiceAsync(
                        session.AdminCategoryId.Value,
                        session.ServiceDraftName!,
                        session.ServiceDraftDesc!,
                        dur,
                        decimal.Parse(session.TempText ?? "0"));
                    SessionStore.Reset(chatId);
                    if (added == null)
                        await bot.SendMessage(chatId, "Не удалось сохранить услугу.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                    else
                        await bot.SendMessage(chatId, "✅ Услуга добавлена.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                    return true;

                case SessionState.Admin_Schedule_CustomTime:
                    if (!ScheduleTimeParser.TryParse(text, out var customTime))
                    {
                        await bot.SendMessage(chatId, "Формат: ЧЧ:ММ (10:00, 14:30)", cancellationToken: ct);
                        return true;
                    }
                    session.State = SessionState.Idle;
                    await ScheduleAdminFlow.AddTimeAsync(bot, chatId, customTime, ct);
                    return true;

                case SessionState.Admin_Schedule_EditTime:
                    if (!ScheduleTimeParser.TryParse(text, out var newTime))
                    {
                        await bot.SendMessage(chatId, "Формат: ЧЧ:ММ (10:00, 14:30)", cancellationToken: ct);
                        return true;
                    }
                    if (!session.TargetSlotId.HasValue)
                    {
                        SessionStore.Reset(chatId);
                        await bot.SendMessage(chatId, "Слот не выбран. Откройте «Расписание».", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                        return true;
                    }
                    var (updated, errMsg) = await ScheduleService.UpdateTimeSlotAsync(session.TargetSlotId.Value, newTime);
                    session.State = SessionState.Idle;
                    session.TargetSlotId = null;
                    if (!updated)
                    {
                        await bot.SendMessage(chatId, errMsg ?? "Не удалось изменить время.", cancellationToken: ct);
                        await ScheduleAdminFlow.RefreshCurrentDayAsync(bot, chatId, ct);
                        return true;
                    }
                    await bot.SendMessage(chatId, $"✅ Время изменено на {newTime:HH:mm}.", cancellationToken: ct);
                    await ScheduleAdminFlow.RefreshCurrentDayAsync(bot, chatId, ct);
                    return true;
            }

            return false;
        }

        private static bool IsScheduleCancel(string text)
        {
            var t = text.Trim().ToLowerInvariant();
            return t is "отмена" or "отменить" or "cancel" or "выход" or "◀️ меню" or "меню" or "в меню";
        }

    }
}
