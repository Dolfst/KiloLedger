# Alfa156 Desktop

ПК-приложение для просмотра и правки данных Alfa156, с импортом/экспортом Excel и Google Sheets.

## Требования

- Windows с .NET Framework 4.8

## Запуск

`Alfa156.Desktop\bin\Release\Alfa156.Desktop.exe`

## Возможности

- Открыть папку с `cars.dat`, `fuel.dat`, `repairs.dat`, `utilities.dat`, `expenses.dat`, `expense-categories.dat`, `settings.dat`, `repair-categories.dat`, `deleted.dat`
- Открыть/сохранить ZIP (как на Android)
- Редактирование таблиц прямо в окне
- **Экспорт / импорт Excel** (`.xlsx`, листы: Автомобили, Заправки, Ремонт, Коммуналка, Категории)
- **Экспорт CSV** для загрузки в Google Таблицы (`Файл → Импорт`)
- **Импорт CSV-папки** (`cars.csv`, `fuel.csv`, …)
- **Синхронизация Google** через Apps Script; мастер настройки — кнопка **Google: мастер**
- **Импорт Google Sheets** по ссылке (таблица должна быть «доступна по ссылке»; листы с именами как в Excel)

## Сборка

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" Alfa156.Desktop.csproj /p:Configuration=Release
```
