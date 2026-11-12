// 실제 배포 연결 검사입니다. 실행하면 별도 버전의 session_start 검증 기록 하나를 생성합니다.
import fs from 'node:fs';
import crypto from 'node:crypto';

const config = JSON.parse(fs.readFileSync('public/firebase-config.json', 'utf8'));
const response = await fetch(`https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=${config.apiKey}`, {
  method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ returnSecureToken: true }),
});
if (!response.ok) throw new Error(`익명 인증 검사 실패: HTTP ${response.status}`);
const auth = await response.json();
const id = crypto.randomUUID().replaceAll('-', '');
const data = { EventId: id, EventName: 'session_start', Category: 'statistics', SchemaVersion: 1,
  AnonymousId: crypto.randomUUID().replaceAll('-', ''), SessionId: crypto.randomUUID().replaceAll('-', ''),
  BuildVersion: 'deployment-check-20261112', BuildMode: 'development', DataVersion: 'deployment-check',
  TimestampMs: Date.now(), ElapsedMs: 0, Fields: {}, OwnerUid: auth.localId };
function encode(object) {
  return Object.fromEntries(Object.entries(object).map(([key, value]) => [key, typeof value === 'object'
    ? { mapValue: { fields: encode(value) } } : typeof value === 'number' ? { integerValue: String(value) } : { stringValue: value }]));
}
const collection = `https://firestore.googleapis.com/v1/projects/${config.projectId}/databases/(default)/documents/telemetryEvents`;
const request = { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${auth.idToken}` },
  body: JSON.stringify({ fields: encode(data) }) };
const first = await fetch(`${collection}?documentId=${id}`, request);
if (!first.ok) throw new Error(`실서버 기록 생성 실패: HTTP ${first.status}`);
const second = await fetch(`${collection}?documentId=${id}`, request);
const read = await fetch(`${collection}/${id}`, { headers: { Authorization: `Bearer ${auth.idToken}` } });
if (second.status !== 409 || read.status !== 403) throw new Error(`중복·조회 차단 검사 실패: ${second.status}/${read.status}`);
fs.mkdirSync('.firebase', { recursive: true });
const result = { eventId: id, buildVersion: data.BuildVersion, anonymousAuthentication: true, creation: first.status,
  duplicate: second.status, anonymousReadDenied: read.status, checkedAt: new Date().toISOString() };
fs.writeFileSync('.firebase/live-smoke-result.json', JSON.stringify(result, null, 2));
console.log(JSON.stringify(result, null, 2));
