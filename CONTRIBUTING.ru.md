# Участие в разработке KiloLedger

[English](CONTRIBUTING.md) · [Русский](CONTRIBUTING.ru.md)

KiloLedger — это три клиента и одна текстовая база: Windows Mobile (`KiloLedger/KiloLedger`, .NET Compact Framework 3.5), Android (`KiloLedger.Android`, .NET 10) и Windows (`KiloLedger.Desktop`, .NET Framework 4.8). Что приложения делают на самом деле, написано в [`README.ru.md`](README.ru.md). Английский текст — [`README.md`](README.md).

## Прежде чем менять поведение

- То же изменение нужно во всех клиентах, где это поведение есть. Копии сделаны нарочно. Один клиент не вызывает остальные.
- Форматы `.dat`, ZIP и `snapshot.json` должны открываться теми файлами, которые уже существуют. Правила колонок — в [`docs/snapshot-schema.md`](docs/snapshot-schema.md). Новое необязательное поле добавляют в конец записи, перед стабильным id. `schemaVersion` повышают только при сломе, который старые клиенты обязаны отвергнуть.
- На КПК допустимы только API из Compact Framework 3.5. Это проверяет `python3 build/verify_cf_apis.py`.
- Версия, которую видит человек, — одна строка в [`VERSION`](VERSION). Имена релизов — `KiloLedger-<версия>…`. Не оставляйте второй номер версии только в одном клиенте.
- Не коммитьте новые пароли, токены, хранилища ключей и местные журналы `*.dat`. `SYNC_TOKEN` и `%AppData%\KiloLedger\google-sync.json` остаются на машине пользователя.

## Проверки

Smoke-проверкам устройство не нужно:

```sh
python3 build/run_verifies.py
```

Если изменение видно по тексту исходников или по фикстуре (формат, цифры отчёта, правила импорта), добавьте или расширьте `build/verify_*.py`. Проверки русских заметок о выпуске читают `README.ru.md`, а не английский `README.md`. Workflow **Smoke verifies** запускает тот же набор и ещё собирает КПК командой `./build/build-kpk.sh Release` на Ubuntu.

Соберите затронутый клиент, если машина это позволяет:

- КПК: `./build/build-kpk.sh Release` или `build/build-kpk.ps1`.
- Desktop на Windows: MSBuild, конфигурация `Release`. На Linux в заметках репозитория указано `dotnet build KiloLedger.Desktop/KiloLedger.Desktop.csproj -p:FrameworkPathOverride=/usr/lib/mono/4.8-api`.
- Android: .NET 10 SDK, JDK 17, Android SDK. Команды — в разделе сборки README. CI публикует APK для ARM64.

## Документация

Пользовательские тексты ведутся на двух языках.

- [`README.md`](README.md) — английский. [`README.ru.md`](README.ru.md) — полный русский текст. В начале обоих стоят ссылки друг на друга.
- Этот файл и [`CONTRIBUTING.md`](CONTRIBUTING.md) устроены так же.
- Если меняется факт, обновляют оба языка. Не описывайте возможность, которая пока только в бэклоге [`ROADMAP.md`](ROADMAP.md) или [`docs/competitive-analysis.md`](docs/competitive-analysis.md).
- [`localization/LOCALIZATION.md`](localization/LOCALIZATION.md) — старая заметка. В клиентах уже есть языки `ru`, `en`, `es`, `fr` и `de`.

Сообщения коммитов в этом репозитории двуязычные, например: `Уточнить установку в README / Clarify install steps in the README`.

## Лицензия и соглашение участника

Программа распространяется на условиях GNU AGPL версии 3 или любой более поздней версии. Copyright (C) 2026 Dolfst. Текст — в [LICENSE](LICENSE).

Открывая pull request, вы соглашаетесь с [лицензионным соглашением участника](CLA.md) в отношении работы в этом запросе. Авторское право остаётся у вас. Вы даёте Dolfst бессрочную, всемирную, безотзывную, безвозмездную лицензию с правом выдавать сублицензии и перелицензировать вклад, в том числе на собственнических условиях, а также патентную лицензию и подтверждение оригинальности из CLA. Английский [CLA.md](CLA.md) главнее перевода [CLA.ru.md](CLA.ru.md).

Чтобы подписать, отметьте пункт Contributor License Agreement в шаблоне pull request ([`.github/pull_request_template.md`](.github/pull_request_template.md)). Если шаблона в описании нет, напишите фразу, что вы согласны с `CLA.md`. Приложение GitHub [CLA Assistant](https://github.com/cla-assistant/cla-assistant) умеет записывать подписи само. Ставить его для этого репозитория не требуется.

## Ошибки и идеи

Замечание открывают в [Dolfst/KiloLedger](https://github.com/Dolfst/KiloLedger/issues). Напишите клиент, версию (`VERSION` или экран «О программе») и откуда данные: папка `.dat`, ZIP, Wi-Fi или Google Таблицы.

Автоматическим участникам дополнительно заданы [`AGENTS.md`](AGENTS.md) и `.github/skills/`.
