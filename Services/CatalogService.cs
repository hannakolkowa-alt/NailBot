using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TelegramBot.Models;

namespace TelegramBot.Services
{
    /// <summary>
    /// Сервис управления каталогом услуг.
    /// Отвечает за извлечение списка доступных процедур, прайс-листа 
    /// и связанных с ними категорий из Supabase.
    /// </summary>
    public static class CatalogService
    {
        public static async Task<List<Service>> GetAllServicesAsync()
        {
            var response = await SupabaseConfig.Client.From<Service>().Get();
            return response.Models ?? new List<Service>();
        }

        public static async Task<List<Service>> GetRequestServicesAsync(Guid requestId)
        {
            var items = await SupabaseConfig.Client.From<RequestItem>().Where(ri => ri.RequestId == requestId).Get();
            if (!items.Models.Any()) return new List<Service>();

            var serviceIds = items.Models.Select(i => i.ServiceId).ToList();
            var servicesResponse = await SupabaseConfig.Client.From<Service>()
                .Filter(s => s.ServiceId, Supabase.Postgrest.Constants.Operator.In, serviceIds).Get();

            return servicesResponse.Models ?? new List<Service>();
        }
    }
}