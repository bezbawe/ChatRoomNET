# ChatRoomNET

Real-time чат на **ASP.NET Core + SignalR + Blazor WebAssembly**.

Фичи (MVP): регистрация/логин (Identity + JWT), публичные и приватные комнаты (по инвайт-коду),
сообщения в реальном времени через SignalR, история с keyset-пагинацией (infinite scroll),
индикатор «печатает…» и статус онлайн/оффлайн.

## Стек

- **.NET 10**, C# (`Nullable` + `ImplicitUsings`).
- **Backend** (`ChatRoomNET.Web`): Minimal APIs + SignalR, EF Core 10 + Npgsql, ASP.NET Core Identity, JWT-bearer.
- **Frontend** (`ChatRoomNET.Web.UI.Blazor`): Blazor WASM, SignalR Client, Blazored.LocalStorage.
- **Тесты** (`ChatRoomNET.Web.Tests`): xUnit + `WebApplicationFactory` + EF Core InMemory.
- **БД:** PostgreSQL (через `docker-compose.yml`).

Solution: `src/ChatRoomNET.sln`.

## Требования

- .NET 10 SDK
- Docker (для PostgreSQL) либо локальный PostgreSQL 17
- `dotnet-ef` (для миграций): `dotnet tool install --global dotnet-ef`

## Запуск с нуля

### 1. Поднять базу

```bash
docker compose up -d          # PostgreSQL на 127.0.0.1:5432, БД chatroomnet (postgres/postgres)
```

### 2. Настроить секреты backend

Строка подключения и `Jwt:Key` не лежат в коммитах — задай их в
`src/ChatRoomNET.Web/appsettings.Development.json` или через user-secrets:

```jsonc
{
  "ConnectionStrings": {
    "Default": "Host=127.0.0.1;Port=5432;Database=chatroomnet;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "dev-only-signing-key-change-me-please-at-least-32-bytes-long!!"
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:5092", "https://localhost:7220" ]
  }
}
```

> Локальный PostgreSQL слушает только IPv4 — используй `Host=127.0.0.1`, а не `localhost`.
> `Jwt:Key` должен быть не короче 32 байт.

### 3. Применить миграции

```bash
dotnet ef database update --project src/ChatRoomNET.Web
```

### 4. Запустить приложение

В двух терминалах:

```bash
dotnet run --project src/ChatRoomNET.Web            # API + SignalR → http://localhost:5260
dotnet run --project src/ChatRoomNET.Web.UI.Blazor  # UI          → http://localhost:5092
```

Адрес API для фронтенда задаётся в `src/ChatRoomNET.Web.UI.Blazor/wwwroot/appsettings.json`
(`ApiBaseAddress`) и должен входить в список `Cors:AllowedOrigins` backend.

Открой `http://localhost:5092`, зарегистрируйся, создай комнату — и открой её во втором браузере,
чтобы увидеть сообщения в реальном времени.

## Тесты

```bash
dotnet test src/ChatRoomNET.Web.Tests
```

Интеграционные тесты поднимают приложение через `WebApplicationFactory` и подменяют `ChatDbContext`
на EF Core InMemory — базы данных и Docker для них не нужно.

## Миграции EF Core

```bash
dotnet ef migrations add <Name> --project src/ChatRoomNET.Web
dotnet ef database update       --project src/ChatRoomNET.Web
```

## Документация

- `docs/Описание проекта.md` — исходное описание задачи.
- `docs/План разработки.md` — план по фазам с чекпойнтами `verify`.
- `CLAUDE.md` — архитектурные конвенции и команды.
