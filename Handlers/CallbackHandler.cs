using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
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

            await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);

            var session = SessionStore.GetOrCreate(chatId);

            if (data.StartsWith("cat:"))
            {
                await BookingFlow.ShowCategoryServicesAsync(bot, chatId, int.Parse(data[4..]), ct);
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
                var idx = int.Parse(data[4..]);
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
                var idx = int.Parse(data[4..]);
                if (idx >= 0 && idx < session.CachedServiceIds.Count)
                {
                    var id = session.CachedServiceIds[idx];
                    if (session.Booking.SelectedServiceIds.Contains(id)) session.Booking.SelectedServiceIds.Remove(id);
                    else session.Booking.SelectedServiceIds.Add(id);
                    await BookingFlow.ShowAdditionalAsync(bot, chatId, ct);
                }
                return;
            }

            if (data.StartsWith("date:"))
            {
                await BookingFlow.ShowTimesAsync(bot, chatId, int.Parse(data[5..]), ct);
                return;
            }

            if (data.StartsWith("slot:"))
            {
                var idx = int.Parse(data[5..]);
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
                await BookingFlow.SubmitAsync(bot, chatId, userId, cq.From.Username, cq.From.FirstName, ct);
                return;
            }

            if (data.StartsWith("req_ok:"))
            {
                if (!isAdmin) return;
                var reqId = session.CachedRequestIds[int.Parse(data[7..])];
                await ApproveRequestAsync(bot, chatId, reqId, ct);
                return;
            }

            if (data.StartsWith("req_no:"))
            {
                if (!isAdmin) return;
                session.TargetRequestId = session.CachedRequestIds[int.Parse(data[7..])];
                session.State = SessionState.Admin_RejectReason;
                await bot.SendMessage(chatId, "Укажите причину отклонения заявки:", cancellationToken: ct);
                return;
            }

            if (data.StartsWith("apt_done:"))
            {
                if (!isAdmin) return;
                var aptId = session.CachedAppointmentIds[int.Parse(data[9..])];
                await MarkAppointmentDoneAsync(bot, chatId, aptId, ct);
                return;
            }

            if (data.StartsWith("rec_can:"))
            {
                var reqId = session.CachedRequestIds[int.Parse(data[8..])];
                session.TargetRequestId = reqId;
                session.State = SessionState.Cancel_EnterReason;
                await bot.SendMessage(chatId, "Укажите причину отмены (мастер получит уведомление):", cancellationToken: ct);
                return;
            }

            if (data.StartsWith("rec_chg:"))
            {
                var reqId = session.CachedRequestIds[int.Parse(data[8..])];
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
                var clientId = session.CachedClientIds[int.Parse(data[8..])];
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
