using System.Text;
using Telegram.Bot;
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

        public async Task SendMessageAsync(long chatId, string text, IReplyMarkup? replyMarkup = null)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                replyMarkup: replyMarkup
                );
        }

        public async Task SendTasksListAsync(long chatId, List<Models.Task> tasks)
        {
            if (tasks.Count == 0)
            {
                await SendMessageAsync(chatId, "Задачи не найдены.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Ваши задачи:");

            for (int i = 0; i < tasks.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {tasks[i].Title} {(tasks[i].IsCompleted ? "✅" : "")}");
                if (!string.IsNullOrEmpty(tasks[i].Description))
                    sb.AppendLine($"    Описание: {tasks[i].Description}");
            }

            var inlineKeyboard = new InlineKeyboardMarkup(tasks.Select((t, i) => new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Удалить", $"delete_{t.Id}"),
                InlineKeyboardButton.WithCallbackData(t.IsCompleted ? "🔄 Возобновить" : "✅ Выполнено", $"toggle_{t.Id}"),
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"edit_{t.Id}")
            }));

            await SendMessageAsync(chatId, $"Ваши задачи:\n{sb.ToString()}", inlineKeyboard);
        }

        public async Task ShowMainMenuAsync(long chatId)
        {
            var replyKeyboard = new ReplyKeyboardMarkup(new[] 
            {
                new[] { new KeyboardButton("Добавить задачу") },
                new[] { new KeyboardButton("Мои задачи") },
                new[] { new KeyboardButton("Просроченные задачи") },
                new[] { new KeyboardButton("Помощь") }
            })
            { ResizeKeyboard =  true };

            await SendMessageAsync(chatId, "Выберите действие: ", replyKeyboard);
        }

        public async Task SendHelpAsync(long chatId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Доступные команды:");
            sb.AppendLine("- Добавить задачу: Нажмите кнопку 'Добавить задачу' и введите наименование и описание (опционально).");
            sb.AppendLine("- Мои задачи: Нажмите кнопку 'Мои задачи'.");
            sb.AppendLine("- Просроченные задачи: Нажмите кнопку 'Просроченные задачи'.");
            sb.AppendLine("- Управление задачами: Используйте кнопки под каждой задачей.");

            await SendMessageAsync(chatId, sb.ToString());
        }
    }
}
