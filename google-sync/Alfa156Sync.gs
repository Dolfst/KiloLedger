/**
 * Alfa156 ↔ Google Sheets sync webhook.
 *
 * Установка (один раз утром):
 * 1. Откройте таблицу «Деньги».
 * 2. Расширения → Apps Script.
 * 3. Вставьте этот файл вместо Code.gs.
 * 4. В Script Properties добавьте ключ SYNC_TOKEN = любой секрет (например alfa156).
 * 5. Deploy → New deployment → Web app:
 *    - Execute as: Me
 *    - Who has access: Anyone
 * 6. Скопируйте URL веб-приложения в Alfa156 Desktop / Android.
 *
 * API:
 *   GET  ?action=pull&token=...
 *   POST { token, cars, fuel, repairs, utilities, categories }
 */

var FUEL_PREFIX = 'Заправки ';
var REPAIR_PREFIX = 'Ремонт ';
var UTILITIES_SHEET = 'Коммунальные';
var CATEGORIES_SHEET = 'Справочник';
var DEFAULT_CAR = 'Alfa Romeo 156';

var MONTHS_RU = [
  'Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь',
  'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'
];

function doGet(e) {
  try {
    var p = e && e.parameter ? e.parameter : {};
    assertToken_(p.token);
    if ((p.action || 'pull') !== 'pull') {
      return json_({ ok: false, error: 'Unknown action' });
    }
    return json_({ ok: true, data: exportSnapshot_() });
  } catch (err) {
    return json_({ ok: false, error: String(err && err.message ? err.message : err) });
  }
}

function doPost(e) {
  try {
    var body = JSON.parse(e.postData.contents);
    assertToken_(body.token);
    importSnapshot_(body);
    return json_({ ok: true });
  } catch (err) {
    return json_({ ok: false, error: String(err && err.message ? err.message : err) });
  }
}

function assertToken_(token) {
  var expected = PropertiesService.getScriptProperties().getProperty('SYNC_TOKEN');
  if (!expected) {
    throw new Error('В Script Properties задайте SYNC_TOKEN');
  }
  if (String(token || '') !== String(expected)) {
    throw new Error('Неверный token');
  }
}

function exportSnapshot_() {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var cars = [];
  var fuel = [];
  var repairs = [];
  var sheets = ss.getSheets();
  for (var i = 0; i < sheets.length; i++) {
    var name = sheets[i].getName();
    if (name.indexOf(FUEL_PREFIX) === 0) {
      var car = name.substring(FUEL_PREFIX.length).trim() || DEFAULT_CAR;
      if (!carIn_(cars, car)) cars.push([car]);
      fuel = fuel.concat(readFuel_(sheets[i], car));
    } else if (name.indexOf(REPAIR_PREFIX) === 0) {
      var carR = name.substring(REPAIR_PREFIX.length).trim() || DEFAULT_CAR;
      if (!carIn_(cars, carR)) cars.push([carR]);
      repairs = repairs.concat(readRepairs_(sheets[i], carR));
    }
  }
  if (cars.length === 0) cars.push([DEFAULT_CAR]);

  return {
    cars: cars,
    fuel: fuel,
    repairs: repairs,
    utilities: readUtilities_(ss),
    categories: readCategories_(ss)
  };
}

function importSnapshot_(data) {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var cars = data.cars || [];
  if (cars.length === 0) cars = [[DEFAULT_CAR]];

  ensureSheet_(ss, CATEGORIES_SHEET);
  ensureSheet_(ss, UTILITIES_SHEET);

  writeCategories_(ss, data.categories || []);
  writeUtilities_(ss, data.utilities || []);

  for (var i = 0; i < cars.length; i++) {
    var car = String(cars[i][0] || '').trim();
    if (!car) continue;
    writeFuel_(ss, car, filterByCar_(data.fuel || [], car));
    writeRepairs_(ss, car, filterByCar_(data.repairs || [], car));
  }
}

function readFuel_(sheet, car) {
  var values = sheet.getDataRange().getDisplayValues();
  var rows = [];
  for (var r = 1; r < values.length; r++) {
    var date = normalizeDate_(values[r][0]);
    if (!date) continue;
    rows.push([
      date,
      car,
      num_(values[r][1]),
      num_(values[r][2]),
      num_(values[r][3])
    ]);
  }
  return rows;
}

function readRepairs_(sheet, car) {
  var values = sheet.getDataRange().getDisplayValues();
  var rows = [];
  for (var r = 1; r < values.length; r++) {
    var date = normalizeDate_(values[r][0]);
    if (!date) continue;
    rows.push([
      date,
      car,
      num_(values[r][1]),
      String(values[r][2] || '').trim(),
      clean_(values[r][3]),
      clean_(values[r][4]),
      num_(values[r][5]) || '1',
      num_(values[r][6]),
      clean_(values[r][7])
    ]);
  }
  return rows;
}

function readUtilities_(ss) {
  var sheet = ss.getSheetByName(UTILITIES_SHEET);
  if (!sheet) return [];
  var values = sheet.getDataRange().getDisplayValues();
  var rows = [];
  for (var r = 1; r < values.length; r++) {
    var month = normalizeMonth_(values[r][0]);
    if (!month) continue;
    rows.push([
      month,
      num_(values[r][1]),
      num_(values[r][2]),
      num_(values[r][3]),
      num_(values[r][4]),
      num_(values[r][5]),
      num_(values[r][6]),
      num_(values[r][7])
    ]);
  }
  return rows;
}

function readCategories_(ss) {
  var sheet = ss.getSheetByName(CATEGORIES_SHEET);
  if (!sheet) return [];
  var values = sheet.getDataRange().getDisplayValues();
  var rows = [];
  var start = 0;
  if (values.length && String(values[0][0]).toLowerCase().indexOf('категор') === 0) start = 1;
  for (var r = start; r < values.length; r++) {
    var name = String(values[r][0] || '').trim();
    if (!name) continue;
    rows.push([name]);
  }
  return rows;
}

function writeFuel_(ss, car, rows) {
  var sheet = ensureSheet_(ss, FUEL_PREFIX + car);
  sheet.clearContents();
  var out = [['Дата', 'Пробег (км)', 'Литры', 'Стоимость', 'Расход на 100км']];
  for (var i = 0; i < rows.length; i++) {
    out.push([rows[i][0], rows[i][2], rows[i][3], rows[i][4], '']);
  }
  sheet.getRange(1, 1, out.length, out[0].length).setValues(out);
}

function writeRepairs_(ss, car, rows) {
  var sheet = ensureSheet_(ss, REPAIR_PREFIX + car);
  sheet.clearContents();
  var out = [['Дата', 'Пробег', 'Категория', 'Товар', 'Наименование', 'Кол-во', 'Сумма', 'Комментарий']];
  for (var i = 0; i < rows.length; i++) {
    out.push([
      rows[i][0], rows[i][2], rows[i][3], rows[i][4],
      rows[i][5], rows[i][6], rows[i][7], rows[i][8]
    ]);
  }
  sheet.getRange(1, 1, out.length, out[0].length).setValues(out);
}

function writeUtilities_(ss, rows) {
  var sheet = ensureSheet_(ss, UTILITIES_SHEET);
  sheet.clearContents();
  var out = [[
    'Дата \\ Вид', 'Коммунальные платежи', 'Электроэнергия', 'Газ',
    'Стационарный телефон', 'Домашний интернет', 'Налог на аренду', 'Аренда жилья'
  ]];
  for (var i = 0; i < rows.length; i++) {
    out.push([
      monthToRu_(rows[i][0]),
      rows[i][1], rows[i][2], rows[i][3], rows[i][4],
      rows[i][5], rows[i][6], rows[i][7]
    ]);
  }
  sheet.getRange(1, 1, out.length, out[0].length).setValues(out);
}

function writeCategories_(ss, rows) {
  var sheet = ensureSheet_(ss, CATEGORIES_SHEET);
  sheet.clearContents();
  var out = [['Категория', 'Описание / Назначение']];
  for (var i = 0; i < rows.length; i++) {
    out.push([rows[i][0], '']);
  }
  sheet.getRange(1, 1, out.length, 2).setValues(out);
}

function ensureSheet_(ss, name) {
  var sheet = ss.getSheetByName(name);
  if (!sheet) sheet = ss.insertSheet(name);
  return sheet;
}

function filterByCar_(rows, car) {
  var out = [];
  for (var i = 0; i < rows.length; i++) {
    if (String(rows[i][1] || '').toLowerCase() === String(car).toLowerCase()) out.push(rows[i]);
  }
  return out;
}

function carIn_(cars, car) {
  for (var i = 0; i < cars.length; i++) {
    if (String(cars[i][0]).toLowerCase() === String(car).toLowerCase()) return true;
  }
  return false;
}

function normalizeDate_(value) {
  var s = String(value || '').trim();
  if (!s) return '';
  if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return s;
  var m = s.match(/^(\d{1,2})[./](\d{1,2})[./](\d{4})$/);
  if (m) {
    return m[3] + '-' + pad_(m[2]) + '-' + pad_(m[1]);
  }
  var d = new Date(s);
  if (!isNaN(d.getTime())) {
    return d.getFullYear() + '-' + pad_(d.getMonth() + 1) + '-' + pad_(d.getDate());
  }
  return '';
}

function normalizeMonth_(value) {
  var s = String(value || '').trim();
  if (!s || /не определено/i.test(s)) return '';
  if (/^\d{4}-\d{2}$/.test(s)) return s;
  var m = s.match(/([A-Za-zА-Яа-яЁё]+)\s+(\d{4})/);
  if (!m) return '';
  var name = m[1].toLowerCase();
  for (var i = 0; i < MONTHS_RU.length; i++) {
    if (MONTHS_RU[i].toLowerCase() === name) {
      return m[2] + '-' + pad_(i + 1);
    }
  }
  return '';
}

function monthToRu_(ym) {
  var m = String(ym || '').match(/^(\d{4})-(\d{2})$/);
  if (!m) return ym;
  var idx = parseInt(m[2], 10) - 1;
  if (idx < 0 || idx > 11) return ym;
  return MONTHS_RU[idx] + ' ' + m[1];
}

function num_(value) {
  var s = String(value == null ? '' : value).trim().replace(/\s/g, '').replace(',', '.');
  if (!s || s.charAt(0) === '#') return '';
  var n = Number(s);
  return isNaN(n) ? s : String(n);
}

function clean_(value) {
  return String(value || '').replace(/\r?\n/g, ' ').trim();
}

function pad_(n) {
  n = String(n);
  return n.length < 2 ? '0' + n : n;
}

function json_(obj) {
  return ContentService
    .createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}
