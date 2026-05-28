using Supabase;
using System;
using System.Threading.Tasks;

namespace TelegramBot.Services
{
    /// <summary>
    /// Конфигурационный класс для работы с Supabase.
    /// Инициализирует соединение с базой данных и предоставляет глобальный доступ к клиенту API.
    /// </summary>
    public static class SupabaseConfig
    {
        public static Supabase.Client Client { get; private set; }

        public static async Task InitializeAsync(string url, string key)
        {
            var options = new SupabaseOptions { AutoConnectRealtime = false };
            Client = new Supabase.Client(url, key, options);
            await Client.InitializeAsync();
            Console.WriteLine("Log: Supabase успешно подключён");
        }
    }
}