/**
 * Gas Laws Dive: receives the game's play records and writes them into this Google Sheet.
 * Setup: docs/LOGGING_SETUP.md. Paste this whole file into Extensions > Apps Script of the Sheet.
 *
 * The game posts {"token": "...", "rows": [ {...}, ... ]} as text/plain.
 * - Rows from a build whose version ends in "-pilot" go to the tab "Pilot", all others to "Study",
 *   so pilot data (not used in the study) stay apart.
 * - A row whose row_id is already in the tab is skipped, so a batch sent twice is stored once.
 * - Rows that do not look like the game's rows are refused (counted, not stored).
 * - Text that starts with = + - or @ is stored as plain text, never as a spreadsheet formula.
 * - The game holds no names or usernames, and this script stores none: only the coded study ID.
 */

var TOKEN = 'c5xizv6atncbcthj';   // must match GameConfig.LogToken in the game
var COLUMNS = ['row_id', 'study_id', 'section', 'version', 'build_version', 'session_id', 'dive_id',
               'question_id', 'answer', 'correct', 'attempt_number', 'time_on_question_s',
               'time_in_dive_s', 'platform', 'timestamp', 'fps_avg', 'fps_p5', 'canvas_w', 'canvas_h',
               'received'];
// Measured once per dive and written on that dive's rows; 0 means not measured (the survey rows),
// stored as an empty cell.
var MEASURES = ['fps_avg', 'fps_p5', 'canvas_w', 'canvas_h'];
var MAX_ROWS_PER_POST = 200;

function doGet() {
  return ContentService.createTextOutput('Gas Laws Dive logger is running.');
}

function doPost(e) {
  var data;
  try { data = JSON.parse(e.postData.contents); } catch (err) { return reply({ ok: false, error: 'not JSON' }); }
  if (!data || data.token !== TOKEN) return reply({ ok: false, error: 'bad token' });
  var rows = Array.isArray(data.rows) ? data.rows.slice(0, MAX_ROWS_PER_POST) : [];

  var lock = LockService.getScriptLock();
  lock.waitLock(30000);
  try {
    var added = 0, duplicates = 0, refused = 0;
    var byTab = {};
    rows.forEach(function (r) {
      if (!looksRight(r)) { refused++; return; }
      var tab = /-pilot$/.test(r.build_version) ? 'Pilot' : 'Study';
      (byTab[tab] = byTab[tab] || []).push(r);
    });
    Object.keys(byTab).forEach(function (tab) {
      var sheet = sheetFor(tab);
      var seen = existingIds(sheet);
      var now = new Date().toISOString();
      var out = [];
      byTab[tab].forEach(function (r) {
        if (seen[r.row_id]) { duplicates++; return; }
        seen[r.row_id] = true;
        out.push(COLUMNS.map(function (c) {
          if (c === 'received') return now;
          if (MEASURES.indexOf(c) >= 0 && !r[c]) return '';   // not measured
          return clean(r[c]);
        }));
      });
      if (out.length) {
        sheet.getRange(sheet.getLastRow() + 1, 1, out.length, COLUMNS.length).setValues(out);
        added += out.length;
      }
    });
    return reply({ ok: true, added: added, duplicates: duplicates, refused: refused });
  } finally {
    lock.releaseLock();
  }
}

function looksRight(r) {
  return r && typeof r === 'object'
    && /^[0-9a-f]{12}-\d{4}$/.test(r.row_id || '')
    && /^[0-9a-f]{10}$/.test(r.study_id || '')
    && /^(A|B)$/.test(r.version || '')
    && /^[A-Z0-9]{1,10}$|^none$/.test(r.section || '')
    && typeof r.build_version === 'string' && r.build_version.length <= 40
    && typeof r.question_id === 'string' && r.question_id.length <= 40
    && typeof r.answer === 'string' && r.answer.length <= 2000
    && /^(desktop|mobile)$/.test(r.platform || '')
    && inRange(r.fps_avg, 0, 1000) && inRange(r.fps_p5, 0, 1000)
    && inRange(r.canvas_w, 0, 20000) && inRange(r.canvas_h, 0, 20000);
}

// A measure may be missing (a row from an older build); if present it must be a number in range.
function inRange(v, lo, hi) {
  return v === undefined || v === null || (typeof v === 'number' && isFinite(v) && v >= lo && v <= hi);
}

function sheetFor(name) {
  var book = SpreadsheetApp.getActiveSpreadsheet();
  var sheet = book.getSheetByName(name);
  if (!sheet) {
    sheet = book.insertSheet(name);
    sheet.getRange(1, 1, 1, COLUMNS.length).setValues([COLUMNS]).setFontWeight('bold');
    sheet.setFrozenRows(1);
    sheet.getRange('A:A').setNumberFormat('@');   // row ids stay text
  }
  return sheet;
}

function existingIds(sheet) {
  var seen = {};
  var last = sheet.getLastRow();
  if (last < 2) return seen;
  sheet.getRange(2, 1, last - 1, 1).getValues().forEach(function (v) { seen[v[0]] = true; });
  return seen;
}

function clean(v) {
  if (v === null || v === undefined) return '';
  if (typeof v === 'string' && /^[=+\-@]/.test(v)) return "'" + v;   // never a formula
  return v;
}

function reply(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj)).setMimeType(ContentService.MimeType.JSON);
}

/** Run this once from the editor (select it, press Run) to check the script can write to the Sheet. */
function testWrite() {
  var fake = { token: TOKEN, rows: [{
    row_id: '000000000000-0001', study_id: '0000000000', section: 'TEST', version: 'A',
    build_version: 'setup-test-pilot', session_id: '000000000000', dive_id: 0, question_id: 'SETUP',
    answer: 'A', correct: true, attempt_number: 1, time_on_question_s: 0, time_in_dive_s: 0,
    platform: 'desktop', timestamp: new Date().toISOString(),
    fps_avg: 60, fps_p5: 55, canvas_w: 1280, canvas_h: 720 }] };
  var out = doPost({ postData: { contents: JSON.stringify(fake) } });
  Logger.log(out.getContent());
}
