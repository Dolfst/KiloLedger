# Формат данных и snapshot Alfa 156 (schemaVersion 1)

Документ описывает публичный формат резервной копии, общий для Android, Desktop и КПК.
Любое несовместимое изменение повышает `schemaVersion`; клиенты отклоняют неизвестную схему до записи данных.

## Состав копии

Полная копия — ZIP (Android/Desktop) или папка (КПК) с файлами данных, папкой `Receipts/` (необязательно) и `snapshot.json`.
Копии без `snapshot.json` (до 1.36.0) остаются допустимыми.

## snapshot.json

```json
{"schema":"alfa156.snapshot","schemaVersion":1,"appVersion":"1.36.0","createdUtc":"2026-11-21T10:00:00Z","deviceToken":"..."}
```

| Поле | Тип | Описание |
|------|-----|----------|
| `schema` | строка | Всегда `alfa156.snapshot` |
| `schemaVersion` | число | Версия схемы; сейчас `1` |
| `appVersion` | строка | Версия приложения, создавшего копию (`VERSION`) |
| `createdUtc` | строка | Время UTC, `yyyy-MM-ddTHH:mm:ssZ` |
| `deviceToken` | строка | Постоянный токен устройства из `settings.dat` |

Обязательны все поля. Неподдерживаемая `schema`/`schemaVersion` или отсутствующее поле блокируют импорт.

## Файлы данных

Текстовые UTF-8, одна запись в строке, поля разделены `|`; `|` и `\` внутри поля экранируются `\`, переводы строк заменяются пробелом.
Последнее поле каждой записи — стабильный id вида `<префикс>_<token>`; он не меняется при редактировании, импорте и sync.
Даты — `yyyy-MM-dd` (коммунальные — `yyyy-MM`); числа — точка как десятичный разделитель.

| Файл | Префикс id | Колонки (до id) |
|------|-----------|-----------------|
| `cars.dat` | `c_` | `car` |
| `fuel.dat` | `f_` | `date, car, km, liters, cost, price, comment, fullTank, purpose, station` |
| `repairs.dat` | `r_` | `date, car, km, category, comment, ?, ?, cost` |
| `expenses.dat` | `e_` | `date, car, category, comment, cost, ?` |
| `utilities.dat` | `u_` | `month, 7 сумм` |
| `trips.dat` | `g_` | `date, car, startKm, endKm, purpose, driver, comment` |
| `cars-meta.dat` | `m_` | `car, year, plate, vin, tankLiters, note` |
| `repair-categories.dat`, `expense-categories.dat` | `k_`, `p_` | категория |
| `templates.dat` | `t_` | `type, templateName, payload...` |
| `reminders.dat` | `n_` | `date, car, title, type` |
| `notes.dat` | `j_` | `date, car, text` |
| `settings.dat` | — | пары ключ/значение |
| `deleted.dat` | — | tombstones: `entityType, id, timestamp` |

`fullTank`: пусто или `1` — полный бак, `0` — частичная заправка. `purpose` поездки: `Работа` или любое другое значение (личная).
Новые необязательные колонки добавляются только в конец записи перед id; старые клиенты их игнорируют.

## Воспроизводимость отчётов

Отчёты периода (`PeriodCompare.Build` + `PeriodReport`) зависят только от файлов данных выбранного автомобиля и диапазона дат,
поэтому один и тот же snapshot даёт одинаковые числа на всех платформах. Проверяется `build/verify_snapshot_schema.py`.
