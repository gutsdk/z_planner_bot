using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using z_planner_bot.Views;

namespace z_planner_bot.Controllers
{
    internal class BotController : ControllerBase
    {
        private readonly TaskController _taskController;
        private readonly UserSettingsController _settingsController;
        private readonly BotView _botView;

        public BotController(TaskController taskController, UserSettingsController settingsController, BotView botView)
        {
            _taskController = taskController;
            _settingsController = settingsController;
            _botView = botView;
        }

        public async Task ShowMenuAsync(long chatId)
        {
            await _botView.ShowMainMenuAsync(chatId);
        }

        public async Task ShowHelpAsync(long chatId)
        {
            await _botView.ShowHelpAsync(chatId);
        }

        public async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            var data = callbackQuery.Data;

            if (data.StartsWith("set_sort_"))
            {
                await _settingsController.HandleSettingsCallbackAsync(callbackQuery);
            }
            else
            {
                await _taskController.HandleTaskCallbackAsync(callbackQuery);
            }
        }
    }
}
