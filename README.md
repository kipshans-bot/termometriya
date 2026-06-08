# Termometriya — мониторинг температуры зерна в элеваторе

Распределённая система мониторинга температуры зерна в силосах элеватора.  
Опрос термоподвесок по Modbus RTU (RS-485) / TCP через блоки БКТ-12, визуализация в реальном времени через SignalR, автоматическая детекция аварийных ситуаций, формирование отчётов.

## Возможности

- **Мнемосхема** — весь элеватор на одном экране: линии, силосы, цветовая индикация температуры
- **Детальный просмотр силоса** — колонки температур по каждой термоподвеске, послойная визуализация, вид сверху
- **Графики трендов** — история изменения температуры по точкам за 1ч/6ч/24ч/7д
- **Алармы** — журнал событий с фильтрацией, квитированием и разрешением
- **8 типов алармов**: Warning, Critical, GradientCritical, GradientWarning, Deviation, Humidity, SensorFault, CriticalHighTemp50
- **Автоматический перезапуск** — при сохранении конфига сервер перезапускается с новыми настройками
- **Визуальный редактор конфига** — админка :5001 с таблицами и формами для всех настроек
- **5 форматов отчётов**: Excel-сводка, PDF-сводка, PDF по силосу, CSV-лог температур, CSV-лог алармов
- **Автоопределение уровня зерна** — анализ градиента температур по высоте
- **Адаптивный опрос** — автоматическое переключение между нормальным и учащённым режимом опроса
- **Конфигуратор** — WPF-десктоп приложение для настройки всей системы
- **Кросс-платформенность** — Windows и Linux, self-contained сборка без установки рантайма
- **Прозрачная работа через NAT** — CORS any origin, WebSocket fallback

## Архитектура

```
┌──────────────────────────────────────────────────────────────┐
│                    Браузер (пользователь)                      │
│  React SPA (:5000)            Admin Panel (:5001)             │
│  Мнемосхема, тренды,           Визуальный редактор             │
│  алармы, отчёты,               конфига, управление             │
│  пороги культур                сервером                        │
│         ↕ SignalR + HTTP ↕                          ↕ HTTP     │
├──────────────────────────────────────────────────────────────┤
│                   ASP.NET Core 10 Сервер                       │
│  ┌──────────┬───────────┬───────────┬──────────────────────┐  │
│  │ REST API │  SignalR  │  Services │  EF Core + SQLite    │  │
│  │ 9 конт-  │  Hub      │  Alert    │  AppDbContext        │  │
│  │ роллеров │ Monitoring│  Report   │  10 таблиц           │  │
│  │          │           │  Polling  │                      │  │
│  │          │           │  Config   │                      │  │
│  │          │           │  GrainDet │                      │  │
│  └──────────┴───────────┴───────────┴──────────────────────┘  │
│                         ↕ Modbus TCP/RTU                       │
├──────────────────────────────────────────────────────────────┤
│                    БКТ-12 / Оборудование                       │
│  Блоки БКТ-12 по RS-485 (Modbus RTU) или Ethernet (Modbus TCP)│
│  Термоподвески: до 6 шт × до 30 точек                         │
│  Симулятор Modbus TCP (tools/) для тестирования                │
├──────────────────────────────────────────────────────────────┤
│              WPF Конфигуратор (Desktop, Windows)               │
│  Визуальный редактор силосов/подвесок, управление сервером     │
└──────────────────────────────────────────────────────────────┘
```

### Технологии

| Компонент | Стек |
|-----------|------|
| **Сервер** | ASP.NET Core 10, EF Core 10 + SQLite, SignalR |
| **Фронтенд** | React 19 + TypeScript, Vite 6, Recharts, SignalR JS |
| **Конфигуратор** | WPF (.NET 10 Windows), MVVM |
| **Modbus** | RTU (System.IO.Ports) / TCP, CRC-16, Function 03 |
| **Отчёты** | PDF (QuestPDF), Excel (ClosedXML), CSV |
| **Шрифты** | Lato (OFL) |

## Оборудование

- **Линии** — до 3+ линий (RS-485 или Ethernet)
- **Блоки БКТ-12** — до 6 блоков (по 2 на линию), Modbus Slave
- **Силосы** — до 4 на блок (гибкая конфигурация, любое количество)
- **Термоподвески** — до 6 на силос (1 центральная + 5 периферийных)
- **Точки** — до 30 на подвеску (термопары)
- **Адреса регистров** — `15 + cableInput × 30`, `Function 03`, big-endian signed int16 × 0.0625

## Порты

| Порт | Назначение |
|------|-----------|
| **5000** | Основной веб-интерфейс (React SPA) |
| **5001** | Административная панель (vanilla HTML/JS) |
| 5173 | Сервер разработки фронтенда (Vite, dev-режим) |

## Быстрый старт

**Требуется**: .NET 10 SDK (сборка) или .NET 10 Runtime (запуск готового деплоя).

### Из исходников (одна команда)

```bash
git clone <repo>
cd termometriya/server
dotnet run
```

Фронтенд встроен в `server/wwwroot` — дополнительная сборка не нужна.  
Сервер: `http://localhost:5000` | Админка: `http://localhost:5001`

### Из готового деплоя

```bash
# Windows
deploy\Termometriya.Server.exe

# Linux
./deploy-linux/Termometriya.Server
```

Скрипты: `deploy/start.bat` / `deploy/start.ps1` (Windows, открывает браузер).

### Разработка фронтенда

```bash
cd client
npm install
npm run dev          # Vite dev-сервер :5173 с прокси на :5000
npm run build        # Сборка в server/wwwroot
```

## Развёртывание

### Windows (self-contained)

```bash
cd server
dotnet publish -c Release --self-contained -r win-x64 -o ../deploy
```

Один `exe` с рантаймом, шрифтами, фронтендом, админкой.  
Запуск: `deploy\Termometriya.Server.exe`.

### Linux (self-contained)

```bash
cd server
dotnet publish -c Release --self-contained -r linux-x64 -o ../deploy-linux --nologo
```

### Framework-dependent (нужен установленный runtime)

```bash
dotnet publish -c Release --no-self-contained -o ../deploy
```

## Конфигурация

Файл `termometriya-config.jsonc` — единая конфигурация всей системы.  
Редактируется через визуальный редактор (`:5001`, вкладка «Линии») или напрямую.

```jsonc
{
  "cultures": [
    {
      "name": "Пшеница",
      "normTemp": 25,          // Норма
      "warnTemp": 30,          // Предупреждение
      "criticalTemp": 35,      // Критично
      "gradientWarn": 1.0,     // Градиент °C/сутки — предупреждение
      "gradientCritical": 2.0, // Градиент °C/сутки — критично
      "deviationThreshold": 3.0, // Отклонение от среднего по силосу
      "highTempThreshold": 30,   // Порог высокой температуры
      "highTempGradient": 5.0,   // Градиент для высокой температуры
      "criticalHighTemp50": 50   // Абсолютный порог 50°C
    }
  ],
  "lines": [
    {
      "name": "Линия 1",
      "displayOrder": 1,
      "enabled": true,
      "protocol": "RTU",
      "comPort": "COM2",
      "baudRate": 9600,
      "dataBits": 8,
      "parity": "Even",
      "stopBits": "One",
      "blocks": [
        {
          "slaveId": 1,
          "silos": [
            {
              "number": 101,
              "culture": "Пшеница",
              "fillLevel": 80,
              "capacity": 1000,
              "sensors": [
                { "cableInput": 0, "points": 30 },
                { "cableInput": 1, "points": 30, "isCentral": true },
                { "cableInput": 2, "points": 30 },
                { "cableInput": 3, "points": 30 },
                { "cableInput": 4, "points": 30 },
                { "cableInput": 5, "points": 30 }
              ]
            }
          ]
        }
      ]
    },
    {
      "name": "Линия 3",
      "protocol": "TCP",
      "ipAddress": "192.168.20.7",
      "ipPort": 502,
      "blocks": [
        { "slaveId": 1, "silos": [...] },
        { "slaveId": 2, "silos": [...] }
      ]
    }
  ]
}
```

## REST API

| Метод | Путь | Назначение |
|-------|------|-----------|
| `GET` | `/api/admin/status` | Статус сервера (силосы, подвески, алармы, аптайм) |
| `GET` | `/api/admin/config` | Содержимое конфиг-файла |
| `POST` | `/api/admin/config` | Сохранить конфиг + перезапустить сервер |
| `POST` | `/api/admin/clear-db` | Очистить БД и пересоздать из конфига |
| `POST` | `/api/admin/restart-server` | Перезапустить процесс сервера |
| `POST` | `/api/admin/restart-polling` | Перезапустить опрос Modbus |
| `GET` | `/api/alert` | Алармы (query: activeOnly, siloId, take) |
| `POST` | `/api/alert/{id}/ack` | Квитировать аларм |
| `POST` | `/api/alert/ack-all` | Квитировать все активные |
| `POST` | `/api/alert/{id}/resolve` | Разрешить аларм |
| `POST` | `/api/alert/resolve-all` | Разрешить все активные |
| `GET` | `/api/alert/config` | Настройки оповещений |
| `PUT` | `/api/alert/config` | Обновить настройки оповещений |
| `GET` | `/api/culture` | Список культур |
| `PUT` | `/api/culture/{id}` | Обновить пороги культуры |
| `GET` | `/api/silo` | Сводка по всем силосам (max/avg temp, alert status) |
| `GET` | `/api/silo/{id}` | Детальные показатели силоса |
| `PUT` | `/api/silo/{id}` | Изменить настройки (культура, заполнение) |
| `GET` | `/api/silo/{id}/readings` | История показаний (query: hours, bucket) |
| `GET` | `/api/silo/{id}/report/pdf` | PDF-отчёт по силосу |
| `GET` | `/api/report/summary` | Excel-сводка по всем силосам (query: from, to) |
| `GET` | `/api/report/summary/pdf` | PDF-сводка |
| `GET` | `/api/report/log/csv` | CSV-лог температур |
| `GET` | `/api/report/alerts/csv` | CSV-лог алармов |
| `GET` | `/api/line` | Список линий с силосами |
| `PUT` | `/api/line/{id}` | Переименовать линию |
| `GET` | `/api/config` | Получить конфиг (DTO) |
| `POST` | `/api/config` | Сохранить конфиг (DTO) |
| `GET` | `/api/pollingconfig` | Интервалы опроса |
| `PUT` | `/api/pollingconfig` | Обновить интервалы опроса |
| `GET` | `/api/emulator/scenarios` | Сценарии симуляции |
| `POST` | `/api/emulator/scenario` | Установить сценарий для силоса |
| `POST` | `/api/emulator/reset` | Сбросить симуляцию |

## Real-time мониторинг (SignalR)

Хаб: `/hubs/monitoring`  
События, которые получают все подключённые клиенты:

| Событие | Данные | Периодичность |
|---------|--------|--------------|
| `SiloUpdated` | полные данные по всем силосам (температуры, подвески, алармы) | каждый цикл опроса |
| `AlertTriggered` | новый аларм (siloId, тип, значение, порог) | немедленно |
| `AlertCounts` | счётчики (всего, active, critical, warning) | каждый цикл опроса |

Группы: `JoinGroup(siloId)` / `LeaveGroup(siloId)` — подписка на обновления конкретного силоса.

## Алармы

Система оценивает каждое показание относительно порогов культуры:

| Тип | Уровень | Условие |
|-----|---------|---------|
| **SensorFault** | ERROR | Температура = `0xAAAA` (-1443.2°C) — обрыв/КЗ датчика |
| **CriticalHighTemp** | CRITICAL | Температура ≥ 50°C (или `criticalHighTemp50`) |
| **Critical** | CRITICAL | Температура ≥ `criticalTemp` |
| **Warning** | WARNING | Температура ≥ `warnTemp` |
| **GradientCritical** | CRITICAL | Рост за сутки ≥ `gradientCritical` °C |
| **GradientWarning** | WARNING | Рост за сутки ≥ `gradientWarn` °C |
| **DeviationWarning** | WARNING | Отклонение точки от среднего ≥ `deviationThreshold` °C |
| **HumidityWarning** | WARNING | Влажность ≥ порога |

Автоматическое разрешение: когда температура падает ниже порога с гистерезисом.

## Отчёты

| Тип | Формат | Что содержит |
|-----|--------|-------------|
| **Сводка** | Excel (.xlsx) | Все силосы: средняя, макс, мин температура за период |
| **Сводка** | PDF | Таблица со средними/макс/мин по силосам, альбомная ориентация |
| **По силосу** | PDF | Полные показатели силоса: все подвески, все точки |
| **Лог температур** | CSV | Все показания за период (siloId, pendantId, pointIndex, temp, timestamp) |
| **Лог алармов** | CSV | Все события алармов за период |

## Grain Level Detector

Автоматическое определение уровня зерна в силосе на основе профиля температур:

1. Анализирует variance температур по точкам каждой подвески за 48-часовое окно
2. На границе «зерно — воздух» variance резко возрастает из-за разницы температур
3. Обновляет `Silo.GrainLevelPointIndex` (уровень заполнения по точкам)
4. Запускается каждый час фоновым сервисом

## Административная панель

`http://localhost:5001` — самодостаточный HTML/JS/CSS инструмент (без зависимостей):

| Вкладка | Что даёт |
|---------|----------|
| **Культуры** | Таблица с полями для всех порогов, добавление/удаление |
| **Линии** | Редактор: линия → блок Modbus (Slave ID) → силос → датчики |
| **Сырой JSON** | Прямое редактирование `termometriya-config.jsonc` |

После сохранения сервер автоматически перезапускается (запускает новый процесс и завершает текущий). Админка дожидается перезапуска и перезагружает страницу.

## WPF Конфигуратор

Windows-приложение для визуальной настройки всей системы:

- **Elevator Config** — дерево культур, линий, силосов, подвесок с редакторами свойств
- **BKT-12 Hardware** — настройка блоков: Slave ID, порты, connection params, маппинг подвесок
- **Server Management** — запуск/остановка сервера, мониторинг вывода консоли

## Modbus TCP Simulator (tools/)

`tools/ModbusTcpServer/` — автономный симулятор блоков БКТ-12:

- Modbus TCP slave на портах 1501–1506
- Function 03 (Read Holding Registers), адреса 15–374
- Реалистичные температурные профили (суточные колебания)
- Поддержка множественных TCP-клиентов
- Используется для тестирования без оборудования

## Адаптивный опрос

`DataPollingService` автоматически переключает режим опроса:

| Режим | Интервал | Условие |
|-------|----------|---------|
| **Normal** | 3600 с (1 час) | Все температуры в норме |
| **Elevated** | 900 с (15 мин) | Есть активные алармы |

## Структура проекта

```
Termometriya/                  # Корень репозитория
├── server/                    # ASP.NET Core backend
│   ├── Controllers/           # REST API (9 контроллеров)
│   │   ├── AdminController.cs     # Админка, статус, конфиг, перезапуск
│   │   ├── AlertController.cs     # Алармы CRUD
│   │   ├── ConfigController.cs    # Конфиг DTO
│   │   ├── CultureController.cs   # Культуры
│   │   ├── EmulatorController.cs  # Сценарии симуляции
│   │   ├── LineController.cs      # Линии
│   │   ├── PollingConfigController.cs
│   │   ├── ReportController.cs    # Отчёты
│   │   └── SiloController.cs      # Силосы, показания
│   ├── Data/                  # EF Core DbContext, SeedData
│   ├── Hubs/                  # MonitoringHub (SignalR)
│   ├── Models/                # Entity + Config models
│   ├── Services/              # Бизнес-логика
│   │   ├── DqtService/            # Modbus RTU/TCP, симулятор
│   │   ├── AlertService.cs        # Детекция алармов
│   │   ├── DataPollingService.cs  # Фоновый опрос
│   │   ├── ElevatorConfigService.cs
│   │   ├── GrainLevelDetector.cs  # Автоопределение уровня
│   │   ├── NotificationService.cs # SignalR push
│   │   ├── ReportService.cs       # PDF/Excel/CSV
│   │   └── TrayService.cs         # Иконка в трее
│   ├── wwwroot/               # Скомпилированный React
│   │   └── admin/                 # Админ-панель
│   ├── appsettings.json       # Порт 5000+5001, SQLite
│   └── termometriya-config.jsonc  # Конфиг системы
├── client/                    # React + Vite фронтенд
│   ├── src/
│   │   ├── pages/             # Mnemoscheme, SiloDetail, Trends, Alarms, Reports, Thresholds
│   │   ├── components/        # SiloCell, SiloBody, SiloTopView, SiloLayers, StatusBar
│   │   ├── services/          # REST API client, SignalR connection
│   │   ├── models/types.ts    # TypeScript интерфейсы
│   │   └── utils/gradient.ts  # Цветовая карта температур
│   └── vite.config.ts         # Прокси :5173 → :5000, сборка в server/wwwroot
├── configurator/              # WPF Desktop Configurator
│   ├── MainWindow.xaml        # 3 вкладки: Config, Hardware, Server
│   ├── ViewModels/            # MVVM
│   └── Services/              # HTTP API клиент, управление процессом
├── tools/
│   └── ModbusTcpServer/       # Автономный Modbus TCP симулятор
└── deploy/                    # Собранный дистрибутив
    ├── Termometriya.Server.exe
    ├── start.bat / start.ps1
    ├── appsettings.json
    └── wwwroot/               # Фронтенд + админка
```

## Сборка из исходников

```bash
# 1. Фронтенд (опционально, в репозитории уже собран)
cd client
npm install
npm run build

# 2. Сервер (Windows)
cd server
dotnet publish -c Release --self-contained -r win-x64 -o ../deploy

# 3. Сервер (Linux)
dotnet publish -c Release --self-contained -r linux-x64 -o ../deploy-linux

# 4. Configurator (Windows только)
cd configurator
dotnet publish -c Release --self-contained -r win-x64 -o ../deploy/configurator

# 5. Simulator
cd tools/ModbusTcpServer
dotnet publish -c Release --self-contained -r win-x64 -o ../../deploy/simulator
```

## Лицензия

MIT
