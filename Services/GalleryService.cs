using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TelegramBot.Models;

namespace TelegramBot.Services
{
    /// <summary>
    /// Сервис для работы с портфолио и медиа-файлами мастера.
    /// Отвечает за извлечение ссылок на фото, добавление новых работ и их удаление из Supabase.
    /// </summary>
    public static class GalleryService
    {
        /// <summary>
        /// Возвращает список всех фотографий работ конкретного мастера.
        /// </summary>
        /// <param name="masterId">Идентификатор мастера в базе данных.</param>
        /// <returns>Список объектов Gallery или пустой список, если фото не найдены.</returns>
        public static async Task<List<Gallery>> GetMasterPhotosAsync(Guid masterId)
        {
            try
            {
                var response = await SupabaseConfig.GetClient()
                    .From<Gallery>()
                    .Where(g => g.MasterId == masterId)
                    .Get();

                return response.Models ?? new List<Gallery>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении галереи: {ex.Message}");
                return new List<Gallery>();
            }
        }

        /// <summary>
        /// Добавляет запись о новой фотографии в таблицу gallery.
        /// </summary>
        /// <param name="masterId">Идентификатор мастера, загрузившего фото.</param>
        /// <param name="photoUrl">Прямая ссылка на файл изображения (из Supabase Storage или Telegram File ID).</param>
        /// <param name="description">Необязательное описание работы (например, тип дизайна, материалы).</param>
        /// <returns>True, если запись успешно создана в БД, иначе False.</returns>
        public static async Task<bool> AddPhotoAsync(Guid masterId, string photoUrl, string description = null)
        {
            try
            {
                var photo = new Gallery
                {
                    MasterId = masterId,
                    PhotoUrl = photoUrl,
                    Description = description,
                    CreatedAt = DateTime.UtcNow
                };

                var response = await SupabaseConfig.GetClient().From<Gallery>().Insert(photo);
                return response.Models.Count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка добавления фото в БД: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Удаляет фотографию из таблицы gallery по её уникальному идентификатору.
        /// </summary>
        /// <param name="photoId">Идентификатор удаляемой фотографии.</param>
        /// <returns>True, если запись была успешно удалена, иначе False.</returns>
        public static async Task<bool> DeletePhotoAsync(Guid photoId)
        {
            try
            {
                // 1. Делаем запрос на удаление. В Postgrest фильтрация идет через лямбду, 
                // но метод Delete() должен вызываться в конце этой цепочки.
                await SupabaseConfig.GetClient()
                    .From<Gallery>()
                    .Where(g => g.PhotoId == photoId)
                    .Delete();

                // 2. Если запрос не выбросил Exception, значит строка успешно удалена
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка удаления фото из БД: {ex.Message}");
                return false;
            }
        }
    }
}