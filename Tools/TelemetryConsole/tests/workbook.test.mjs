import test from 'node:test';
import assert from 'node:assert/strict';
import ExcelJS from 'exceljs';
import { createWorkbook } from '../src/workbook.js';

test('엑셀 집계·상세·조회 범위가 같은 파일에 보존되고 문자열은 수식이 되지 않음', async () => {
  const source = createWorkbook([{ '전체 집계': '아니오(부분 집계)' }], [{ '혼 획득': 10, '혼 소비': 4 }],
    [{ '오류': '=1+1', '악성 값': { formula: '1+1' } }]);
  const buffer = await source.xlsx.writeBuffer();
  const copy = new ExcelJS.Workbook();
  await copy.xlsx.load(buffer);
  assert.deepEqual(copy.worksheets.map(sheet => sheet.name), ['조회 조건', '집계', '상세']);
  assert.equal(copy.getWorksheet('조회 조건').getCell('A2').value, '아니오(부분 집계)');
  assert.equal(copy.getWorksheet('집계').getCell('B2').value, 4);
  assert.equal(copy.getWorksheet('상세').getCell('A2').value, '=1+1');
  assert.equal(typeof copy.getWorksheet('상세').getCell('B2').value, 'string');
});
