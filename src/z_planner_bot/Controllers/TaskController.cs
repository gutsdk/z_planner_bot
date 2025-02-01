using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Telegram.Bot.Types;
using z_planner_bot.Views;

namespace z_planner_bot.Controllers
{
    internal class TaskController : ControllerBase
    {
        private readonly IDbContextFactory<Models.AppDbContext> _dbContextFactory;
        private readonly TaskView _taskView;
        private readonly Dictionary<long, int> _pendingEdits = new();
        private readonly Dictionary<long, TaskInputStage> _userStages = new();
        private readonly Dictionary<long, (string Title, string? Description, DateTime? DueDate)> _tempTasks = new();
        private enum TaskInputStage { None, Title, Description, DueDate, Confirmation }

        public TaskController(IDbContextFactory<Models.AppDbContext> dbContextFactory, TaskView taskView)
        {
            _dbContextFactory = dbContextFactory;
            _taskView = taskView;
        }

        public async Task SendTasksAsync(long chatId, List<Models.Task> tasks, Models.SortType sortType)
        {
            await _taskView.SendTasksListAsync(chatId, tasks, sortType);
        }

        // Добавление задачи
        public async Task HandleAddTaskAsync(long chatId, long userId, string title, string? description = null,
            DateTime? dueDate = null)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var task = new Models.Task
            {
                UserId = userId,
                Title = title,
                Description = description,
                DueDate = dueDate
            };

            dbContext.Tasks.Add(task);
            await dbContext.SaveChangesAsync();

            await _taskView.SendMessageAsync(chatId, "Задача добавлена! 😎");

            _userStages.Remove(chatId);
            _tempTasks.Remove(chatId);
        }

        // Просмотр задач
        public async Task HandleListTasksAsync(long chatId, long userId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var tasks = await dbContext.Tasks
            .Where(task => task.UserId == userId)
            .ToListAsync();

            var settings = await dbContext.UserSettings.FirstOrDefaultAsync(us => us.UserId == userId);
            var sortType = settings == null ? Models.SortType.ByStatus : settings.SortType;

            await SendTasksAsync(chatId, tasks, sortType);
        }

        // Просмотр просроченных задач
        public async Task HandleOverdueTasksAsync(long chatId, long userId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var currentDate = DateTime.UtcNow;
            var overdueTasks = await dbContext.Tasks
                .Where(task => task.UserId == userId && task.DueDate < currentDate && !task.IsCompleted)
                .ToListAsync();

            var settings = await dbContext.UserSettings.FirstOrDefaultAsync(us => us.UserId == userId);
            var sortType = settings == null ? Models.SortType.ByStatus : settings.SortType;

            await SendTasksAsync(chatId, overdueTasks, sortType);
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

        // Обработка callback-запросов связанных по задачам(нажатия на кнопки)
        public async Task HandleTaskCallbackAsync(CallbackQuery callbackQuery)
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
                    await _taskView.SendMessageAsync(chatId, "Не знаю таких действий 🤔");
                    break;
            }

            // Обновляем список задач после выполнения действия
            await HandleListTasksAsync(chatId, userId);
        }

        public async Task HandleUserInputAsync(long chatId, long userId, string text)
        {
            if (text.ToLower() == "отмена")
            {
                await _taskView.SendMessageAsync(chatId, "Создание задачи отменено");
                _userStages.Remove(chatId);
                _tempTasks.Remove(chatId);
                return;
            }

            if (!_userStages.ContainsKey(chatId))
            {
                await _taskView.SendMessageAsync(chatId, "Начните с команды 'Добавить задачу'.");
                return;
            }

            switch (_userStages[chatId])
            {
                case TaskInputStage.Title:
                    await HandleTitleInputAsync(chatId, text);
                    break;
                case TaskInputStage.Description:
                    await HandleDescriptionInputAsync(chatId, text);
                    break;
                case TaskInputStage.DueDate:
                    await HandleDueDateInputAsync(chatId, text);
                    break;
                case TaskInputStage.Confirmation:
                    await HandleConfirmationInputAsync(chatId, userId, text);
                    break;
                default:
                    await _taskView.SendMessageAsync(chatId, "Что-то пошло не так. Начните с команды 'Добавить задачу'.");
                    _userStages.Remove(chatId);
                    _tempTasks.Remove(chatId);
                    break;
            }
        }

        private async Task HandleTitleInputAsync(long chatId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await _taskView.SendMessageAsync(chatId, "❌ Название задачи не может быть пустым. Попробуйте еще раз:");
                return;
            }

            _tempTasks[chatId] = (text, null, null);
            _userStages[chatId] = TaskInputStage.Description;
            await _taskView.SendMessageAsync(chatId, "Добавить описание (или отправьте 'Пропустить'):");
        }

        private async Task HandleDescriptionInputAsync(long chatId, string text)
        {
            _tempTasks[chatId] = (_tempTasks[chatId].Title, text.ToLower() == "пропустить" ? null : text, null);
            _userStages[chatId] = TaskInputStage.DueDate;
            await _taskView.SendMessageAsync(chatId, "Введите дату дедлайна (в формате ГГГГ-ММ-ДД, ДД.ММ.ГГГГ или 'Пропустить'):");
        }

        private async Task HandleDueDateInputAsync(long chatId, string text)
        {
            DateTime? dueDate = null;
            if (text.ToLower() != "пропустить")
            {
                dueDate = ParseDate(text);
                if (dueDate == null)
                {
                    await _taskView.SendMessageAsync(chatId, "❌ Некорректный формат даты. Попробуйте еще раз:");
                    return;
                }
            }

            // Формируем сводку
            var summary = $"Название: {_tempTasks[chatId].Title}\n" +
                          $"Описание: {_tempTasks[chatId].Description ?? "нет"}\n" +
                          $"Дедлайн: {(dueDate?.ToString("dd.MM.yyyy") ?? "нет")}\n\n" +
                          "Добавить задачу? (Да/Нет)";

            await _taskView.SendMessageAsync(chatId, summary);

            // Переходим в режим подтверждения
            _userStages[chatId] = TaskInputStage.Confirmation;
            _tempTasks[chatId] = (_tempTasks[chatId].Title, _tempTasks[chatId].Description, dueDate);
        }

        private async Task HandleConfirmationInputAsync(long chatId, long userId, string text)
        {
            if (text.ToLower() == "да")
            {
                await HandleAddTaskAsync(chatId, userId, _tempTasks[chatId].Title, _tempTasks[chatId].Description, _tempTasks[chatId].DueDate);
            }
            else
            {
                await _taskView.SendMessageAsync(chatId, "Создание задачи отменено.");
                _userStages.Remove(chatId);
                _tempTasks.Remove(chatId);
            }
        }

        public async Task HandleAddTaskPromptAsync(long chatId)
        {
            await _taskView.SendMessageAsync(chatId, "Введите название задачи:");
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

        private DateTime? ParseDate(string input)
        {
            if (DateTime.TryParseExact(input, new[] { "yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate;
            }

            // Обработка относительных дат
            input = input.ToLower();
            if (input == "завтра") return DateTime.Today.AddDays(1);
            if (input == "послезавтра") return DateTime.Today.AddDays(2);
            if (input.StartsWith("через ") && int.TryParse(input.Substring(6), out var days))
            {
                return DateTime.Today.AddDays(days);
            }

            return null;
        }
    }
}
