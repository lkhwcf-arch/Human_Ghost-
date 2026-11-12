import test from 'node:test';
import assert from 'node:assert/strict';
import { normalize, filterRecords, aggregate, summaryRows } from '../src/metrics.js';

let next = 0;
const row = (name, fields = {}, session = 'a', timestamp = 1000) => ({ EventId: String(++next), EventName: name,
  Category: name === 'error' ? 'errors' : 'statistics', SchemaVersion: 1, AnonymousId: 'install', SessionId: session,
  BuildVersion: 'build', DataVersion: 'data', BuildMode: 'development', TimestampMs: timestamp, ElapsedMs: timestamp, Fields: fields });

test('중복 제거와 기간·버전 조건을 동일 집계에 적용', () => {
  const a = row('stage_enter', { stage_id: 1 });
  const result = normalize([a, a, { Fields: null }]);
  assert.equal(result.records.length, 1); assert.equal(result.invalid, 1);
  assert.equal(filterRecords(result.records, { start: 0, end: 1001, build: 'other' }).length, 0);
  assert.equal(filterRecords(result.records, { start: 0, end: 1000 }).length, 0);
});
test('재화 획득·소비를 순증감과 구분하고 스냅샷을 수급량에서 제외', () => {
  const groups = aggregate([row('currency_changed', { currency_id: 1, delta: 10 }),
    row('currency_changed', { currency_id: 1, delta: -4 }), row('currency_snapshot', { currency_id: 1, balance: 90 })], 'currency');
  assert.equal(groups[0].gain, 10); assert.equal(groups[0].spent, 4); assert.equal(groups[0].net, 6);
});
test('반복 사망은 횟수와 고유 사망 세션을 구분하고 진입 기록을 분모로 사용', () => {
  const groups = aggregate([row('stage_enter', { stage_id: 1 }, 'a'), row('stage_enter', { stage_id: 1 }, 'b'),
    row('player_death', { stage_id: 1 }, 'a'), row('player_death', { stage_id: 1 }, 'a')], 'deaths');
  assert.equal(groups[0].count, 2); assert.equal(groups[0].sessionCount, 1); assert.equal(groups[0].rate, 50);
  assert.equal(aggregate([row('player_death', { stage_id: 1 })], 'deaths')[0].rate, null);
});
test('갈림길별 선택 횟수를 분모로 사용하고 다른 갈림길은 섞지 않음', () => {
  const rows = [row('route_selected', { junction_id: 1, route_id: 1 }), row('route_selected', { junction_id: 1, route_id: 2 }),
    row('route_selected', { junction_id: 2, route_id: 1 })];
  const groups = aggregate(rows, 'routes');
  assert.equal(groups.find(g => g.dimensions.junction_id === 2).rate, 100);
  assert.equal(groups.find(g => g.dimensions.junction_id === 1).rate, 50);
});
test('소비량 순위는 이벤트 횟수 대신 실제 수량 합계 사용', () => {
  const groups = aggregate([row('item_consumed', { item_id: 1, quantity: 1 }), row('item_consumed', { item_id: 2, quantity: 3 })], 'consumed');
  assert.equal(groups[0].dimensions.item_id, 2); assert.equal(summaryRows(groups, 'consumed')[0]['수량'], 3);
});
test('마지막 관측은 세션별 마지막 진입만 사용하고 정상 종료와 클리어를 혼동하지 않음', () => {
  const groups = aggregate([row('stage_enter', { stage_id: 1 }, 'a', 1000), row('stage_enter', { stage_id: 2 }, 'a', 2000),
    row('session_end', {}, 'a', 3000), row('stage_enter', { stage_id: 2 }, 'b', 2000)], 'progress');
  assert.equal(groups.length, 1); assert.equal(groups[0].count, 2); assert.equal(groups[0].missingEnd, 1);
});
test('같은 오류를 묶되 서로 다른 스택은 별도 처리', () => {
  const groups = aggregate([row('error', { kind: 'Error', message: '문제', stack: 'a' }),
    row('error', { kind: 'Error', message: '문제', stack: 'a' }), row('error', { kind: 'Error', message: '문제', stack: 'b' })], 'errors');
  assert.equal(groups.length, 2); assert.equal(groups[0].count, 2);
});
