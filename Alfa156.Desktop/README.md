# Alfa 156 Desktop

ПК-приложение с тем же логотипом и стилем, что КПК и Android.

## Запуск

`dist/Alfa156-Desktop/Alfa156.Desktop.exe` (нужен .NET Framework 4.8)

## Возможности

- Папка `.dat` / ZIP Android↔КПК
- Excel `.xlsx` импорт/экспорт
- CSV для Google Sheets
- Импорт Google по ссылке (формат «Деньги»)
- **Синхронизация Google** через Apps Script (`../google-sync/`)

## Сборка

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" Alfa156.Desktop.csproj /p:Configuration=Release
```
