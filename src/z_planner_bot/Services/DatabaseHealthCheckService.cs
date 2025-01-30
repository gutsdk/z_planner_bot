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
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            _logger.LogInformation("Сервис проверки состояния базы запущен...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //  Проверяем подключение к базе данных
                    bool isConnected = await dbContext.CheckConnectionAsync();

                    if (isConnected)
                    {
                        _logger.LogInformation("Подключение активно");
                    }
                    else
                    {
                        _logger.LogWarning("Подключение к базе потеряно. Попытка подключиться заново...");
                        // возможно надо будет добавить сюда дополнительные действия, todo
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Произошла ошибка во время проверки подключения к базе");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            _logger.LogInformation("Сервис проверки состояния базы остановлен...");
        }
    }
}
