using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace z_planner_bot.Views
{
    internal class UserSettingsView
    {
        private readonly ITelegramBotClient _botClient;

        public UserSettingsView(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        internal async Task ShowSortSettingsMenuAsync(long chatId)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("📅 По дате", $"set_sort_{Models.SortType.ByDate}") },
                new[] { InlineKeyboardButton.WithCallbackData("✅ По статусу", $"set_sort_{Models.SortType.ByStatus}") },
                new[] { InlineKeyboardButton.WithCallbackData("🔤 По названию", $"set_sort_{Models.SortType.ByTitle}") }
            });

            await SendMessageAsync(chatId, "Выберите способ сортировки по умолчанию:", inlineKeyboard);
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

        internal async Task GetTimeZoneAsync(long chatId)
        {
            var buttons = new List<InlineKeyboardButton>();

            for (int i = -12; i <= 14; i++)
            {
                string label = i >= 0 ? $"(UTC+{i}:00)" : $"(UTC{i}:00)";
                string data = $"set_timezone_{i}";

                buttons.Add(InlineKeyboardButton.WithCallbackData(label, data));
            }

            // Разбиваем на строки по 3 кнопки
            var rows = buttons.Select((btn, index) => new { btn, index })
                              .GroupBy(x => x.index / 3)
                              .Select(g => g.Select(x => x.btn).ToArray())
                              .ToList();

            await SendMessageAsync(chatId, "Выберите свой часовой пояс:", new InlineKeyboardMarkup(rows));
        }
    }
}
