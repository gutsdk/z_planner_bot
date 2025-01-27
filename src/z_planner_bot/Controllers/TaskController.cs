using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;
using z_planner_bot.Views;

namespace z_planner_bot.Controllers
{
    internal class TaskController
    {
        private readonly AppDbContext _dbContext;
        private readonly TaskView _taskView;

        public TaskController(AppDbContext dbContext, TaskView taskView)
        {
            _dbContext = dbContext;
            _taskView = taskView;
        }

        // Добавление задачи
        public async Task HandleAddTaskAsync(long chatId, long userId, string title, string? description = null)
        {
            var task = new Models.Task
            {
                UserId = userId,
                Title = title,
                Description = description
            };

            _dbContext.Tasks.Add(task);
            await _dbContext.SaveChangesAsync();

            await _taskView.SendMessageAsync(chatId, "Задача добавлена! 😎");
        }

        // Просмотр задач
        public async Task HandleListTasksAsync(long chatId, long userId)
        {
            var tasks = await _dbContext.Tasks
                .Where(task => task.UserId == userId)
                .ToListAsync();
            await _taskView.SendTasksListAsync(chatId, tasks);
        }

        // Просмотр просроченных задач
        public async Task HandleOverdueTasksAsync(long chatId, long userId)
        {
            var currentDate = DateTime.UtcNow;
            var overdueTasks = await _dbContext.Tasks
                .Where(task => task.UserId == userId && task.DueDate < currentDate && !task.IsCompleted)
                .ToListAsync();

            await _taskView.SendTasksListAsync(chatId, overdueTasks);

        }

        // Удаление задачи
        public async Task HandleDeleteTaskAsync(long chatId, long userId, int taskId)
        {
            var task = await _dbContext.Tasks
                .FirstOrDefaultAsync(task => task.UserId == userId && task.Id == taskId);

            if (task == null)
            {
                await _taskView.SendMessageAsync(chatId, "Задача не найдена. 😕");
                return;
            }

            _dbContext.Tasks.Remove(task);
            await _dbContext.SaveChangesAsync();

            await _taskView.SendMessageAsync(chatId, "Задача удалена! 😎");
        }

        // Переключение статуса задачи
        public async Task HandleToggleTaskAsync(long chatId, long userId, int taskId)
        {
            var task = await _dbContext.Tasks
                .FirstOrDefaultAsync(task => task.UserId == userId && task.Id == taskId);

            if (task == null)
            {
                await _taskView.SendMessageAsync(chatId, "Задача не найдена. 😕");
                return;
            }

            task.IsCompleted = !task.IsCompleted;
            await _dbContext.SaveChangesAsync();

            await _taskView.SendMessageAsync(chatId, $"Задача отмечена как {(task.IsCompleted ? "выполненная" : "не выполненная")}. 😎");
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
                    await _taskView.SendMessageAsync(chatId, "Введите новое название и описание (опционально):");
                    break;
                default:
                    await _taskView.SendMessageAsync(chatId, "Не знаю таких действий. 🤔");
                    break;
            }

            // Обновляем список задач после выполнения действия
            await HandleListTasksAsync(chatId, userId);
        }
    }
}
