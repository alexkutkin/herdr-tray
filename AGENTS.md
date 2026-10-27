# Правила репозитория

- **Собирай Release вместе с Debug.** После правок в коде запускай `dotnet build -c Release`
  в дополнение к `dotnet build -c Debug`: ярлык `Herdr Tray Utility` на рабочем столе запускает
  `bin\Release\net48\Herdr Tray Utility.exe`, и изменение, собранное только в Debug, там не видно.
