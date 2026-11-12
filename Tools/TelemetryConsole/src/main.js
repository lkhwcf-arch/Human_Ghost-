import './style.css';
import { initializeApp } from 'firebase/app';
import { getAuth, GoogleAuthProvider, signInWithPopup, signOut, onAuthStateChanged } from 'firebase/auth';
import { getFirestore, doc, getDoc, collection, query, where, orderBy, documentId, limit, startAfter, getDocs } from 'firebase/firestore/lite';
import { metrics, labels, normalize, filterRecords, aggregate, summaryRows, detailRows } from './metrics.js';

const $ = id => document.getElementById(id);
const pageSize = 500, maximum = 5000;
let db, auth, allowed = false, records = [], groups = [], selected = [], cursor = null;
let complete = false, source = '미조회', queryRange = null, generation = 0, busy = false, exporting = false, invalid = 0, readCount = 0;

function status(message, error = false) { $('status').textContent = message; $('status').classList.toggle('error', error); }
function dates() {
  const start = Date.parse($('from').value + 'T00:00:00+09:00');
  const end = Date.parse($('to').value + 'T00:00:00+09:00') + 86400000;
  if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start || end - start > 31 * 86400000)
    throw new Error('조회 기간은 시작일 이후 최대 31일로 지정해 주세요.');
  return { start, end };
}
function table(target, rows, clickable = false) {
  target.replaceChildren();
  if (!rows.length) { target.textContent = '해당 조건의 기록이 없습니다.'; return; }
  const columns = [...new Set(rows.flatMap(row => Object.keys(row)))];
  const element = document.createElement('table'), head = document.createElement('thead'), tr = document.createElement('tr');
  for (const key of columns) { const th = document.createElement('th'); th.textContent = key; tr.append(th); }
  head.append(tr); element.append(head);
  const body = document.createElement('tbody');
  rows.forEach((row, index) => {
    const item = document.createElement('tr');
    if (clickable) { item.dataset.index = index; item.tabIndex = 0; item.addEventListener('click', () => select(index));
      item.addEventListener('keydown', e => { if (e.key === 'Enter') select(index); }); }
    for (const key of columns) { const cell = document.createElement('td'); cell.textContent = row[key] == null ? '' : String(row[key]); item.append(cell); }
    body.append(item);
  });
  element.append(body); target.append(element);
}
function select(index) {
  selected = groups[index]?.details ?? [];
  $('detail-caption').textContent = `선택 항목 ${selected.length}개 중 처음 100개 표시 · 엑셀에는 전체 상세를 포함합니다.`;
  table($('details'), detailRows(selected.slice(0, 100)));
}
function filters() { return { ...dates(), build: $('build').value.trim(), data: $('data').value.trim(), mode: $('mode').value, version: $('version').value }; }
function render() {
  try {
    const f = filters(), metricId = $('metric').value;
    const filtered = filterRecords(records, f);
    const mixed = ['BuildVersion', 'DataVersion', 'SchemaVersion'].filter(key => new Set(filtered.map(r => r[key])).size > 1);
    groups = aggregate(filtered, metricId); selected = [];
    const matchesRange = queryRange && f.start >= queryRange.start && f.end <= queryRange.end;
    const full = complete && (source !== 'Firestore' || matchesRange);
    $('coverage').textContent = `${full ? '조회 범위 전체 집계' : '부분 집계 — 전체 통계가 아닙니다'} · ${source} · 읽은 문서 ${readCount.toLocaleString()}개 · 고유 기록 ${records.length.toLocaleString()}개 · 조건 일치 ${filtered.length.toLocaleString()}개 · 형식 제외 ${invalid}개`
      + (source === 'Firestore' ? ' · 조회마다 Firestore 읽기 할당량을 사용합니다.' : ' · 선택한 로컬 파일에 포함된 기록만 집계합니다.')
      + (mixed.length ? ' · 여러 빌드·데이터·형식 버전이 포함됐습니다. 비교 목적에 맞게 필터를 좁혀 주세요.' : '');
    $('export').disabled = !records.length || busy || exporting;
    $('more').disabled = busy || !allowed || complete || readCount >= maximum || source !== 'Firestore' || !matchesRange;
    table($('summary'), summaryRows(groups, metricId), true);
    $('details').replaceChildren(); $('detail-caption').textContent = '요약 항목을 선택하세요.';
    $('cards').replaceChildren();
    const cards = [['집계 그룹', groups.length], ['발생 기록', groups.reduce((sum, group) => sum + group.count, 0)],
      ['고유 세션', new Set(groups.flatMap(group => group.details.map(r => `${r.AnonymousId}:${r.SessionId}`))).size]];
    for (const [label, value] of cards) {
      const item = document.createElement('div'); item.className = 'card'; item.textContent = label;
      const number = document.createElement('strong'); number.textContent = value.toLocaleString(); item.append(number); $('cards').append(item);
    }
    $('chart').replaceChildren();
    const measure = metrics[metricId].measure;
    const max = Math.max(1, ...groups.slice(0, 10).map(group => group[measure]));
    groups.slice(0, 10).forEach((group, index) => {
      const bar = document.createElement('button'); bar.className = 'bar';
      const title = document.createElement('span'); title.textContent = Object.entries(group.dimensions).filter(([key]) => key !== 'stack')
        .map(([key, value]) => `${labels[key] ?? key} ${String(value).slice(0, 80)}`).join(' · ');
      const value = document.createElement('b'); value.textContent = group[measure].toLocaleString(); title.append(value);
      const width = document.createElement('i'); width.style.width = `${group[measure] / max * 100}%`;
      bar.append(title, width); bar.addEventListener('click', () => select(index)); $('chart').append(bar);
    });
  } catch (error) { status(error.message, true); }
}
async function load(more = false) {
  if (!allowed || busy) return;
  const mine = ++generation;
  busy = true; $('query').disabled = true; $('more').disabled = true;
  try {
    if (!more) { queryRange = dates(); records = []; cursor = null; complete = false; invalid = 0; readCount = 0; source = 'Firestore'; }
    const constraints = [where('TimestampMs', '>=', queryRange.start), where('TimestampMs', '<', queryRange.end), orderBy('TimestampMs'), orderBy(documentId())];
    if (cursor) constraints.push(startAfter(cursor));
    constraints.push(limit(Math.min(pageSize, maximum - readCount)));
    status('Firestore에서 기록을 읽고 집계하고 있습니다.');
    const snapshot = await getDocs(query(collection(db, 'telemetryEvents'), ...constraints));
    if (mine !== generation || !allowed) return;
    readCount += snapshot.size;
    const cleaned = normalize([...records, ...snapshot.docs.map(entry => entry.data())]);
    records = cleaned.records; invalid += cleaned.invalid;
    cursor = snapshot.docs.at(-1) ?? cursor;
    complete = snapshot.size < pageSize;
    status(complete ? '조회 범위의 집계를 완료했습니다.' : readCount >= maximum
      ? '한 번의 조회 한도 5,000개에 도달했습니다. 기간을 줄여 전체 집계를 확인하세요.' : '추가 기록이 있습니다. 전체 집계가 필요하면 다음 기록을 읽어 주세요.');
  } catch (error) { if (mine === generation) status(`조회 실패: ${error.code ?? error.message}`, true); }
  finally { busy = false; $('query').disabled = !allowed; render(); }
}

async function download() {
  if (!records.length || exporting) return;
  exporting = true;
  $('export').disabled = true;
  try {
    const f = filters(), metricId = $('metric').value, filtered = filterRecords(records, f);
    const mine = generation, filterKey = JSON.stringify(f), from = $('from').value, to = $('to').value;
    const isCurrent = () => {
      if (mine !== generation || metricId !== $('metric').value) return false;
      try { return JSON.stringify(filters()) === filterKey; } catch { return false; }
    };
    const current = aggregate(filtered, metricId);
    const metadata = [{ '통계': metrics[metricId].label, '시작일(한국)': from, '종료일(한국)': to,
      '자료': source, '전체 집계': complete && (source !== 'Firestore' || (f.start >= queryRange.start && f.end <= queryRange.end)) ? '예' : '아니오(부분 집계)',
      '빌드': f.build || '전체', '데이터 버전': f.data || '전체', '빌드 종류': f.mode || '전체',
      '형식 버전': f.version || '전체', '읽은 문서': readCount, '읽은 고유 기록': records.length, '제외된 형식': invalid,
      '포함 빌드': [...new Set(filtered.map(r => r.BuildVersion))].join(', '),
      '포함 데이터 버전': [...new Set(filtered.map(r => r.DataVersion))].join(', '),
      '포함 형식 버전': [...new Set(filtered.map(r => r.SchemaVersion))].join(', '),
      '비율 기준': metricId === 'routes' ? '갈림길 내 선택 횟수 비율' : metricId === 'deaths' ? '진입 세션 중 사망 기록 비율' : '',
      '주의': '원시 관측 기록 기준이며 강제 종료·로그 없는 버그·미연결 이벤트는 확정하지 않습니다.' }];
    const { createWorkbook } = await import('./workbook.js');
    if (!isCurrent()) return;
    const workbook = createWorkbook(metadata, summaryRows(current, metricId), detailRows(current.flatMap(group => group.details)));
    const buffer = await workbook.xlsx.writeBuffer();
    // 로그아웃·자료 교체·조건 변경 뒤 이전 작업이 파일을 내보내지 않게 합니다.
    if (!isCurrent()) return;
    const url = URL.createObjectURL(new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }));
    const link = document.createElement('a'); link.href = url; link.download = `인귀_${metrics[metricId].label}_${from}_${to}.xlsx`; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  } catch (error) { status('엑셀 생성 실패: ' + error.message, true); }
  finally { exporting = false; render(); }
}

for (const [key, metric] of Object.entries(metrics)) { const option = document.createElement('option'); option.value = key; option.textContent = metric.label; $('metric').append(option); }
const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Seoul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
$('from').value = today; $('to').value = today;
for (const id of ['metric', 'build', 'data', 'mode', 'version', 'from', 'to']) $(id).addEventListener('change', render);
$('query').addEventListener('click', () => load(false)); $('more').addEventListener('click', () => load(true));
$('export').addEventListener('click', download);
$('local').addEventListener('change', async event => {
  const mine = ++generation;
  try {
    const raw = []; invalid = 0;
    for (const file of event.target.files) for (const line of (await file.text()).split(/\r?\n/).filter(Boolean)) {
      try { raw.push(JSON.parse(line)); } catch { invalid++; }
    }
    if (mine !== generation) return;
    const cleaned = normalize(raw); records = cleaned.records; invalid += cleaned.invalid; readCount = raw.length;
    complete = true; source = '로컬 파일'; cursor = null; queryRange = null;
    status('로컬 기록을 읽었습니다. 날짜 조건을 기록 기간에 맞춰 주세요.'); render();
  } catch (error) { status('파일 읽기 실패: ' + error.message, true); }
});
try {
  const response = await fetch('/firebase-config.json', { cache: 'no-store' });
  if (!response.ok) throw new Error('공개 Firebase 설정이 없습니다. prepare-config를 실행하세요.');
  const config = await response.json();
  const app = initializeApp(config); auth = getAuth(app); db = getFirestore(app);
  $('login').addEventListener('click', async () => {
    try { await signInWithPopup(auth, new GoogleAuthProvider()); } catch (error) { status('로그인 실패: ' + error.code, true); }
  });
  $('logout').addEventListener('click', async () => { await signOut(auth); });
  onAuthStateChanged(auth, async user => {
    const mine = ++generation;
    allowed = false; records = []; groups = []; complete = false; source = '미조회';
    $('query').disabled = true; $('logout').hidden = !user; $('login').hidden = !!user;
    $('account').textContent = user?.email ?? '로그인 전'; render();
    if (!user) { status('Google 로그인 또는 로컬 파일로 통계를 확인하세요.'); return; }
    try {
      const reader = await getDoc(doc(db, 'telemetryReaders', user.uid));
      if (mine !== generation) return;
      allowed = user.emailVerified && reader.exists() && reader.data().enabled === true;
      $('query').disabled = !allowed;
      status(allowed ? '조회 권한을 확인했습니다. 기간을 선택하고 조회를 눌러 주세요.'
        : `조회 권한이 없습니다. 관리자에게 Firebase 사용자 UID ${user.uid}를 전달하세요.`, !allowed);
    } catch (error) { if (mine === generation) status('조회 권한 확인 실패: ' + error.code, true); }
  });
} catch (error) { status(error.message + ' 로컬 파일 집계는 사용할 수 있습니다.', true); }
