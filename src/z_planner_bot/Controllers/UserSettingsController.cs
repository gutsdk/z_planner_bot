using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;
using z_planner_bot.Views;

namespace z_planner_bot.Controllers
{
    internal class UserSettingsController : ControllerBase
    {
        private readonly IDbContextFactory<Models.AppDbContext> _dbContextFactory;
        private readonly UserSettingsView _usView;

        public UserSettingsController(IDbContextFactory<Models.AppDbContext> dbContextFactory, UserSettingsView view)
        {
            _dbContextFactory = dbContextFactory;
            _usView = view;
        }

        public async Task ShowSettingsAsync(long chatId)
        {
            await _usView.ShowSettingsMenuAsync(chatId);
        }

        public async Task HandleSettingsCallbackAsync(CallbackQuery callbackQuery)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var userId = callbackQuery.From.Id;
            var data = callbackQuery.Data;

            if (data.StartsWith("set_sort_"))
            {
                string sortTypeString = data.Replace("set_sort_", "");
                if (Enum.TryParse(sortTypeString, true, out Models.SortType sortType))
                {
                    await SetSortPreferenceAsync(userId, sortType);
                    await _usView.SendMessageAsync(chatId, $"✅ Сортировка установлена: {GetSortName(sortType)}");
                }
                else
                {
                    await _usView.SendMessageAsync(chatId, "❌ Ошибка выбора сортировки.");
                }
            }
        }

        private async Task SetSortPreferenceAsync(long userId, Models.SortType sortType)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var settings = await dbContext.UserSettings.FirstOrDefaultAsync(u => u.UserId == userId);
            if (settings == null)
            {
                settings = new Models.UserSettings { UserId = userId, SortType = sortType };
                dbContext.UserSettings.Add(settings);
            }
            else
            {
                settings.SortType = sortType;
            }

            await dbContext.SaveChangesAsync();
        }

        private string GetSortName(Models.SortType sortType) =>
            sortType switch
            {
                Models.SortType.ByDate => "📅 По дате",
                Models.SortType.ByStatus => "✅ По статусу",
                Models.SortType.ByTitle => "🔤 По названию",
                _ => "Неизвестный"
            };
    }
}
