# Privacy Policy / Политика конфиденциальности — KiloLedger

**Effective Date / Дата вступления в силу:** 2026-11-24  
**Package / Идентификатор приложения:** `com.kiloledger.app`  
**Developer / Разработчик:** Dolfst (`Dolfst@gmail.com`)  
**Repository / Репозиторий:** https://github.com/Dolfst/KiloLedger

---

## English

### 1. Overview
**KiloLedger** is an offline-first personal vehicle expense, maintenance, trip, and utility tracker. The developer does **not** operate backend servers, does **not** display advertisements, and does **not** include third-party analytics, crash-reporting, or tracking SDKs.

### 2. Data Stored Locally on Your Device
All records you enter (vehicles, odometer readings, fuel purchases, repairs/maintenance, other expenses, utility bills, trips, notes, reminders, settings, and receipt photos) are stored locally in the app's private storage on your device.

### 3. Permissions Used
- **Camera (`android.permission.CAMERA`)** — Optional. Used only when you explicitly choose to capture a receipt photo to attach to an expense record. Photos remain stored locally on your device unless you export or sync them yourself.
- **Notifications (`android.permission.POST_NOTIFICATIONS`)** — Optional. Used only to show local reminders on your device (oil/belt maintenance intervals, monthly budget warnings, and date reminders).
- **Internet (`android.permission.INTERNET`)** — Used only when you manually initiate:
  1. **Local Wi‑Fi sync** between your Android phone and your Pocket PC device on a trusted local network (port `45816`).
  2. **Optional Google Sheets sync** via your own self-hosted Google Apps Script (`KiloLedgerSync.gs`) URL and token configured by you.

### 4. Data Sharing & Third Parties
KiloLedger does **not** collect, sell, rent, or transmit your personal data to the developer or any third parties. If you choose to connect your own Google Apps Script Web App, your expense rows are sent directly from your device to your own Google account under Google's privacy terms.

### 5. Data Retention & Deletion
You have full control over your data at all times:
- You can export a ZIP backup or delete individual records/receipts inside the app.
- Uninstalling KiloLedger deletes the app's private local data directory from your device.

### 6. Children's Privacy
KiloLedger is a utility/automotive finance tool intended for general adult audiences (18+) and does not knowingly collect personal information from children.

### 7. Contact
If you have questions about this Privacy Policy, please contact `Dolfst@gmail.com` or open an issue at https://github.com/Dolfst/KiloLedger/issues.

---

## Русский

### 1. Общие положения
**KiloLedger** — офлайн-приложение для учёта расходов автомобиля, ТО, поездок и коммунальных платежей. Разработчик **не** использует собственные серверы сбора данных, **не** показывает рекламу и **не** встраивает сторонние SDK аналитики, телеметрии или отслеживания.

### 2. Локальное хранение данных
Все вносимые вами данные (автомобили, пробег, заправки, ремонт/ТО, прочие расходы, коммунальные платежи, поездки, заметки, напоминания, настройки и фото чеков) хранятся локально в защищённой памяти приложения на вашем устройстве.

### 3. Используемые разрешения
- **Камера (`android.permission.CAMERA`)** — необязательное разрешение. Запрашивается только тогда, когда вы сами нажимаете «сфотографировать чек». Снимки хранятся локально на устройстве, пока вы сами не экспортируете или не синхронизируете их.
- **Уведомления (`android.permission.POST_NOTIFICATIONS`)** — необязательное разрешение. Используется только для локальных напоминаний (интервалы ТО, лимит бюджета, даты страховки/техосмотра).
- **Доступ в сеть (`android.permission.INTERNET`)** — используется исключительно по вашей инициативе для:
  1. **Локальной синхронизации по Wi‑Fi** с вашим КПК в доверенной локальной сети (порт `45816`).
  2. **Опциональной синхронизации с Google Таблицами** через ваш собственный скрипт Google Apps Script (`KiloLedgerSync.gs`), URL и токен которого вы задаёте самостоятельно.

### 4. Передача данных третьим лицам
KiloLedger **не** собирает, **не** продаёт и **не** передаёт ваши данные разработчику или третьим лицам. При подключении собственного скрипта Google Apps Script обмен идёт напрямую между вашим устройством и вашим аккаунтом Google.

### 5. Хранение и удаление данных
Вы полностью управляете своими данными:
- В любой момент можно сделать резервную копию (Экспорт ZIP) или удалить отдельные записи и фото чеков в приложении.
- Удаление приложения полностью удаляет локальные данные из памяти устройства.

### 6. Контакты
По вопросам политики конфиденциальности пишите на `Dolfst@gmail.com` или создайте обращение в репозитории https://github.com/Dolfst/KiloLedger/issues.
