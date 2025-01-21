# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Устанавливаем рабочую директорию
WORKDIR /src

# Копируем проект в контейнер
COPY src/z_planner_bot/z_planner_bot.csproj ./z_planner_bot/

# Восстанавливаем зависимости
RUN dotnet restore z_planner_bot/z_planner_bot.csproj

# Копируем весь код проекта в контейнер
COPY src/z_planner_bot/ .

# Сборка и публикация проекта
RUN dotnet publish z_planner_bot/z_planner_bot.csproj -c Release -o /out

# Используем более легкий образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Устанавливаем рабочую директорию для запуска
WORKDIR /app

# Копируем собранные файлы
COPY --from=build /out .

# Открываем порт для работы
EXPOSE 80

# Запуск приложения
ENTRYPOINT ["dotnet", "z_planner_bot.dll"]
