using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramBot.Services;
using TelegramBot.Handlers;

namespace TelegramBot
{
    class Program
    {
        private static ITelegramBotClient _botClient;

        static async Task Main(string[] args)
        {
            // 1. Конфиг: локально — appsettings.json, на Render — переменные окружения
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var supabaseUrl = config["SupabaseUrl"];
            var supabaseKey = config["SupabaseKey"];
            var botToken = config["TelegramBotToken"];

            if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
            {
                Console.WriteLine("Ошибка: задайте SupabaseUrl и SupabaseKey (appsettings.json или переменные окружения).");
                return;
            }

            // 2. Инициализация БД
            await SupabaseConfig.InitializeAsync(supabaseUrl, supabaseKey);

            // 3. Запуск бота
            if (string.IsNullOrWhiteSpace(botToken))
            {
                Console.WriteLine("Ошибка: задайте TelegramBotToken (appsettings.json или переменную окружения).");
                return;
            }

            _botClient = new TelegramBotClient(botToken);

            try
            {
                var me = await _botClient.GetMe();
                Console.WriteLine($"Бот запущен! Имя: @{me.Username} | ID: {me.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка запуска: {ex.Message}");
                return;
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

            _botClient.StartReceiving(
                BotUpdateHandler.HandleUpdateAsync,
                BotUpdateHandler.HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот работает. Ожидание сообщений...");
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Остановка бота.");
            }
        }
    }
}