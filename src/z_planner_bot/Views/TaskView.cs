using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace z_planner_bot.Views
{
    internal class TaskView
    {
        private readonly ITelegramBotClient _botClient;

        public TaskView(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        internal async Task SendMessageAsync(long chatId, string text, IReplyMarkup? replyMarkup = null, ParseMode parseMode = ParseMode.None)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: parseMode,
                replyMarkup: replyMarkup
                );
        }

        internal async Task SendTasksListAsync(long chatId, List<Models.Task> tasks, Models.SortType sortType, string timeZone)
        {
            if (!tasks.Any())
            {
                await SendMessageAsync(chatId, "Задачи не найдены.");
                return;
            }

            // Определяем тип сортировки
            switch (sortType)
            {
                case Models.SortType.ByDate:
                    tasks = tasks.OrderBy(t => t.CreatedAt).ToList();
                    break;
                case Models.SortType.ByStatus:
                    tasks = tasks.OrderBy(t => t.IsCompleted).ToList();
                    break;
                case Models.SortType.ByTitle:
                    tasks = tasks.OrderBy(t => t.Title).ToList();
                    break;
            }

            // Выводим отсортированный список
            foreach (var task in tasks)
            {
                var taskText = $"📌 <b>{task.Title}</b> {(task.IsCompleted ? "✅" : "")}";
                if (!string.IsNullOrEmpty(task.Description))
                    taskText += $"<br>📝 <i>{task.Description}</i>";

                if (task.DueDate.HasValue && !string.IsNullOrEmpty(timeZone))
                {
                    if (TimeSpan.TryParse(timeZone, out TimeSpan offset))
                    {
                        var localTime = task.DueDate.Value.Add(offset);
                        taskText += $"\n📝 <i>{localTime}</i>";
                    }
                    else
                    {
                        taskText += "\n⚠ Не указано";
                    }
                }

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("❌ Удалить", $"delete_{task.Id}"),
                        InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"edit_{task.Id}")
                        
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(task.IsCompleted ? "🔄 Возобновить" : "✅ Выполнено", $"toggle_{task.Id}")
                    }
                });

                await SendMessageAsync(chatId, taskText, inlineKeyboard, parseMode: ParseMode.Html);
            }
        }
    }
}
