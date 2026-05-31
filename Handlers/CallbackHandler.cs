using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot;
using TelegramBot.Constants;
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
            var isAdmin = userId == BotConfig.AdminTelegramId;

            try
            {
                await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
                await HandleCallbackCoreAsync(bot, chatId, userId, isAdmin, data, cq.From.Username, cq.From.FirstName, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Callback error [{data}]: {ex.Message}");
                try
                {
                    await bot.SendMessage(chatId,
                        "⚠️ Не удалось обработать кнопку. Нажмите /start или «◀️ Меню».",
                        cancellationToken: ct);
                }
                catch { }
            }
        }

        private static async Task HandleCallbackCoreAsync(ITelegramBotClient bot, long chatId, long userId, bool isAdmin, string data, string? username, string? firstName, CancellationToken ct)
        {
            var session = SessionStore.GetOrCreate(chatId);

            if (data.StartsWith("cat:") && int.TryParse(data[4..], out var catIdx))
            {
                await BookingFlow.ShowCategoryServicesAsync(bot, chatId, catIdx, ct);
                return;
            }

            if (data == "add:menu")
            {
                await BookingFlow.ShowAdditionalAsync(bot, chatId, ct);
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

            if (data.StartsWith("add:"))
            {
                if (data == "add:done") { await BookingFlow.ShowDatesAsync(bot, chatId, ct); return; }
                if (!int.TryParse(data[4..], out var addIdx) || addIdx < 0 || addIdx >= session.CachedServiceIds.Count)
                {
                    await StaleCallbackAsync(bot, chatId, ct);
                    return;
                }
                if (addIdx >= 0 && addIdx < session.CachedServiceIds.Count)
                {
                    var id = session.CachedServiceIds[addIdx];
                    if (session.Booking.SelectedServiceIds.Contains(id)) session.Booking.SelectedServiceIds.Remove(id);
                    else session.Booking.SelectedServiceIds.Add(id);
                    await BookingFlow.ShowAdditionalAsync(bot, chatId, ct);
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
                if (!isAdmin) return;
                if (!int.TryParse(data[7..], out var ri) || ri >= session.CachedRequestIds.Count) { await StaleCallbackAsync(bot, chatId, ct); return; }
                var reqId = session.CachedRequestIds[ri];
                await ApproveRequestAsync(bot, chatId, reqId, ct);
                return;
            }

            if (data.StartsWith("req_no:"))
            {
                if (!isAdmin) return;
                if (!int.TryParse(data[7..], out var rj) || rj >= session.CachedRequestIds.Count) { await StaleCallbackAsync(bot, chatId, ct); return; }
                session.TargetRequestId = session.CachedRequestIds[rj];
                session.State = SessionState.Admin_RejectReason;
                await bot.SendMessage(chatId, "Укажите причину отклонения заявки:", cancellationToken: ct);
                return;
            }

            if (data.StartsWith("apt_done:"))
            {
                if (!isAdmin) return;
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
                if (!isAdmin) return;
                if (!int.TryParse(data[8..], out var di) || di >= session.CachedClientIds.Count) { await StaleCallbackAsync(bot, chatId, ct); return; }
                var clientId = session.CachedClientIds[di];
                await ClientService.DeleteClientAsync(clientId);
                await bot.SendMessage(chatId, "Клиент удалён.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                return;
            }

            if (data == "cli_clear")
            {
                if (!isAdmin) return;
                await ClientService.ClearAllClientsAsync();
                await bot.SendMessage(chatId, "Клиентская база очищена.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
                return;
            }

            if (data.StartsWith("prof:") && isAdmin)
            {
                session.State = SessionState.Admin_EditProfile;
                session.AdminEditField = data[5..];
                await bot.SendMessage(chatId, "Введите новое значение:", cancellationToken: ct);
            }
        }

        private static async Task StaleCallbackAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            SessionStore.Reset(chatId);
            await bot.SendMessage(chatId,
                "Сессия устарела. Нажмите «Услуги» или /start, чтобы начать заново.",
                cancellationToken: ct);
        }

        private static async Task ApproveRequestAsync(ITelegramBotClient bot, long adminChatId, Guid requestId, CancellationToken ct)
        {
            var request = await RequestService.GetByIdAsync(requestId);
            if (request == null) return;

            var master = await MasterService.GetMasterProfileAsync();
            if (master == null)
            {
                await bot.SendMessage(adminChatId, "Сначала создайте профиль мастера.", cancellationToken: ct);
                return;
            }

            var wd = (await ScheduleService.GetWorkingDatesAsync(DateOnly.MinValue, DateOnly.MaxValue))
                .FirstOrDefault(w => w.Date == request.DesiredDate);
            if (wd == null)
            {
                await bot.SendMessage(adminChatId, "Дата записи не найдена в расписании.", cancellationToken: ct);
                return;
            }

            var slots = await ScheduleService.GetFreeSlotsAsync(wd.DateId);
            var slot = slots.FirstOrDefault(s => s.Time == request.DesiredTime)
                ?? slots.FirstOrDefault();

            if (slot == null)
            {
                await bot.SendMessage(adminChatId, "Нет свободного слота на это время.", cancellationToken: ct);
                return;
            }

            await RequestService.UpdateRequestStatusAsync(requestId, RequestStatus.Approved);
            await AppointmentService.CreateFromRequestAsync(request, master.MasterId, wd.DateId, slot.TimeSlotId);

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

            await bot.SendMessage(adminChatId, "Заявка одобрена, запись создана.", replyMarkup: Keyboards.CreateAdminMenuKeyboard(), cancellationToken: ct);
        }

        private static async Task MarkAppointmentDoneAsync(ITelegramBotClient bot, long adminChatId, Guid appointmentId, CancellationToken ct)
        {
            await AppointmentService.MarkCompletedAsync(appointmentId);
            var all = await SupabaseConfig.Client.From<Models.Appointment>().Get();
            var apt = (all.Models ?? new List<Models.Appointment>()).FirstOrDefault(a => a.AppointmentId == appointmentId);
            if (apt == null) return;

            var client = (await ClientService.GetAllClientsAsync()).FirstOrDefault(c => c.ClientId == apt.ClientId);
            if (client != null)
            {
                try
                {
                    await bot.SendMessage(client.TelegramId,
                        "💅 Работа выполнена! Пожалуйста, оставьте отзыв — напишите текст сообщением.",
                        cancellationToken: ct);
                    var s = SessionStore.GetOrCreate(client.TelegramId);
                    s.State = SessionState.Review_EnterText;
                    s.TargetAppointmentId = appointmentId;
                }
                catch { }
            }

            await bot.SendMessage(adminChatId, "Запись отмечена как выполненная. Клиенту отправлен запрос на отзыв.", cancellationToken: ct);
        }
    }
}
