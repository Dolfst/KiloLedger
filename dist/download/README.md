# Готовые сборки Alfa 156

Версия берётся из корневого файла [`VERSION`](../../VERSION) (сейчас `1.20.0`).
Имена файлов в этой папке всегда включают номер версии:

| Файл | Платформа |
|------|-----------|
| `Alfa156-<version>.apk` | Android (ARM64) |
| `Alfa156-<version>-Desktop.exe` | Windows ПК |
| `Alfa156-<version>-KPK.exe` | Windows Mobile / КПК |
| `Alfa156-<version>-KPK.zip` | КПК + .NET CF 3.5 CAB |

Пример для `1.20.0`: `Alfa156-1.20.0.apk`, `Alfa156-1.20.0-Desktop.exe`, `Alfa156-1.20.0-KPK.exe`, `Alfa156-1.20.0-KPK.zip`.

**1.20.0:** паспорт авто (`cars-meta.dat`); лента расходов (топливо/ремонт/прочие).

**1.19.0:** цель заправки (`purpose`); итоги за год в сводке; `onboarding_seen`.

**1.18.0:** экспорт/шаринг сводки; поиск дубликатов; подстановка последней цены/л.

**1.17.0:** проверка пробега; глобальный поиск; Google Sheets «Прочие {авто}» + «Кат. прочих».

**1.16.0:** предпросмотр слияния перед sync; автокопия `Safety_yyyyMMdd_HHmmss`.

**1.15.0:** валюта в `settings.dat`; фото чеков по Wi‑Fi (`ALFA156SYNC2R`, ≤2 МБ/файл, ≤50/сеанс).

**1.14.0:** график расходов 12 мес.; фото чеков `Receipts/` в ZIP.

**1.13.0:** фильтры на Desktop; флаг «полный бак» у заправок (л/100 км только full→full).

**1.12.0:** Wi‑Fi sync v2 (обфускация сессии от кода), мастер Google Sync на Desktop.

**1.11.0:** прочие расходы (`expenses.dat`), категории прочих, настраиваемые интервалы ТО (`settings.dat`).

**1.10.0:** стабильные id + `deleted.dat` — удаления синхронизируются; журнал после sync.

**1.9.0:** сводка на Desktop, цена/л и комментарий у заправок, уведомления ТО на Android.

Сборки обновляются автоматически GitHub Actions и публикуются в rolling Releases:

- [android-latest](https://github.com/Dolfst/alfa/releases/tag/android-latest) — APK
- [desktop-latest](https://github.com/Dolfst/alfa/releases/tag/desktop-latest) — Desktop EXE
- [kpk-latest](https://github.com/Dolfst/alfa/releases/tag/kpk-latest) — КПК EXE / ZIP
