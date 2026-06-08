# Termometriya — система термометрии элеватора

Распределённая система мониторинга температуры зерна в силосах элеватора.

## Архитектура

- **Backend**: ASP.NET Core 10, EF Core + SQLite, SignalR
- **Frontend**: React 19 + TypeScript + Vite 6, Recharts
- **Конфигуратор**: WPF (.NET 10 Windows)
- **Modbus**: RTU (RS-485) / TCP через шлюз, блоки БКТ-12
- **Отчёты**: PDF (QuestPDF), Excel (ClosedXML), CSV

## Оборудование

- 3 линии × 4 силоса = 12 силосов
- До 6 термоподвесок на силос (1 центральная + 5 периферийных)
- До 30 точек на подвеску
- 6 блоков БКТ-12 (по 2 на линию)

## Быстрый старт

```bash
# 1. Установить .NET 10 SDK + Node.js 20
# 2. Сервер
cd server
dotnet run

# 3. Клиент (отдельный терминал)
cd client
npm install
npm run dev
```

Сервер: `http://localhost:5000`  
Клиент: `http://localhost:5173`

## Развёртывание

```bash
cd client && npm install && npm run build
cd ../server
dotnet publish -c Release --no-self-contained -o ../deploy
```

Запуск: `deploy\start.bat`

## Структура проекта

```
server/                        # ASP.NET Core backend
  Controllers/                 # REST API
  Data/                        # EF Core DbContext, SeedData
  Hubs/                        # SignalR MonitoringHub
  Models/                      # Entity models
  Services/                    # Business logic
    DqtService/                # Modbus polling, simulator
  wwwroot/                     # Client build output
client/                        # React + Vite frontend
  src/
    components/                # SiloCell, SiloBody, SiloTopView, ...
    pages/                     # Mnemoscheme, SiloDetail, Trends, Alarms, ...
    services/                  # API client, SignalR
    utils/                     # Gradient temperature mapping
configurator/                  # WPF Configurator
tools/ModbusTcpServer/         # Standalone Modbus TCP simulator
```

## Лицензия

MIT
