# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Устанавливаем рабочую директорию
WORKDIR /app

# Копируем все файлы проекта в контейнер
COPY . ./

# Восстанавливаем зависимости и собираем проект
RUN dotnet restore
RUN dotnet publish -c Release -o /out

# Используем более легкий образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Устанавливаем рабочую директорию для запуска
WORKDIR /app

# Копируем собранные файлы из предыдущего шага
COPY --from=build /out .

# Открываем порт, на котором будет работать бот (если нужно)
EXPOSE 80

# Команда для запуска бота
ENTRYPOINT ["dotnet", "z_planner_bot.dll"]
