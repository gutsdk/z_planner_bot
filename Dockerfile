# Этап сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Устанавливаем рабочую директорию
WORKDIR /src

# Копируем проект и восстанавливаем зависимости
COPY . ./
RUN dotnet restore

# Собираем проект
RUN dotnet publish -c Release -o /out

# Этап выполнения
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

# Устанавливаем рабочую директорию для выполнения
WORKDIR /app

# Копируем собранные файлы из этапа сборки
COPY --from=build /out .

# Открываем порт 80 для приложения
EXPOSE 80

# Запуск приложения
ENTRYPOINT ["dotnet", "z_planner_bot.dll"]  
