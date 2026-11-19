# Готовые сборки Alfa 156

Версия берётся из корневого файла [`VERSION`](../../VERSION) (сейчас `1.33.0`).
Имена файлов в этой папке всегда включают номер версии:

| Файл | Платформа |
|------|-----------|
| `Alfa156-<version>.apk` | Android (ARM64) |
| `Alfa156-<version>-Desktop.exe` | Windows ПК |
| `Alfa156-<version>-KPK.exe` | Windows Mobile / КПК |
| `Alfa156-<version>-KPK.zip` | КПК + .NET CF 3.5 CAB |

Пример для `1.33.0`: `Alfa156-1.33.0.apk`, `Alfa156-1.33.0-Desktop.exe`, `Alfa156-1.33.0-KPK.exe`, `Alfa156-1.33.0-KPK.zip`.

**1.33.0:** отчёт за период (топливо/ремонт/прочие/коммунальные, /км); Desktop — txt/html/печать; Android — share/HTML; КПК — txt в My Documents.

**1.32.0:** АЗС / бренд (`station`) у заправок после `purpose`; автодополнение; ср. цена/л и число заправок в сводке; фильтр по АЗС.

**1.31.0:** заметки по авто (`notes.dat`: `date|car|text|id`); CRUD на Android/Desktop/КПК; сводка/лента; ZIP/Wi‑Fi/tombstone `note`.

**1.30.0:** оценка пробега в сводке (средний км/день за 30–90 дн.; км/дни до ТО масла и ремня ГРМ).

**1.29.0:** быстрая заправка (Android / Desktop / КПК): default_car, пробег, литры, стоимость, авто цена/л, полный бак.

**1.28.0:** smoke verifies в CI; CF API guard; полный ZIP (templates/reminders/cars-meta); стабилизация после 1.25–1.27.

**1.27.0:** мастер «Импорт» на Desktop (ZIP/папка/CSV/Excel + предпросмотр + Safety_); расширенный предпросмотр «Импорт копии» на Android; шаблоны CSV.

**1.26.0:** напоминания по дате (`reminders.dat`); `reminder_warn_days` (по умолч. 14).

**1.25.0:** шаблоны записей (`templates.dat`); избранный авто (`default_car`).

**1.24.0:** сравнение периодов в сводке (Δ топливо/ремонт/прочие/коммунальные и /км); Android/Desktop — пресет и свои даты; КПК — этот vs прошлый месяц.

**1.23.0:** напоминание о копии (`backup_remind_days` / `last_backup_utc`); полный экспорт ZIP; подтверждение SD-копии на КПК со счётчиком файлов.

**1.22.0:** мягкий месячный бюджет (`budget_month` / `budget_warn_pct`); сводка spent vs limit; Android-уведомление при превышении.

**1.21.0:** сравнение авто в сводке (топливо/ремонт/прочие, /км, л/100, пробег); «Проверка данных» + простое исправление категорий.

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
