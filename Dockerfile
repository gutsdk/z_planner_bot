# Этап сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Устанавливаем рабочую директорию для сборки
WORKDIR /src/z_planner_bot

# Копируем решение и восстанавливаем зависимости
COPY src/z_planner_bot/z_planner_bot.csproj ./
RUN dotnet restore

# Собираем и публикуем решение
COPY src/z_planner_bot/. ./
RUN dotnet publish -c Release --property:OutputPath=/out

# Этап выполнения
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

# Устанавливаем рабочую директорию для выполнения
WORKDIR /app

# Копируем собранные файлы из этапа сборки
COPY --from=build /out .

# Открываем порт 443 для приложения
EXPOSE 443

# Запуск приложения
ENTRYPOINT ["dotnet", "z_planner_bot.dll"]
