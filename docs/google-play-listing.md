# Google Play Console Publishing Guide — KiloLedger (`com.kiloledger.app`)

Этот документ содержит всё необходимое для публикации **KiloLedger** в **Google Play Console**.

---

## 1. Сборка `.aab` в GitHub Actions

1. Откройте вкладку **Actions → Android Play AAB** в репозитории `Dolfst/KiloLedger`.
2. Workflow автоматически собирает подписанный multi-ABI App Bundle (`arm64-v8a` + `armeabi-v7a`) при изменениях в `KiloLedger.Android/**`, `VERSION` или вручную через **Run workflow**.
3. Скачайте готовый файл `KiloLedger-<version>-vc<versionCode>.aab` из раздела **Artifacts** нужного запуска.

### Ключ подписи (Upload Keystore) и `versionCode`
- Сейчас `Android Play AAB` использует `PLAY_KEYSTORE_*` (если заданы в GitHub Secrets) либо fallback на уже настроенные `ANDROID_KEYSTORE_*`.
- **Важно:** при первой загрузке в Google Play Console обязательно включите **Play App Signing** (рекомендуется Google по умолчанию). Тогда Google будет подписывать релизы своим App Signing key, а ваш keystore останется ключом загрузки (Upload Key).
- Если вы уже загружали в Play Console сборку с каким-то `versionCode`, следующий загружаемый `.aab` обязан иметь строго больший `versionCode` (при ручном запуске **Run workflow** можно указать параметр `version_code`).

---

## 2. Пошаговый чеклист в Google Play Console

1. **Аккаунт разработчика:** зарегистрируйтесь в [Google Play Console](https://play.google.com/console) ($25 разовый сбор + подтверждение личности).
2. **Создать приложение:**
   - Название: `KiloLedger`
   - Язык по умолчанию: Русский (или English)
   - Приложение или игра: **Приложение**
   - Бесплатное или платное: **Бесплатное**
3. **Политика конфиденциальности (Privacy Policy URL):**
   - Включите **GitHub Pages** в настройках репозитория (`Settings → Pages → Source: Deploy from a branch → main / docs` или `/ (root)`).
   - Ссылка будет вида `https://dolfst.github.io/KiloLedger/docs/privacy-policy.html` (если root) или `https://dolfst.github.io/KiloLedger/privacy-policy.html` (если `/docs`).
4. **Настройка приложения (App content):**
   - **Доступ к приложению:** все функции доступны без входа в аккаунт.
   - **Реклама:** Нет, приложение не содержит рекламу.
   - **Возрастной рейтинг (IARC):** утилита / финансы / авто → не содержит запрещённого контента → рейтинг `3+` / `Everyone`.
   - **Целевая аудитория:** `18+` (чтобы не подпадать под требования Families Policy).
   - **Безопасность данных (Data Safety):**
     - Собирает или передаёт ли приложение данные пользователей? → **Нет (No)** *(приложение работает полностью локально; опциональная синхронизация идёт напрямую в собственный Google Apps Script пользователя или по его локальной Wi‑Fi сети без участия серверов разработчика)*.
     - Шифрование при передаче: включите, если используете HTTPS к Google Apps Script (опционально).
5. **Графика для страницы в Google Play:**
   - **Значок приложения:** `512 × 512 px`, PNG 32-bit — **уже готов в репозитории:** `assets/logo/logo-512.png`.
   - **Картинка для описания (Feature Graphic):** `1024 × 500 px`, PNG или JPEG.
   - **Скриншоты телефона:** минимум 2–4 шт. (рекомендуется 1080×1920 или реальные скриншоты с телефона: главный список, сводка с графиком, добавление заправки, настройки/синхронизация).
6. **Требование закрытого тестирования (для новых личных аккаунтов):**
   - Для личных аккаунтов Google Play, созданных после ноября 2023 года, перед выходом в **Рабочую версию (Production)** требуется провести **Закрытое тестирование (Closed testing)** с участием не менее **12 тестеров в течение 14 дней подряд**.

---

## 3. Готовые тексты карточки магазина (5 языков)

### Русский (`ru-RU`)
- **Название:** `KiloLedger`
- **Краткое описание (75/80 симв.):**
  `Офлайн-учёт авто: заправки, ТО, чеки, поездки, бюджет и отчёты без рекламы.`
- **Полное описание:**
  ```text
  KiloLedger — удобный офлайн-трекер расходов автомобиля, технического обслуживания и поездок без рекламы, подписок и привязки к сторонним серверам.

  Возможности:
  • Учёт заправок: пробег, литры, стоимость, цена за литр, АЗС, полный/частичный бак и точный расчёт среднего расхода (л/100 км).
  • Ремонт и ТО: категории работ и запчастей, напоминания по пробегу (замена масла, ремень ГРМ) с прогнозом срока и пробега до следующего обслуживания.
  • Прочие расходы и коммунальные платежи: страховки, налоги, парковка, а также помесячный учёт коммунальных услуг.
  • Поездки и заметки: журнал поездок (личные/рабочие) и заметки по каждому автомобилю.
  • Фото чеков: прикрепляйте фотографии чеков с камеры или из галереи прямо к записям.
  • Сводка и аналитика: график расходов за 12 месяцев, сравнение автомобилей, сравнение периодов, месячный бюджет, отчёты в TXT и HTML.
  • Резервные копии и синхронизация: полный экспорт/импорт ZIP с проверкой целостности, локальная Wi‑Fi синхронизация и опциональная синхронизация с собственной Google Таблицей (через ваш Apps Script).
  • 5 языков интерфейса: русский, английский, испанский, французский и немецкий.

  Все данные хранятся локально на вашем устройстве — вы полностью контролируете свои записи.
  ```

### English (`en-US`)
- **Title:** `KiloLedger`
- **Short description (73/80 chars):**
  `Offline car expense tracker: fuel, maintenance, receipts, trips & no ads.`
- **Full description:**
  ```text
  KiloLedger is a fast, offline-first vehicle expense, maintenance, and trip tracker with zero ads, zero subscriptions, and complete control over your data.

  Key Features:
  • Fuel Log: track odometer, liters, total cost, price per liter, gas station, full/partial fills, and accurate L/100 km consumption.
  • Maintenance & Repairs: customizable categories plus oil and timing belt interval reminders with mileage/date forecasts.
  • Other Expenses & Utilities: log insurance, taxes, washes, and monthly household utilities.
  • Trips & Notes: odometer trip log (personal/business) and per-vehicle notes.
  • Receipt Photos: attach camera or gallery receipt photos directly to any expense record.
  • Analytics & Reports: 12-month expense chart, vehicle comparison, period-over-period comparison, monthly budget alerts, and TXT/HTML reports.
  • Backups & Sync: full ZIP backup/restore with integrity checks, local Wi‑Fi sync, and optional sync with your own Google Sheets (via self-hosted Apps Script).
  • 5 Languages: English, Russian, Spanish, French, and German.

  Privacy-first: all records stay locally on your device.
  ```

### Español (`es-ES`)
- **Título:** `KiloLedger`
- **Descripción breve (73/80 car.):**
  `Control de gastos del coche offline: combustible, mantenimiento y viajes.`
- **Descripción completa:**
  ```text
  KiloLedger es una aplicación offline para llevar el control de gastos de tu coche, mantenimientos y viajes, sin anuncios ni suscripciones.

  Funciones principales:
  • Repostajes: kilometraje, litros, importe, precio/litro, estación, depósito lleno y cálculo de consumo (L/100 km).
  • Mantenimiento y reparaciones: categorías personalizadas y recordatorios de cambio de aceite y correa con previsión.
  • Otros gastos y suministros: seguros, impuestos y gastos mensuales.
  • Viajes y notas: registro de trayectos (personal/trabajo) y notas por vehículo.
  • Fotos de recibos: adjunta fotos de tickets desde la cámara o galería.
  • Informes y presupuesto: gráfico de 12 meses, comparación de vehículos y períodos, presupuesto mensual y exportación TXT/HTML.
  • Copias de seguridad: exportación/importación ZIP, sincronización Wi‑Fi local y sincronización opcional con tu propia hoja de Google Sheets.
  ```

### Français (`fr-FR`)
- **Titre :** `KiloLedger`
- **Description courte (73/80 car.) :**
  `Suivi auto hors ligne : carburant, entretien, reçus, trajets et sans pub.`
- **Description complète :**
  ```text
  KiloLedger est un carnet d'entretien et de dépenses automobiles hors ligne, sans publicité ni abonnement.

  Fonctionnalités :
  • Carburant : kilométrage, litres, coût, station, plein complet/partiel et calcul précis de la consommation (L/100 km).
  • Entretien et réparations : catégories personnalisées et rappels de vidange et courroie avec estimation.
  • Autres dépenses et charges : assurance, taxes et suivi mensuel.
  • Trajets et notes : suivi des déplacements (personnel/professionnel) et notes par véhicule.
  • Photos des reçus : joignez des photos de tickets à vos dépenses.
  • Analyses et rapports : graphique sur 12 mois, comparaison de véhicules et périodes, budget mensuel, export TXT/HTML.
  • Sauvegardes : export/import ZIP, synchronisation Wi‑Fi locale et synchronisation optionnelle avec votre propre Google Sheets.
  ```

### Deutsch (`de-DE`)
- **Titel:** `KiloLedger`
- **Kurzbeschreibung (72/80 Zeichen):**
  `Offline-Autokosten-Tracker: Tanken, Wartung, Belege, Fahrten, werbefrei.`
- **Vollständige Beschreibung:**
  ```text
  KiloLedger ist ein werbefreier Offline-Tracker für Fahrzeugkosten, Wartung und Fahrten — ohne Abos und ohne Cloud-Zwang.

  Funktionen:
  • Tankbuch: Kilometerstand, Liter, Kosten, Literpreis, Tankstelle, Voll-/Teiltankung und genauer Verbrauch (L/100 km).
  • Wartung & Reparaturen: eigene Kategorien sowie Ölwechsel- und Zahnriemen-Erinnerungen mit Prognose.
  • Weitere Ausgaben & Nebenkosten: Versicherung, Steuern und monatliche Nebenkosten.
  • Fahrten & Notizen: Fahrtenbuch (Privat/Dienstlich) und Fahrzeugnotizen.
  • Belegfotos: Kassenbons per Kamera oder Galerie direkt an Einträge anhängen.
  • Berichte & Budget: 12-Monats-Diagramm, Fahrzeug- und Periodenvergleich, Monatsbudget, TXT/HTML-Export.
  • Backups & Sync: ZIP-Sicherung, lokaler WLAN-Abgleich und optionaler Abgleich mit eigener Google-Tabelle.
  ```
