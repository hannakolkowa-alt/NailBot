using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Services;
using TelegramBot.UI;

namespace TelegramBot.Handlers
{
    /// <summary>
    /// Обработчик текстовых команд и кнопок, доступных только администратору (мастеру).
    /// Управляет логикой редактирования профиля, прайса, графика и работы с заявками.
    /// </summary>
    public static class AdminHandler
    {
        public static async Task HandleAsync(ITelegramBotClient botClient, long chatId, string text, CancellationToken ct)
        {
            var kb = Keyboards.CreateAdminMenuKeyboard();

            switch (text)
            {
                case "мой профиль":
                    //await botClient.SendMessage(chatId, "👤 Редактирование профиля мастера (заглушка)", replyMarkup: kb, cancellationToken: ct);

                    // 1. Пытаемся получить профиль мастера
                    var profile = await MasterService.GetMasterProfileAsync();

                    if (profile == null)
                    {
                        // Если профиля нет — запускаем процесс создания (например, переводим бота в режим ожидания имени)
                        // Для демонстрации авто-сохранения сразу генерируем запись:
                        Guid newMasterId = Guid.NewGuid(); // В реале тут лучше привязаться к какому-то ID

                        bool created = await MasterService.SaveOrUpdateProfileAsync(
                            newMasterId,
                            "Новый Мастер",
                            "master_username", // Просто строка с юзернеймом по умолчанию или затычка
                            "0 лет",
                            "Описание отсутствует"
                        );

                        await botClient.SendMessage(chatId, "✨ Профиль не был найден и автоматически создан! Используйте кнопки для его редактирования.", replyMarkup: kb, cancellationToken: ct);
                    }
                    else
                    {
                        // Если профиль есть — выводим данные и кнопки "Редактировать Имя/Опыт"
                        string info = $"👤 Ваш профиль:\n\nИмя: {profile.Name}\nОпыт: {profile.Experience}\nОбо мне: {profile.Description}";
                        await botClient.SendMessage(chatId, info, replyMarkup: kb, cancellationToken: ct);
                    }
                    
                    break;
                case "галерея":
                    //await botClient.SendMessage(chatId, "🖼️ Пришлите фото для добавления", replyMarkup: kb, cancellationToken: ct);

                    var galleryKeyboard = new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(new[]
                    {
                        new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("➕ Добавить фото", "gallery_add") },
                        new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("❌ Удалить фото", "gallery_delete") }
                    });

                    await botClient.SendMessage(chatId, "🖼️ Управление портфолио. Выберите действие:", replyMarkup: galleryKeyboard, cancellationToken: ct);
                    
                    break;
                case "прайс":
                    await botClient.SendMessage(chatId, "💰 Редактирование прайса", replyMarkup: kb, cancellationToken: ct);
                    break;
                case "график":
                    await botClient.SendMessage(chatId, "📅 Редактирование графика", replyMarkup: kb, cancellationToken: ct);
                    break;
                case "заявки":
                    await botClient.SendMessage(chatId, "Новых заявок пока нет.", replyMarkup: kb, cancellationToken: ct);
                    break;
                case "записи":
                    await botClient.SendMessage(chatId, "📋 Все записи клиентов", replyMarkup: kb, cancellationToken: ct);
                    break;
                case "отзывы":
                    await botClient.SendMessage(chatId, "⭐ Управление отзывами", replyMarkup: kb, cancellationToken: ct);
                    break;
                default:
                    await botClient.SendMessage(chatId, "Неизвестная команда админа.", replyMarkup: kb, cancellationToken: ct);
                    break;
            }
        }
    }
}