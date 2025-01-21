using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

class Program
{
    private static readonly ReceiverOptions receiverOptions = new ReceiverOptions
    {
        AllowedUpdates = { }
    };

    static async Task Main(string[] args)
    {
        string? botToken = Environment.GetEnvironmentVariable("TG_TOKEN");

        if (string.IsNullOrEmpty(botToken))
        {
            Console.WriteLine("Ошибка: Токен не задан");
            return;
        }

        var botClient = new TelegramBotClient(botToken);
        var me = await botClient.GetMe();
        Console.WriteLine($"Bot {me.FirstName} is running...");

        botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, CancellationToken.None);
        await Task.Delay(Timeout.Infinite);
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Update type: {update.Type}");

        if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message && update.Message?.Text != null)
        {
            var message = update.Message;
            Console.WriteLine($"Received a message from {message.Chat.Username}: {message.Text}");

            await botClient.SendMessage(message.Chat.Id, $"Ты написал: {message.Text}", cancellationToken: cancellationToken);
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error occured: {exception.Message}");
        return Task.CompletedTask;
    }

}