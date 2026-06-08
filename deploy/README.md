# Термометрия элеватора

Система мониторинга температуры в силосах элеватора через термоподвески БКТ-12 (Modbus TCP/RTU).

## Требования

- **.NET 10 Runtime** — [скачать](https://dotnet.microsoft.com/download/dotnet/10.0)
- **SQLite** (встроен, дополнительной установки не требуется)
- **Права на COM-порт** (для RTU-линий)

## Быстрый старт

### Windows

```
cd deploy\win-x64
start.bat
```

Или:

```
cd deploy\win-x64
dotnet Termometriya.Server.dll
```

Сервер запустится на `http://localhost:5000`. Откройте в браузере.

### Linux

```bash
cd deploy/linux-x64
chmod +x start.sh
./start.sh
```

Или:

```bash
cd deploy/linux-x64
dotnet Termometriya.Server.dll
```

> **Важно:** Для использования COM-порта на Linux нужны права:
> ```bash
> sudo usermod -a -G dialout $USER
> # После этого выйти и заново зайти
> ```

## Конфигурация

Файл `termometriya-config.jsonc` — описание линий, блоков, силосов и термоподвесок.

Параметры сервера — `appsettings.json`:

```json
{
  "Urls": "http://0.0.0.0:5000",
  "ConnectionStrings": {
    "Default": "Data Source=termometriya.db"
  }
}
```

При первом запуске база данных (`termometriya.db`) создаётся автоматически на основе конфигурации.

## Остановка

Нажмите `Ctrl+C` в окне терминала.

## Структура deploy

```
deploy/
├── win-x64/          # Windows-сборка
│   ├── Termometriya.Server.exe
│   ├── start.bat
│   ├── start.ps1
│   ├── wwwroot/      # Статические файлы веб-интерфейса
│   └── termometriya-config.jsonc
├── linux-x64/        # Linux-сборка
│   ├── Termometriya.Server
│   ├── start.sh
│   ├── wwwroot/
│   └── termometriya-config.jsonc
└── README.md
```

## Протоколы подключения

- **Modbus TCP** — линия настраивается через `protocol: "tcp"`, `ipAddress`, `port`
- **Modbus RTU** — линия настраивается через `protocol: "rtu"`, `comPort`, `baud`, `parity`, `stopBits`, `dataBits`
  - Windows: `"comPort": "COM2"`
  - Linux: `"comPort": "/dev/ttyS1"` или `"/dev/ttyUSB0"`

## Пороги срабатывания алармов

Настраиваются через веб-интерфейс: вкладка **Пороги** → выбрать культуру.
