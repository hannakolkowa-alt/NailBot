using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Constants;
using TelegramBot.Helpers;
using TelegramBot.Flows;
using TelegramBot.Services;
using TelegramBot.State;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    public static class CallbackHandler
    {
        public static async Task HandleAsync(ITelegramBotClient bot, CallbackQuery cq, CancellationToken ct)
        {
            var data = cq.Data ?? "";
            var chatId = cq.Message?.Chat.Id ?? cq.From.Id;
            var userId = cq.From.Id;
            var isMasterAccount = RoleHelper.IsMasterAccount(userId);

            try
            {
                await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
                await HandleCallbackCoreAsync(bot, chatId, userId, isMasterAccount, data, cq.From.Username, cq.From.FirstName, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Callback error [{data}]: {ex}");
                try
                {
                    var hint = ex.Message.Length > 300 ? ex.Message[..300] + "…" : ex.Message;
                    await bot.SendMessage(chatId,
                        $"⚠️ Ошибка кнопки:\n{hint}\n\n/start или «Заявки» снова.",
                        cancellationToken: ct);
                }
                catch { }
            }
        }

        private static async Task HandleCallbackCoreAsync(ITelegramBotClient bot, long chatId, long userId, bool isMasterAccount, string data, string? username, string? firstName, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);

            if (data.StartsWith("cat:") && int.TryParse(data[4..], out var catIdx))
            {
                await BookingFlow.ShowCategoryServicesAsync(bot, chatId, catIdx, ct);
                return;
            }

            if (data.StartsWith("svc:"))
            {
                if (data == "svc:done") { await BookingFlow.ShowDatesAsync(bot, chatId, ct); return; }
                if (!int.TryParse(data[4..], out var idx) || idx < 0 || idx >= session.CachedServiceIds.Count)
                {
                    await StaleCallbackAsync(bot, chatId, ct);
                    return;
                }
                if (idx >= 0 && idx < session.CachedServiceIds.Count)
                {
                    var id = session.CachedServiceIds[idx];
                    if (session.Booking.SelectedServiceIds.Contains(id)) session.Booking.SelectedServiceIds.Remove(id);
                    else session.Booking.SelectedServiceIds.Add(id);
                    await BookingFlow.ShowCategoryServicesAsync(bot, chatId, session.CurrentCategoryIndex, ct);
                }
                return;
            }

            if (data.StartsWith("date:") && int.TryParse(data[5..], out var dateIdx))
            {
                await BookingFlow.ShowTimesAsync(bot, chatId, dateIdx, ct);
                return;
            }

            if (data.StartsWith("slot:") && int.TryParse(data[5..], out var slotIdx))
            {
                var idx = slotIdx;
                if (idx >= 0 && idx < session.CachedSlotIds.Count)
                {
                    session.Booking.TimeSlotId = session.CachedSlotIds[idx];
                    var slots = await ScheduleService.GetFreeSlotsAsync(session.Booking.WorkingDateId!.Value);
                    session.Booking.Time = slots.FirstOrDefault(s => s.TimeSlotId == session.Booking.TimeSlotId)?.Time;
                    await BookingFlow.AskNameAsync(bot, chatId, ct);
                }
                return;
            }

            if (data == "book:restart")
            {
                SessionStore.Reset(chatId);
                await BookingFlow.StartAsync(bot, chatId, ct);
                return;
            }

            if (data == "book:ok")
            {
                await BookingFlow.SubmitAsync(bot, chatId, userId, username, firstName, ct);
                return;
            }

            if (data.StartsWith("req_ok:"))
            {
                if (!isMasterAccount) return;
                if (!TryResolveRequestId(data, "req_ok:", session, out var reqIdOk))
                {
                    await StaleCallbackAsync(bot, chatId, ct);
                    return;
                }
                await ApproveRequestAsync(bot, chatId, reqIdOk, ct);
                return;
            }

            if (data.StartsWith("req_no:"))
            {
                if (!isMasterAccount) return;
                if (!TryResolveRequestId(data, "req_no:", session, out var reqIdNo))
                {
                    await StaleCallbackAsync(bot, chatId, ct);
                    return;
                }
                session.TargetRequestId = reqIdNo;
                session.State = SessionState.Admin_RejectReason;
                await bot.SendMessage(chatId, "Укажите причину отклонения заявки:", cancellationToken: ct);
                return;
            }

            if (data.StartsWith("apt_done:"))
            {
                if (!isMasterAccount) return;
                if (!int.TryParse(data[9..], out var ai) || ai >= session.CachedAppointmentIds.Count) { await StaleCallbackAsync(bot, chatId, ct); return; }
                var aptId = session.CachedAppointmentIds[ai];
                await MarkAppointmentDoneAsync(bot, chatId, aptId, ct);
                return;
            }

            if (data.StartsWith("rec_can:"))
            {
                if (!int.TryParse(data[8..], out var ci) || ci >= session.CachedRequestIds.Count) { await StaleCallbackAsync(bot, chatId, ct); return; }
                var reqId = session.CachedRequestIds[ci];
                session.TargetRequestId = reqId;
                session.State = SessionState.Cancel_EnterReason;
                await bot.SendMessage(chatId, "Укажите причину отмены (мастер получит уведомление):", cancellationToken: ct);
                return;
            }

            if (data.StartsWith("rec_chg:"))
            {
                if (!int.TryParse(data[8..], out var cg) || cg >= session.CachedRequestIds.Count) { await StaleCallbackAsync(bot, chatId, ct); return; }
                var reqId = session.CachedRequestIds[cg];
                session.Booking.EditingRequestId = reqId;
                SessionStore.Reset(chatId);
                SessionStore.GetOrCreate(chatId).Booking.EditingRequestId = reqId;
                await bot.SendMessage(chatId, "Изменение записи: выберите услуги заново.", cancellationToken: ct);
                await BookingFlow.StartAsync(bot, chatId, ct);
                return;
            }

            if (data.StartsWith("cli_del:"))
            {
                if (!isMasterAccount) return;
                if (!int.TryParse(data[8..], out var di) || di >= session.CachedClientIds.Count) { await StaleCallbackAsync(bot, chatId, ct); return; }
                var clientId = session.CachedClientIds[di];
                await ClientService.DeleteClientAsync(clientId);
                await bot.SendMessage(chatId, "Клиент удалён.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                return;
            }

            if (data == "cli_clear")
            {
                if (!isMasterAccount) return;
                await ClientService.ClearAllClientsAsync();
                await bot.SendMessage(chatId, "Клиентская база очищена.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                return;
            }

            if (data.StartsWith("prof:") && isMasterAccount)
            {
                session.State = SessionState.Admin_EditProfile;
                session.AdminEditField = data[5..];
                await bot.SendMessage(chatId, "Введите новое значение:", cancellationToken: ct);
                return;
            }

            if (isMasterAccount && data == "gal_add")
            {
                await GalleryAdminFlow.BeginAddPhotoAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data == "gal_done")
            {
                session.State = SessionState.Idle;
                await bot.SendMessage(chatId, "Добавление фото завершено.", cancellationToken: ct);
                await GalleryAdminFlow.ShowMenuAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data == "gal_del_menu")
            {
                await GalleryAdminFlow.ShowDeletePickerAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data.StartsWith("gal_del:") && int.TryParse(data[8..], out var galDelIdx))
            {
                if (galDelIdx < 0 || galDelIdx >= session.CachedGalleryPhotoIds.Count)
                {
                    await StaleCallbackAsync(bot, chatId, ct);
                    return;
                }
                var photoId = session.CachedGalleryPhotoIds[galDelIdx];
                var deleted = await GalleryService.DeletePhotoAsync(photoId);
                if (!deleted)
                {
                    await bot.SendMessage(chatId, "Не удалось удалить фото.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                    return;
                }
                await bot.SendMessage(chatId, "✅ Фото удалено.", cancellationToken: ct);
                await GalleryAdminFlow.ShowMenuAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data == "adm_svc_add")
            {
                await ServicesAdminFlow.ShowCategoryPickerForAddAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data == "adm_svc_del_menu")
            {
                await ServicesAdminFlow.ShowDeletePickerAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data.StartsWith("adm_cat:") && int.TryParse(data[8..], out var admCatIdx))
            {
                await ServicesAdminFlow.BeginAddInCategoryAsync(bot, chatId, admCatIdx, ct);
                return;
            }

            if (isMasterAccount && data.StartsWith("adm_del:") && int.TryParse(data[8..], out var delIdx))
            {
                if (delIdx < 0 || delIdx >= session.CachedServiceIds.Count)
                {
                    await StaleCallbackAsync(bot, chatId, ct);
                    return;
                }
                var serviceId = session.CachedServiceIds[delIdx];
                var ok = await CatalogService.DeleteServiceAsync(serviceId);
                if (!ok)
                {
                    await bot.SendMessage(chatId, "Не удалось удалить услугу.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                    return;
                }
                await bot.SendMessage(chatId, "✅ Услуга удалена.", cancellationToken: ct);
                await ServicesAdminFlow.ShowMenuAsync(bot, chatId, ct);
                return;
            }

            if (data == "sch_noop")
                return;

            if (isMasterAccount && ScheduleAdminFlow.TryParseDayCallback(data, out var pickedDay))
            {
                await ScheduleAdminFlow.ShowDayAsync(bot, chatId, pickedDay, ct);
                return;
            }

            if (isMasterAccount && ScheduleAdminFlow.TryParseMonthCallback(data, out var calYear, out var calMonth))
            {
                await ScheduleAdminFlow.ShowCalendarAsync(bot, chatId, calYear, calMonth, ct);
                return;
            }

            if (isMasterAccount && data == "sch_add")
            {
                await ScheduleAdminFlow.ShowTimePickerAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data == "sch_tc")
            {
                await ScheduleAdminFlow.BeginCustomTimeAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data == "sch_back_day")
            {
                await ScheduleAdminFlow.RefreshCurrentDayAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data.StartsWith("sch_t:") && ScheduleTimeParser.TryParseFromCallback(data[6..], out var pickTime))
            {
                await ScheduleAdminFlow.AddTimeAsync(bot, chatId, pickTime, ct);
                return;
            }

            if (isMasterAccount && data.StartsWith("sch_es:") && int.TryParse(data[7..], out var editIdx))
            {
                await ScheduleAdminFlow.BeginEditSlotAsync(bot, chatId, editIdx, ct);
                return;
            }

            if (isMasterAccount && data.StartsWith("sch_ds:") && int.TryParse(data[7..], out var delSlotIdx))
            {
                await ScheduleAdminFlow.DeleteSlotAsync(bot, chatId, delSlotIdx, ct);
                return;
            }

            if (isMasterAccount && data == "sch_dd")
            {
                await ScheduleAdminFlow.ConfirmDeleteDayAsync(bot, chatId, ct);
                return;
            }

            if (isMasterAccount && data == "sch_dd_yes")
            {
                await ScheduleAdminFlow.DeleteDayAsync(bot, chatId, ct);
                return;
            }

            if (data.StartsWith("rev_start:") && int.TryParse(data[10..], out var revStartIdx))
            {
                if (revStartIdx < 0 || revStartIdx >= session.CachedReviewableAppointmentIds.Count)
                {
                    await bot.SendMessage(chatId, "Запись не найдена. Откройте «Мои записи» снова.", cancellationToken: ct);
                    return;
                }
                await ReviewFlow.BeginReviewAsync(bot, chatId, userId, session.CachedReviewableAppointmentIds[revStartIdx], ct);
                return;
            }

            if (data.StartsWith("rev_star:") && int.TryParse(data[9..], out var starN) && starN is >= 1 and <= 5)
            {
                await ReviewFlow.OnStarSelectedAsync(bot, chatId, starN, ct);
                return;
            }

            if (data == "rev_cancel")
            {
                SessionStore.Reset(chatId);
                await bot.SendMessage(chatId, "Отзыв отменён.",
                    replyMarkup: Keyboards.GetMenuForUser(chatId, userId),
                    cancellationToken: ct);
                return;
            }
        }

        private static async Task StaleCallbackAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            SessionStore.Reset(chatId);
            await bot.SendMessage(chatId,
                "Сессия устарела. Нажмите «Услуги» или /start, чтобы начать заново.",
                cancellationToken: ct);
        }

        private static bool TryResolveRequestId(string data, string prefix, UserSession session, out Guid requestId)
        {
            requestId = default;
            var payload = data[prefix.Length..];
            if (Guid.TryParse(payload, out requestId))
                return true;

            if (int.TryParse(payload, out var idx) && idx >= 0 && idx < session.CachedRequestIds.Count)
            {
                requestId = session.CachedRequestIds[idx];
                return true;
            }

            return false;
        }

        private static async Task ApproveRequestAsync(ITelegramBotClient bot, long adminChatId, Guid requestId, CancellationToken ct)
        {
            var request = await RequestService.GetByIdAsync(requestId);
            if (request == null)
            {
                await bot.SendMessage(adminChatId, "Заявка не найдена.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                return;
            }

            var master = await MasterService.EnsureMasterExistsAsync("Мастер", null);

            if (!request.DesiredDate.HasValue)
            {
                await bot.SendMessage(adminChatId, "В заявке нет даты.", cancellationToken: ct);
                return;
            }

            var wd = (await ScheduleService.GetWorkingDatesAsync(DateOnly.MinValue, DateOnly.MaxValue))
                .FirstOrDefault(w => w.Date == request.DesiredDate);
            wd ??= await ScheduleService.AddWorkingDateAsync(master.MasterId, request.DesiredDate.Value);
            if (wd == null)
            {
                await bot.SendMessage(adminChatId, "Не удалось создать дату в расписании.", cancellationToken: ct);
                return;
            }

            var allSlots = await ScheduleService.GetSlotsForWorkingDateAsync(wd.DateId);
            var slot = allSlots.FirstOrDefault(s => request.DesiredTime.HasValue && s.Time == request.DesiredTime.Value)
                ?? allSlots.FirstOrDefault();

            if (slot == null && request.DesiredTime.HasValue)
                slot = await ScheduleService.AddTimeSlotAsync(wd.DateId, request.DesiredTime.Value);

            if (slot == null)
            {
                await bot.SendMessage(adminChatId, "Нет слота на это время. Добавьте время в «Расписание».", cancellationToken: ct);
                return;
            }

            if (!await RequestService.UpdateRequestStatusAsync(requestId, RequestStatus.Approved))
            {
                await bot.SendMessage(adminChatId,
                    "Не удалось обновить статус. Выполните supabase_fix_requests_status.sql в Supabase.",
                    cancellationToken: ct);
                return;
            }

            var appt = await AppointmentService.CreateFromRequestAsync(request, master.MasterId, wd.DateId, slot.TimeSlotId);
            if (appt == null)
            {
                await bot.SendMessage(adminChatId,
                    "Статус обновлён, но запись не создана. Выполните supabase_appointments.sql в Supabase.",
                    cancellationToken: ct);
                return;
            }

            var client = (await ClientService.GetAllClientsAsync()).FirstOrDefault(c => c.ClientId == request.ClientId);
            if (client != null)
            {
                try
                {
                    await bot.SendMessage(client.TelegramId,
                        $"✅ Ваша заявка одобрена!\n{request.DesiredDate:dd.MM.yyyy} в {request.DesiredTime:HH:mm}",
                        cancellationToken: ct);
                }
                catch { }
            }

            await bot.SendMessage(adminChatId, "✅ Заявка одобрена, запись создана.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
        }

        private static async Task MarkAppointmentDoneAsync(ITelegramBotClient bot, long adminChatId, Guid appointmentId, CancellationToken ct)
        {
            await AppointmentService.MarkCompletedAsync(appointmentId);
            var all = await SupabaseConfig.GetClient().From<Models.Appointment>().Get();
            var apt = (all.Models ?? new List<Models.Appointment>()).FirstOrDefault(a => a.AppointmentId == appointmentId);
            if (apt == null) return;

            var client = (await ClientService.GetAllClientsAsync()).FirstOrDefault(c => c.ClientId == apt.ClientId);
            if (client != null)
            {
                try
                {
                    await ReviewFlow.BeginReviewAsync(bot, client.TelegramId, client.TelegramId, appointmentId, ct);
                }
                catch { }
            }

            await bot.SendMessage(adminChatId, "Запись отмечена как выполненная. Клиенту отправлен запрос на отзыв.", cancellationToken: ct);
        }
    }
}
