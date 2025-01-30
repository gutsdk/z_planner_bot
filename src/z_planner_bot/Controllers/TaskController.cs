using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;
using z_planner_bot.Views;

namespace z_planner_bot.Controllers
{
    internal class TaskController : ControllerBase
    {
        private readonly IDbContextFactory<Models.AppDbContext> _dbContextFactory;
        private readonly TaskView _taskView;
        private readonly Dictionary<long, int> _pendingEdits = new();

        public TaskController(IDbContextFactory<Models.AppDbContext> dbContextFactory, TaskView taskView)
        {
            _dbContextFactory = dbContextFactory;
            _taskView = taskView;
        }

        // Добавление задачи
        public async Task HandleAddTaskAsync(long chatId, long userId, string title, string? description = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var task = new Models.Task
            {
                UserId = userId,
                Title = title,
                Description = description
            };

            dbContext.Tasks.Add(task);
            await dbContext.SaveChangesAsync();

            await _taskView.SendMessageAsync(chatId, "Задача добавлена! 😎");
        }

        // Просмотр задач
        public async Task HandleListTasksAsync(long chatId, long userId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var tasks = await dbContext.Tasks
            .Where(task => task.UserId == userId)
            .ToListAsync();
            await _taskView.SendTasksListAsync(chatId, tasks);
        }

        // Просмотр просроченных задач
        public async Task HandleOverdueTasksAsync(long chatId, long userId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var currentDate = DateTime.UtcNow;
            var overdueTasks = await dbContext.Tasks
                .Where(task => task.UserId == userId && task.DueDate < currentDate && !task.IsCompleted)
                .ToListAsync();

            await _taskView.SendTasksListAsync(chatId, overdueTasks);
        }

        // Удаление задачи
        public async Task HandleDeleteTaskAsync(long chatId, long userId, int taskId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var task = await dbContext.Tasks
            .FirstOrDefaultAsync(task => task.UserId == userId && task.Id == taskId);

            if (task == null)
            {
                await _taskView.SendMessageAsync(chatId, "Задача не найдена. 😕");
                return;
            }

            dbContext.Tasks.Remove(task);
            await dbContext.SaveChangesAsync();

            await _taskView.SendMessageAsync(chatId, "Задача удалена! 😎");
        }

        // Переключение статуса задачи
        public async Task HandleToggleTaskAsync(long chatId, long userId, int taskId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var task = await dbContext.Tasks
                .FirstOrDefaultAsync(task => task.UserId == userId && task.Id == taskId);

            if (task == null)
            {
                await _taskView.SendMessageAsync(chatId, "Задача не найдена. 😕");
                return;
            }

            task.IsCompleted = !task.IsCompleted;
            await dbContext.SaveChangesAsync();

            await _taskView.SendMessageAsync(chatId, $"Задача отмечена как {(task.IsCompleted ? "выполненная" : "не выполненная")}. 😎");
        }

        public async Task HandleEditTaskAsync(long chatId, long userId, int taskId, string title, string? description = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var task = await dbContext.Tasks
            .FirstOrDefaultAsync(task => task.UserId == userId && task.Id == taskId);

            if (task == null)
            {
                await _taskView.SendMessageAsync(chatId, "Задача не найдена. 😕");
                return;
            }

            task.Title = title;
            if (description != null)
                task.Description = description;

            await dbContext.SaveChangesAsync();

            await _taskView.SendMessageAsync(chatId, "Задача обновлена ✅");
        }

        // Обработка callback-запросов (нажатия на кнопки)
        public async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var userId = callbackQuery.From.Id;
            var data = callbackQuery.Data;

            var action = data.Split('_')[0];
            var taskId = int.Parse(data.Split('_')[1]);

            switch (action)
            {
                case "delete":
                    await HandleDeleteTaskAsync(chatId, userId, taskId);
                    break;
                case "toggle":
                    await HandleToggleTaskAsync(chatId, userId, taskId);
                    break;
                case "edit":
                    _pendingEdits[chatId] = taskId;
                    await _taskView.SendMessageAsync(chatId, "Введите новое название и описание (опционально):");
                    break;
                default:
                    await _taskView.SendMessageAsync(chatId, "Не знаю таких действий. 🤔");
                    break;
            }

            // Обновляем список задач после выполнения действия
            await HandleListTasksAsync(chatId, userId);
        }

        // Проверка ожидания редактирования
        public bool IsWaitingForEdit(long chatId) => _pendingEdits.ContainsKey(chatId);

        // Получение ID задачи, которую требуется отредактировать
        public int GetPendingEditTaskId(long chatId)
        {
            if (_pendingEdits.TryGetValue(chatId, out var taskId))
            {
                _pendingEdits.Remove(chatId);
                return taskId;
            }
            return -1;
        }
    }
}
