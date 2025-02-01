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

        internal async Task ShowSettingsMenuAsync(long chatId)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📅 По дате", $"set_sort_{Models.SortType.ByDate}"),
                    InlineKeyboardButton.WithCallbackData("✅ По статусу", $"set_sort_{Models.SortType.ByStatus}"),
                    InlineKeyboardButton.WithCallbackData("🔤 По названию", $"set_sort_{Models.SortType.ByTitle}")
                }
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
    }
}
