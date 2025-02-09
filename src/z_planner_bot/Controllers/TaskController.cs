using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using z_planner_bot.Views;

namespace z_planner_bot.Controllers
{
    internal class TaskController : ControllerBase
    {
        private readonly IDbContextFactory<Models.AppDbContext> _dbContextFactory;
        private readonly TaskView _taskView;
        private readonly Dictionary<long, TaskInputStage> _userStages = new();
        private readonly Dictionary<long, (string Title, string? Description, DateTime? DueDate)> _tempTasks = new();
        private readonly Dictionary<long, int> _editTaskIds = new();
        private enum TaskInputStage { None, Title, Description, DueDate, Confirmation }

        public TaskController(IDbContextFactory<Models.AppDbContext> dbContextFactory, TaskView taskView)
        {
            _dbContextFactory = dbContextFactory;
            _taskView = taskView;
        }

        public async Task SendTasksAsync(long chatId, List<Models.Task> tasks, Models.SortType sortType, string timeZone)
        {
            await _taskView.SendTasksListAsync(chatId, tasks, sortType, timeZone);
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
                DueDate = dueDate == null ? null : dueDate.Value.ToUniversalTime()
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
            var timeZone = settings == null ? "3" : settings.TimeZone;

            await SendTasksAsync(chatId, tasks, sortType, timeZone);
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
            var timeZone = settings == null ? "3" : settings.TimeZone;

            await SendTasksAsync(chatId, overdueTasks, sortType, timeZone);
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

        public async Task HandleEditTaskAsync(long chatId, long userId, int taskId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var task = await dbContext.Tasks
            .FirstOrDefaultAsync(task => task.UserId == userId && task.Id == taskId);

            if (task == null)
            {
                await _taskView.SendMessageAsync(chatId, "Задача не найдена. 😕");
                return;
            }

            // Сохраняем существующую задачу во временное хранилище
            _tempTasks[chatId] = (task.Title, task.Description, task.DueDate);
            // Добавляем ID задачи в отдельный словарь
            _editTaskIds[chatId] = taskId;

            // Запускаем последовательность ввода
            _userStages[chatId] = TaskInputStage.Title;
            await _taskView.SendMessageAsync(chatId, $"Текущее название: {task.Title}\nВведите новое название задачи:");
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
                    await HandleListTasksAsync(chatId, userId);
                    break;
                case "toggle":
                    await HandleToggleTaskAsync(chatId, userId, taskId);
                    await HandleListTasksAsync(chatId, userId);
                    break;
                case "edit":
                    await HandleEditTaskAsync(chatId, userId, taskId);
                    break;

                default:
                    await _taskView.SendMessageAsync(chatId, "Не понял вас 🤔");
                    await HandleListTasksAsync(chatId, userId);
                    break;
            }
        }

        public async Task HandleUserInputAsync(long chatId, long userId, string text)
        {
            if (text.ToLower() == "отмена")
            {
                await _taskView.SendMessageAsync(chatId, "Создание задачи отменено");
                _userStages.Remove(chatId);
                _tempTasks.Remove(chatId);
                _editTaskIds.Remove(chatId);
                return;
            }

            // Проверяем, есть ли этап ввода или редактируемая задача
            if (!_userStages.ContainsKey(chatId))
            {
                if (_editTaskIds.ContainsKey(chatId))
                {
                    // Если это редактирование, начинаем с ввода названия
                    _userStages[chatId] = TaskInputStage.Title;
                }
                else
                {
                    await _taskView.SendMessageAsync(chatId, "Начните с команды 'Добавить задачу'.");
                    return;
                }
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
                    _editTaskIds.Remove(chatId);
                    break;
            }
        }

        private async Task HandleTitleInputAsync(long chatId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await _taskView.SendMessageAsync(chatId, "Не понял вас 🤔");
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
            await _taskView.SendMessageAsync(chatId,
                "Введите дату и/или время дедлайна:\n" +
                "• ГГГГ-ММ-ДД [ЧЧ:ММ]\n" +
                "• ДД.ММ.ГГГГ [ЧЧ:ММ]\n" +
                "• ДД/ММ/ГГГГ [ЧЧ:ММ]\n" +
                "• ЧЧ:ММ (для сегодня/завтра)\n" +
                "• завтра\n" +
                "• послезавтра\n" +
                "• через N дней\n" +
                "• или 'Пропустить'");
        }

        private async Task HandleDueDateInputAsync(long chatId, string text)
        {
            DateTime? dueDate = null;

            // Получаем часовой пояс пользователя
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var settings = await dbContext.UserSettings
                .FirstOrDefaultAsync(us => us.UserId == chatId);

            var userTimeZoneOffset = int.Parse(settings?.TimeZone ?? "3"); // По умолчанию UTC+3

            if (text.ToLower() != "пропустить")
            {
                dueDate = ParseDate(text);
                if (dueDate == null)
                {
                    await _taskView.SendMessageAsync(chatId, "Не понял вас 🤔");
                    return;
                }

                // Конвертируем локальное время пользователя в UTC
                // Например, если пользователь ввел 15:00 в UTC+3, то в UTC это будет 12:00
                dueDate = dueDate.Value.AddHours(-userTimeZoneOffset);
            }

            // В сводке показываем локальное время (то, что ввел пользователь)
            var localDueDate = dueDate?.AddHours(userTimeZoneOffset); // Не меняем время для отображения
            var summary = $"Название: {_tempTasks[chatId].Title}\n" +
                          $"Описание: {_tempTasks[chatId].Description ?? "нет"}\n" +
                          $"Дедлайн: {(localDueDate?.ToString("dd.MM.yyyy HH:mm") ?? "нет")}\n\n" +
                          "Добавить задачу? (Да/Нет)";

            await _taskView.SendMessageAsync(chatId, summary);

            // Переходим в режим подтверждения и сохраняем UTC время
            _userStages[chatId] = TaskInputStage.Confirmation;
            _tempTasks[chatId] = (_tempTasks[chatId].Title, _tempTasks[chatId].Description, dueDate);
        }

        private async Task HandleConfirmationInputAsync(long chatId, long userId, string text)
        {
            if (text.ToLower() == "да")
            {
                if (_editTaskIds.ContainsKey(chatId))
                {
                    // Обновляем существующую задачу
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    var task = await dbContext.Tasks
                        .FirstOrDefaultAsync(t => t.UserId == userId && t.Id == _editTaskIds[chatId]);

                    if (task != null)
                    {
                        task.Title = _tempTasks[chatId].Title;
                        task.Description = _tempTasks[chatId].Description;
                        task.DueDate = _tempTasks[chatId].DueDate;
                        await dbContext.SaveChangesAsync();
                        await _taskView.SendMessageAsync(chatId, "Задача обновлена ✅");
                    }
                    _editTaskIds.Remove(chatId);
                }
                else
                {
                    // Создаем новую задачу
                    await HandleAddTaskAsync(chatId, userId, _tempTasks[chatId].Title, _tempTasks[chatId].Description, _tempTasks[chatId].DueDate);
                }
            }
            else if (text.ToLower() == "нет")
            {
                await _taskView.SendMessageAsync(chatId, _editTaskIds.ContainsKey(chatId) ? "Изменения отменены." : "Создание задачи отменено.");
                _editTaskIds.Remove(chatId);
            }
            else
            {
                await _taskView.SendMessageAsync(chatId, "Не понял вас 🤔");
                return;
            }

            _userStages.Remove(chatId);
            _tempTasks.Remove(chatId);
        }

        public async Task HandleAddTaskPromptAsync(long chatId)
        {
            if (_userStages.ContainsKey(chatId))
                _userStages.Remove(chatId);

            _userStages.Add(chatId, TaskInputStage.Title);
            await _taskView.SendMessageAsync(chatId, "Введите название задачи:");
        }

        private DateTime? ParseDate(string input)
        {
            input = input.Trim().ToLower();

            // Относительные даты
            if (input == "завтра") return DateTime.Today.AddDays(1);
            if (input == "послезавтра") return DateTime.Today.AddDays(2);

            // Через N дней
            var throughDaysPattern = @"^через\s+(\d+)\s*(?:день|дня|дней)?$";
            var throughMatch = Regex.Match(input, throughDaysPattern);
            if (throughMatch.Success && int.TryParse(throughMatch.Groups[1].Value, out var days))
                return DateTime.Today.AddDays(days);

            // Стандартные форматы даты с опциональным временем
            var dateTimePatterns = new[]
            {
                // yyyy-MM-dd[ HH:mm]
                @"^(\d{4})-(\d{2})-(\d{2})(?:\s+(\d{1,2}):(\d{2}))?$",
                // dd.MM.yyyy[ HH:mm]
                @"^(\d{2})\.(\d{2})\.(\d{4})(?:\s+(\d{1,2}):(\d{2}))?$",
                // dd/MM/yyyy[ HH:mm]
                @"^(\d{2})/(\d{2})/(\d{4})(?:\s+(\d{1,2}):(\d{2}))?$"
            };

            foreach (var pattern in dateTimePatterns)
            {
                var match = Regex.Match(input, pattern);
                if (match.Success)
                {
                    try
                    {
                        int year, month, day;
                        if (pattern.StartsWith(@"^(\d{4})"))
                        {
                            // yyyy-MM-dd формат
                            year = int.Parse(match.Groups[1].Value);
                            month = int.Parse(match.Groups[2].Value);
                            day = int.Parse(match.Groups[3].Value);
                        }
                        else
                        {
                            // dd.MM.yyyy или dd/MM/yyyy формат
                            day = int.Parse(match.Groups[1].Value);
                            month = int.Parse(match.Groups[2].Value);
                            year = int.Parse(match.Groups[3].Value);
                        }

                        var date = new DateTime(year, month, day);

                        // Если указано время
                        if (match.Groups[4].Success && match.Groups[5].Success)
                        {
                            var hour = int.Parse(match.Groups[4].Value);
                            var minute = int.Parse(match.Groups[5].Value);

                            if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59)
                            {
                                date = date.AddHours(hour).AddMinutes(minute);
                            }
                            else
                            {
                                return null; // Неверный формат времени
                            }
                        }
                        else
                        {
                            // Если время не указано, устанавливаем конец дня
                            date = date.AddHours(23).AddMinutes(59);
                        }

                        // Проверяем, что дата не в прошлом
                        if (date < DateTime.Today)
                        {
                            return null;
                        }

                        return date;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            // Только время (для сегодняшнего дня)
            var timePattern = @"^(\d{1,2}):(\d{2})$";
            var timeMatch = Regex.Match(input, timePattern);
            if (timeMatch.Success)
            {
                try
                {
                    var hour = int.Parse(timeMatch.Groups[1].Value);
                    var minute = int.Parse(timeMatch.Groups[2].Value);

                    if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59)
                    {
                        var date = DateTime.Today.AddHours(hour).AddMinutes(minute);

                        // Если указанное время уже прошло, переносим на завтра
                        if (date < DateTime.Now)
                        {
                            date = date.AddDays(1);
                        }

                        return date;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }
}
