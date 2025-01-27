﻿using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using z_planner_bot.Controllers;
using z_planner_bot.Views;
using Telegram.Bot.Polling;

string? botToken = Environment.GetEnvironmentVariable("TG_TOKEN");
if (string.IsNullOrEmpty(botToken))
{
    Console.WriteLine("Ошибка: Токен не задан");
    return;
}

string? dBConnectionString = Environment.GetEnvironmentVariable("DB_CONNECT");
if (!string.IsNullOrEmpty(dBConnectionString))
{
    Console.WriteLine("Не получилось подключиться к БД");
    return;
}

var botClient = new TelegramBotClient(botToken);

var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(dBConnectionString)
    .Options;
using var dbContext = new AppDbContext(dbContextOptions);

var taskView = new TaskView(botClient);
var taskController = new TaskController(dbContext, taskView);

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Text)
    {
        var chatId = update.Message.Chat.Id;
        var userId = update.Message.From.Id;
        var text = update.Message.Text;

        switch (text)
        {
            case "Добавить задачу":
                await taskView.SendMessageAsync(chatId, "Введите наименование задачи:");
                break;
            case "Мои задачи":
                await taskController.HandleListTasksAsync(chatId, userId);
                break;
            case "Просроченные задачи":
                await taskController.HandleOverdueTasksAsync(chatId, userId); // todo
                break;
            case "Помощь":
                await taskView.SendHelpAsync(chatId);
                break;
            default:
                // Если пользователь вводит текст после запроса на добавление задачи
                if (text.Contains("\n"))
                {
                    var parts = text.Split('\n');
                    var title = parts[0];
                    var description = parts.Length > 1 ? parts[1] : null;
                    await taskController.HandleAddTaskAsync(chatId, userId, title, description);
                }
                else
                {
                    await taskController.HandleAddTaskAsync(chatId, userId, text);
                }
                break;
        } 
    }
    else if (update.Type == UpdateType.CallbackQuery)
    {
        await taskController.HandleCallbackQueryAsync(update.CallbackQuery);
    }
}

Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    Console.WriteLine($"Ошибка: {exception.Message}");
    return Task.CompletedTask;
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
await Task.Delay(Timeout.Infinite);
cts.Cancel();