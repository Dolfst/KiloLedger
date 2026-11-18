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

var SPREADSHEET_ID = '1h-EJLq7PhsP_XDSsdnKMLsB7_nxxxFeoT_WKUJ4XgJA';
function getSs_() {
  try { return SpreadsheetApp.getActiveSpreadsheet() || SpreadsheetApp.openById(SPREADSHEET_ID); }
  catch (e) { return SpreadsheetApp.openById(SPREADSHEET_ID); }
}


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
  if (!expected) { expected = 'alfa156'; }
  if (String(token || '') !== String(expected)) {
    throw new Error('Неверный token');
  }
}

function exportSnapshot_() {
  var ss = getSs_();
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
  var ss = getSs_();
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
  pruneCarSheets_(ss, cars);
}

function readFuel_(sheet, car) {
  var values = sheet.getDataRange().getValues();
  var display = sheet.getDataRange().getDisplayValues();
  var rows = [];
  for (var r = 1; r < values.length; r++) {
    var date = normalizeDate_(values[r][0], display[r][0]);
    if (!date) continue;
    var km = num_(display[r][1]);
    var liters = num_(display[r][2]);
    var cost = num_(display[r][3]);
    if (!km && !liters && !cost) continue;
    rows.push([date, car, km, liters, cost]);
  }
  return rows;
}

function readRepairs_(sheet, car) {
  var values = sheet.getDataRange().getValues();
  var display = sheet.getDataRange().getDisplayValues();
  var rows = [];
  for (var r = 1; r < values.length; r++) {
    var date = normalizeDate_(values[r][0], display[r][0]);
    if (!date) continue;
    var product = clean_(display[r][3]);
    var description = clean_(display[r][4]);
    var sum = num_(display[r][6]);
    // пропускаем совсем пустые хвосты
    if (!product && !description && !sum && !clean_(display[r][2])) continue;
    rows.push([
      date,
      car,
      num_(display[r][1]),
      String(display[r][2] || '').trim(),
      product,
      description,
      num_(display[r][5]) || '1',
      sum,
      clean_(display[r][7])
    ]);
  }
  return rows;
}

function readUtilities_(ss) {
  var sheet = ss.getSheetByName(UTILITIES_SHEET);
  if (!sheet) return [];
  var display = sheet.getDataRange().getDisplayValues();
  var rows = [];
  for (var r = 1; r < display.length; r++) {
    var month = normalizeMonth_(display[r][0]);
    if (!month) continue;
    var row = [
      month,
      num_(display[r][1]),
      num_(display[r][2]),
      num_(display[r][3]),
      num_(display[r][4]),
      num_(display[r][5]),
      num_(display[r][6]),
      num_(display[r][7])
    ];
    var any = false;
    for (var c = 1; c < 8; c++) if (row[c]) { any = true; break; }
    if (!any) continue;
    rows.push(row);
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
  clearSheetForWrite_(sheet);
  var out = [['Дата', 'Пробег (км)', 'Литры', 'Стоимость', 'Расход на 100км']];
  for (var i = 0; i < rows.length; i++) {
    out.push([rows[i][0], rows[i][2], rows[i][3], rows[i][4], '']);
  }
  sheet.getRange(1, 1, out.length, out[0].length).setValues(out);
}

function writeRepairs_(ss, car, rows) {
  var sheet = ensureSheet_(ss, REPAIR_PREFIX + car);
  clearSheetForWrite_(sheet);
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
  clearSheetForWrite_(sheet);
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
  clearSheetForWrite_(sheet);
  var out = [['Категория', 'Описание / Назначение']];
  for (var i = 0; i < rows.length; i++) {
    out.push([rows[i][0], '']);
  }
  sheet.getRange(1, 1, out.length, 2).setValues(out);
}

/** Снимаем data validation — иначе setValues падает (категории вне списка после restore). */
function clearSheetForWrite_(sheet) {
  try { sheet.clearDataValidations(); } catch (e) {}
  try { sheet.clearContents(); } catch (e) { sheet.clear(); }
}

/** Удаляет пустые/лишние листы Заправки/Ремонт для машин не из списка. */
function pruneCarSheets_(ss, cars) {
  var keep = {};
  for (var i = 0; i < cars.length; i++) {
    var name = String(cars[i][0] || '').trim();
    if (name) keep[name] = true;
  }
  var sheets = ss.getSheets();
  for (var s = 0; s < sheets.length; s++) {
    var title = sheets[s].getName();
    var car = null;
    if (title.indexOf(FUEL_PREFIX) === 0) car = title.substring(FUEL_PREFIX.length).trim();
    else if (title.indexOf(REPAIR_PREFIX) === 0) car = title.substring(REPAIR_PREFIX.length).trim();
    else continue;
    if (car && !keep[car]) {
      try { ss.deleteSheet(sheets[s]); } catch (e) {}
    }
  }
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

function normalizeDate_(value, display) {
  if (Object.prototype.toString.call(value) === '[object Date]' && !isNaN(value.getTime())) {
    return value.getFullYear() + '-' + pad_(value.getMonth() + 1) + '-' + pad_(value.getDate());
  }
  if (typeof value === 'number' && value > 20000 && value < 80000) {
    var epoch = new Date(Date.UTC(1899, 11, 30));
    epoch.setUTCDate(epoch.getUTCDate() + Math.floor(value));
    return epoch.getUTCFullYear() + '-' + pad_(epoch.getUTCMonth() + 1) + '-' + pad_(epoch.getUTCDate());
  }
  var s = String((display != null && display !== '') ? display : (value || '')).trim();
  if (!s) return '';
  if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return s;
  var m = s.match(/^(\d{1,2})[./](\d{1,2})[./](\d{4})$/);
  if (m) {
    return m[3] + '-' + pad_(m[2]) + '-' + pad_(m[1]);
  }
  // "18 авг. 2025" / "18 августа 2025"
  m = s.match(/^(\d{1,2})\s+([A-Za-zА-Яа-яЁё.]+)\s+(\d{4})$/);
  if (m) {
    var month = russianMonthIndex_(m[2]);
    if (month >= 0) return m[3] + '-' + pad_(month + 1) + '-' + pad_(m[1]);
  }
  var d = new Date(s);
  if (!isNaN(d.getTime())) {
    return d.getFullYear() + '-' + pad_(d.getMonth() + 1) + '-' + pad_(d.getDate());
  }
  return '';
}

function russianMonthIndex_(name) {
  var n = String(name || '').toLowerCase().replace(/\./g, '');
  var names = [
    ['январ', 'янв', 'january', 'jan'],
    ['феврал', 'фев', 'february', 'feb'],
    ['март', 'мар', 'march', 'mar'],
    ['апрел', 'апр', 'april', 'apr'],
    ['ма', 'may'],
    ['июн', 'june', 'jun'],
    ['июл', 'july', 'jul'],
    ['август', 'авг', 'august', 'aug'],
    ['сентябр', 'сен', 'september', 'sep'],
    ['октябр', 'окт', 'october', 'oct'],
    ['ноябр', 'ноя', 'november', 'nov'],
    ['декабр', 'дек', 'december', 'dec']
  ];
  for (var i = 0; i < names.length; i++) {
    for (var j = 0; j < names[i].length; j++) {
      if (n.indexOf(names[i][j]) === 0) return i;
    }
  }
  return -1;
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
  var s = String(value == null ? '' : value).trim();
  if (!s || s.charAt(0) === '#') return '';
  s = s.replace(/\s/g, '')
    .replace(/р\./gi, '')
    .replace(/руб\.?/gi, '')
    .replace(/BYN/gi, '')
    .replace(/₽/g, '')
    .replace(',', '.');
  s = s.replace(/[^0-9.\-]/g, '');
  if (!s || s === '-' || s === '.') return '';
  var n = Number(s);
  return isNaN(n) ? '' : String(n);
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


