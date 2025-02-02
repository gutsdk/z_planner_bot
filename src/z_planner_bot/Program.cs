using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using z_planner_bot.Controllers;
using z_planner_bot.Views;
using z_planner_bot.Models;
using Telegram.Bot.Polling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using z_planner_bot.Services;
using Microsoft.Extensions.Configuration;

//using IHost hostConfig = Host.CreateApplicationBuilder(args).Build();
//IConfiguration configuration = hostConfig.Services.GetRequiredService<IConfiguration>();

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Регистрируем AppDbContext
        services.AddDbContextFactory<AppDbContext>(options =>
        {
            //string? dBConnectionString = configuration.GetValue<string>("DB_CONNECT:Value");
            string? dBConnectionString = Environment.GetEnvironmentVariable("DB_CONNECT");

            if (string.IsNullOrEmpty(dBConnectionString))
            {
                Console.WriteLine("Ошибка: Строка подключения не указана");
                return;
            }

            options.UseNpgsql(dBConnectionString);
        });

        // Регистрируем бота
        services.AddSingleton<ITelegramBotClient>(lambda =>
        {
            //string? botToken = configuration.GetValue<string>("TG_TOKEN:Value");
            string? botToken = Environment.GetEnvironmentVariable("TG_TOKEN");

            if (string.IsNullOrEmpty(botToken))
            {
                throw new InvalidOperationException("Ошибка: Токен не указан");
            }
            return new TelegramBotClient(botToken);

        });

        // Регистрируем UserSettingsView
        services.AddSingleton<UserSettingsView>();
        // Регистрируем UserSettingsController
        services.AddSingleton<UserSettingsController>();

        // Регистрируем TaskView
        services.AddSingleton<TaskView>();
        // Регистрируем TaskController
        services.AddTransient<TaskController>();

        // Регистрируем BotView
        services.AddSingleton<BotView>();
        // Регистрируем BotController
        services.AddSingleton<BotController>();

        // Регистрируем фоновый сервис
        services.AddHostedService<DatabaseHealthCheckService>();
    });

using var host = builder.Build();
var serviceProvider = host.Services;

var botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
var botController = serviceProvider.GetRequiredService<BotController>();
var taskController = serviceProvider.GetRequiredService<TaskController>();
var userSettingsController = serviceProvider.GetRequiredService<UserSettingsController>();

async System.Threading.Tasks.Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Text)
    {
        var chatId = update.Message.Chat.Id;
        var userId = update.Message.From.Id;
        var text = update.Message.Text;

        if (taskController.IsWaitingForEdit(chatId))
        {
            int taskId = taskController.GetPendingEditTaskId(chatId);

            var parts = text.Split('|', 2);
            var newTitle = parts[0].Trim();
            var newDescription = parts.Length > 1 ? parts[1].Trim() : null;

            await taskController.HandleEditTaskAsync(chatId, userId, taskId, newTitle, newDescription);
            await taskController.HandleListTasksAsync(chatId, userId);
            return;
        }

        switch (text)
        {
            case "Добавить задачу":
                await taskController.HandleAddTaskPromptAsync(chatId);
                break;
            case "Мои задачи":
                await taskController.HandleListTasksAsync(chatId, userId);
                break;
            case "Просроченные задачи":
                await taskController.HandleOverdueTasksAsync(chatId, userId);
                break;
            case "Настройки":
                await userSettingsController.ShowSettingsAsync(chatId); 
                break;
            case "Помощь":
                await botController.ShowHelpAsync(chatId);
                break;
            case "/start":
                await botController.ShowMenuAsync(chatId);
                break;
            default:
                // Бот ожидает ввода от пользователя
                await taskController.HandleUserInputAsync(chatId, userId, text);
                break;
        }
    }
    else if (update.Type == UpdateType.CallbackQuery)
    {
        await botController.HandleCallbackQueryAsync(update.CallbackQuery);
    }
}

System.Threading.Tasks.Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    Console.WriteLine($"Ошибка: {exception.Message}");
    return System.Threading.Tasks.Task.CompletedTask;
}

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>()  // получаем все типы обновлений
};

using var cts = new CancellationTokenSource();
botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandleErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

Console.WriteLine("Бот запущен...");
await host.RunAsync();
cts.Cancel();