import ExcelJS from 'exceljs';

export function createWorkbook(metadata, summary, details) {
  const workbook = new ExcelJS.Workbook(); workbook.creator = 'Human Ghost';
  for (const [name, rows] of [['조회 조건', metadata], ['집계', summary], ['상세', details]]) {
    const sheet = workbook.addWorksheet(name);
    const keys = [...new Set(rows.flatMap(row => Object.keys(row)))];
    sheet.columns = keys.map(key => ({ header: key, key, width: 24 }));
    // 문자열을 수식 객체로 해석하지 않고 값 그대로 기록합니다.
    sheet.addRows(rows.map(row => Object.fromEntries(Object.entries(row).map(([key, value]) =>
      [key, value != null && typeof value === 'object' ? JSON.stringify(value) : value]))));
    sheet.views = [{ state: 'frozen', ySplit: 1 }];
    sheet.getRow(1).font = { bold: true };
    if (keys.length) sheet.autoFilter = { from: { row: 1, column: 1 }, to: { row: 1, column: keys.length } };
  }
  return workbook;
}
