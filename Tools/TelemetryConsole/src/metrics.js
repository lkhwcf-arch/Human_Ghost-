// 계산과 표시를 분리합니다. 같은 집계 결과를 화면·그래프·엑셀에 사용합니다.
export const metrics = {
  deaths: { label: '사망 구간', event: 'player_death', dimensions: ['stage_id'], measure: 'count' },
  currency: { label: '혼·엽전 수급', event: 'currency_changed', dimensions: ['currency_id'], measure: 'gain' },
  levels: { label: '구간별 진입 레벨', event: 'stage_enter', dimensions: ['stage_id', 'level'], measure: 'count' },
  monsters: { label: '몬스터 처치', event: 'monster_defeated', dimensions: ['stage_id', 'monster_id'], measure: 'count' },
  acquired: { label: '아이템 획득', event: 'item_acquired', dimensions: ['item_id'], measure: 'quantity' },
  consumed: { label: '소비 아이템 순위', event: 'item_consumed', dimensions: ['item_id'], measure: 'quantity' },
  routes: { label: '갈림길 선택', event: 'route_selected', dimensions: ['junction_id', 'route_id', 'destination_stage_id'], measure: 'count' },
  progress: { label: '마지막 관측 구간', event: 'stage_enter', dimensions: ['stage_id'], measure: 'count' },
  errors: { label: '오류 현황', category: 'errors', dimensions: ['kind', 'message', 'stack'], measure: 'count' },
};

export function normalize(records) {
  const unique = new Map();
  let invalid = 0;
  for (const record of records) {
    if (!record || !['EventId', 'EventName', 'SessionId', 'AnonymousId', 'BuildVersion', 'BuildMode', 'DataVersion'].every(key => typeof record[key] === 'string')
        || !Number.isSafeInteger(record.TimestampMs) || typeof record.Fields !== 'object' || !record.Fields
        || Array.isArray(record.Fields) || !Object.values(record.Fields).every(value => value == null
          || typeof value === 'string' || typeof value === 'boolean' || Number.isSafeInteger(value))) { invalid++; continue; }
    if (!unique.has(record.EventId)) unique.set(record.EventId, record);
  }
  return { records: [...unique.values()], invalid };
}

export function filterRecords(records, { start, end, build = '', data = '', mode = '', version = '' }) {
  return records.filter(r => r.TimestampMs >= start && r.TimestampMs < end
    && (!build || r.BuildVersion === build) && (!data || r.DataVersion === data)
    && (!mode || r.BuildMode === mode) && (!version || String(r.SchemaVersion) === version));
}

const sessionKey = r => `${r.AnonymousId ?? ''}:${r.SessionId}`;
const number = value => Number.isSafeInteger(value) ? value : 0;

export function aggregate(records, metricId) {
  const definition = metrics[metricId];
  if (!definition) throw new Error('조회할 통계 항목이 없습니다.');
  let selected = records.filter(r => definition.category ? r.Category === definition.category : r.EventName === definition.event);
  if (metricId === 'progress') {
    const last = new Map();
    for (const r of selected) {
      const key = sessionKey(r), previous = last.get(key);
      if (!previous || r.TimestampMs > previous.TimestampMs
          || (r.TimestampMs === previous.TimestampMs && r.ElapsedMs > previous.ElapsedMs)) last.set(key, r);
    }
    selected = [...last.values()];
  }
  const ended = new Set(records.filter(r => r.EventName === 'session_end').map(sessionKey));
  const groups = new Map();
  for (const record of selected) {
    const dimensions = Object.fromEntries(definition.dimensions.map(key => [key, record.Fields[key] ?? '미제공']));
    const key = JSON.stringify(dimensions);
    let group = groups.get(key);
    if (!group) {
      group = { key, dimensions, count: 0, sessions: new Set(), gain: 0, spent: 0, net: 0,
        quantity: 0, missingEnd: 0, details: [] };
      groups.set(key, group);
    }
    group.count++;
    group.sessions.add(sessionKey(record));
    const delta = number(record.Fields.delta);
    group.gain += Math.max(0, delta); group.spent += Math.max(0, -delta); group.net += delta;
    group.quantity += number(record.Fields.quantity);
    if (!ended.has(sessionKey(record))) group.missingEnd++;
    group.details.push(record);
  }
  const result = [...groups.values()];
  for (const group of result) {
    group.sessionCount = group.sessions.size;
    if (metricId === 'deaths') {
      const entrants = new Set(records.filter(r => r.EventName === 'stage_enter'
        && r.Fields.stage_id === group.dimensions.stage_id).map(sessionKey));
      group.enteredSessions = entrants.size;
      const deathsInScope = [...group.sessions].filter(key => entrants.has(key)).length;
      group.rate = entrants.size ? deathsInScope / entrants.size * 100 : null;
      group.rateLabel = '진입 세션 중 사망 기록 비율';
    }
    if (metricId === 'routes') {
      const total = result.filter(other => other.dimensions.junction_id === group.dimensions.junction_id)
        .reduce((sum, other) => sum + other.count, 0);
      group.rate = total ? group.count / total * 100 : null;
      group.rateLabel = '갈림길 내 선택 횟수 비율';
    }
  }
  return result.sort((a, b) => b[definition.measure] - a[definition.measure] || a.key.localeCompare(b.key));
}

export const labels = {
  stage_id: '스테이지', currency_id: '재화', level: '진입 레벨', item_id: '아이템', monster_id: '몬스터',
  junction_id: '갈림길', route_id: '경로', destination_stage_id: '목적지', kind: '오류 종류', message: '오류 문구', stack: '호출 경로',
  count: '발생 횟수', sessionCount: '고유 세션 수', gain: '획득량', spent: '소비량', net: '순증감', quantity: '수량',
  rate: '비율(%)', enteredSessions: '진입 세션 수', missingEnd: '정상 종료 기록 없는 세션 수',
};

export function summaryRows(groups, metricId) {
  return groups.map(group => {
    const row = Object.fromEntries(Object.entries(group.dimensions).filter(([key]) => key !== 'stack')
      .map(([key, value]) => [labels[key] ?? key, value]));
    row['발생 횟수'] = group.count; row['고유 세션 수'] = group.sessionCount;
    if (metricId === 'currency') Object.assign(row, { '획득량': group.gain, '소비량': group.spent, '순증감': group.net });
    if (['acquired', 'consumed'].includes(metricId)) row['수량'] = group.quantity;
    if (group.rateLabel) { row[group.rateLabel + '(%)'] = group.rate == null ? '분모 미제공' : Number(group.rate.toFixed(2)); }
    if (metricId === 'deaths') row['진입 세션 수'] = group.enteredSessions;
    if (metricId === 'progress') row['정상 종료 기록 없는 세션 수'] = group.missingEnd;
    return row;
  });
}

export function detailRows(records) {
  return records.map(r => ({ '이벤트 ID': r.EventId, '항목': r.EventName, '설치 ID': r.AnonymousId,
    '세션 ID': r.SessionId, '발생 시각(UTC)': new Date(r.TimestampMs).toISOString(),
    '경과 시간(ms)': r.ElapsedMs, '빌드': r.BuildVersion, '데이터 버전': r.DataVersion,
    '빌드 종류': r.BuildMode, '형식 버전': r.SchemaVersion,
    ...Object.fromEntries(Object.entries(r.Fields).map(([key, value]) => [labels[key] ?? key, value])) }));
}
