using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace z_planner_bot.Services
{
    internal class DatabaseHealthCheckService : BackgroundService
    {
        private readonly IDbContextFactory<Models.AppDbContext> _dbContextFactory;
        private readonly ILogger<DatabaseHealthCheckService> _logger;
        public DatabaseHealthCheckService(IDbContextFactory<Models.AppDbContext> dbContextFactory, ILogger<DatabaseHealthCheckService> logger) 
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Сервис проверки состояния базы запущен...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    //  Проверяем подключение к базе данных
                    bool isConnected = await dbContext.Database.CanConnectAsync();

                    if (isConnected)
                    {
                        _logger.LogInformation("✅ Подключение к базе активно");
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Потеряно подключение к базе");
                        // возможно надо будет добавить сюда дополнительные действия
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Ошибка подключения к базе");
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }

            _logger.LogInformation("Сервис проверки состояния базы остановлен...");
        }
    }
}
