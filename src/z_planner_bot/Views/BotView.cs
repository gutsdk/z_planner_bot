using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace z_planner_bot.Views
{
    internal class BotView
    {
        private readonly ITelegramBotClient _botClient;

        public BotView(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        internal async Task ShowMainMenuAsync(long chatId)
        {
            var replyKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new[] { new KeyboardButton("Добавить задачу") },
                new[] { new KeyboardButton("Мои задачи") },
                new[] { new KeyboardButton("Просроченные задачи") },
                new[] { new KeyboardButton("Настройки") },
                new[] { new KeyboardButton("Помощь") }
            })
            { ResizeKeyboard = true };

            await SendMessageAsync(chatId, "Выберите действие: ", replyKeyboard);
        }

        internal async Task ShowHelpAsync(long chatId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Доступные команды:");
            sb.AppendLine("- Добавить задачу: Нажмите кнопку 'Добавить задачу' и введите наименование и описание (опционально).");
            sb.AppendLine("- Мои задачи: Нажмите кнопку 'Мои задачи'.");
            sb.AppendLine("- Просроченные задачи: Нажмите кнопку 'Просроченные задачи'.");
            sb.AppendLine("- Управление задачами: Используйте кнопки под каждой задачей.");
            sb.AppendLine("- Настройки: Выбирайте сортировку задач по умолчанию.");

            await SendMessageAsync(chatId, sb.ToString());
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
    }
}
