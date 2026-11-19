# Готовые сборки Alfa 156

Версия берётся из корневого файла [`VERSION`](../../VERSION) (сейчас `1.8.0`).
Имена файлов в этой папке всегда включают номер версии:

| Файл | Платформа |
|------|-----------|
| `Alfa156-<version>.apk` | Android (ARM64) |
| `Alfa156-<version>-Desktop.exe` | Windows ПК |
| `Alfa156-<version>-KPK.exe` | Windows Mobile / КПК |
| `Alfa156-<version>-KPK.zip` | КПК + .NET CF 3.5 CAB |

Пример для `1.8.0`: `Alfa156-1.8.0.apk`, `Alfa156-1.8.0-Desktop.exe`, `Alfa156-1.8.0-KPK.exe`, `Alfa156-1.8.0-KPK.zip`.

Сборки обновляются автоматически GitHub Actions и публикуются в rolling Releases:

- [android-latest](https://github.com/Dolfst/alfa/releases/tag/android-latest) — APK
- [desktop-latest](https://github.com/Dolfst/alfa/releases/tag/desktop-latest) — Desktop EXE
- [kpk-latest](https://github.com/Dolfst/alfa/releases/tag/kpk-latest) — КПК EXE / ZIP
