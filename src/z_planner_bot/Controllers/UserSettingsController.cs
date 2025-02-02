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
            await _usView.ShowSortSettingsMenuAsync(chatId);
            await _usView.GetTimeZoneAsync(chatId);
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

            if (data.StartsWith("set_timezone_"))
            {
                string timeZoneString = data.Replace("set_timezone_", "");
                int offset;
                if (int.TryParse(timeZoneString, out offset))
                {
                    await SetTimeZonePreference(userId, offset);
                    await _usView.SendMessageAsync(chatId, $"✅ Часовой пояс установлен на: (UTC{(offset > 0 ? '+' + offset.ToString() : offset)}:00)");
                }
                else
                {
                    await _usView.SendMessageAsync(chatId, "❌ Ошибка выбора часового пояса.");
                }
            }
        }

        private async Task SetTimeZonePreference(long userId, int timeZone)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var settings = await dbContext.UserSettings.FirstOrDefaultAsync(u => u.UserId == userId);
            if (settings == null)
            {
                settings = new Models.UserSettings { UserId = userId, TimeZone = timeZone.ToString() };
                dbContext.UserSettings.Add(settings);
            }
            else
            {
                settings.TimeZone = timeZone.ToString();
            }

            await dbContext.SaveChangesAsync();
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
