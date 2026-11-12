import fs from 'node:fs';
import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { initializeTestEnvironment, assertSucceeds, assertFails } from '@firebase/rules-unit-testing';
import { doc, setDoc, getDoc, collection, getDocs } from 'firebase/firestore';

const projectId = 'demo-humanghost';
let environment;
const record = (id = '1'.repeat(32)) => ({ EventId: id, EventName: 'player_death', Category: 'statistics', SchemaVersion: 1,
  AnonymousId: 'a'.repeat(32), SessionId: 'b'.repeat(32), BuildVersion: 'test', BuildMode: 'development',
  DataVersion: 'data', TimestampMs: Date.now(), ElapsedMs: 1, OwnerUid: 'writer', Fields: { stage_id: 8002, level: 1 } });

before(async () => {
  environment = await initializeTestEnvironment({ projectId, firestore: { rules: fs.readFileSync('firestore.rules', 'utf8') } });
  await environment.withSecurityRulesDisabled(async context => {
    await setDoc(doc(context.firestore(), 'telemetryReaders', 'analyst'), { enabled: true });
  });
});
after(async () => { await environment?.cleanup(); });
const writer = () => environment.authenticatedContext('writer', { firebase: { sign_in_provider: 'anonymous' } }).firestore();
const analyst = (uid = 'analyst') => environment.authenticatedContext(uid,
  { firebase: { sign_in_provider: 'google.com' }, email_verified: true }).firestore();

test('익명 작성자는 유효한 기록을 생성할 수 있으나 조회·수정·권한 추가는 불가', async () => {
  const r = record();
  await assertSucceeds(setDoc(doc(writer(), 'telemetryEvents', r.EventId), r));
  await assertFails(getDoc(doc(writer(), 'telemetryEvents', r.EventId)));
  await assertFails(setDoc(doc(writer(), 'telemetryEvents', r.EventId), { ...r, Fields: { level: 2 } }));
  await assertFails(setDoc(doc(writer(), 'telemetryReaders', 'writer'), { enabled: true }));
});
test('허용한 Google 계정만 전체 기록 조회 가능', async () => {
  await assertSucceeds(getDocs(collection(analyst(), 'telemetryEvents')));
  await assertFails(getDocs(collection(analyst('outsider'), 'telemetryEvents')));
  await assertFails(getDocs(collection(environment.unauthenticatedContext().firestore(), 'telemetryEvents')));
});
test('기존 game_logs의 작성 허용·조회 금지 계약도 보존', async () => {
  await assertSucceeds(setDoc(doc(writer(), 'game_logs', 'legacy-example'), { userId: 'writer', stage: 'example', score: 1, playTime: 1.5 }));
  await assertFails(getDoc(doc(writer(), 'game_logs', 'legacy-example')));
  await assertFails(getDoc(doc(analyst(), 'game_logs', 'legacy-example')));
});
test('소유 UID 위조·미등록 필드·실수 레벨·미래 시각을 거부', async () => {
  let index = 3;
  for (const change of [{ OwnerUid: 'other' }, { Fields: { arbitrary: 'wrong' } }, { Fields: { level: 1.5 } }, { TimestampMs: Date.now() + 3600000 }]) {
    const r = { ...record(String(index++).repeat(32)), ...change };
    await assertFails(setDoc(doc(writer(), 'telemetryEvents', r.EventId), r));
  }
});
test('Firestore REST 생성 재시도는 기존 문서를 덮어쓰지 않고 이미 존재함을 확인', async () => {
  const id = '8'.repeat(32), r = record(id);
  const encode = object => Object.fromEntries(Object.entries(object).map(([key, value]) => [key,
    typeof value === 'object' ? { mapValue: { fields: encode(value) } } : typeof value === 'number'
      ? { integerValue: String(value) } : { stringValue: value }]));
  // 에뮬레이터에서만 허용되는 서명 없는 검사용 토큰입니다. 운영에서는 사용하지 않습니다.
  const part = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const now = Math.floor(Date.now() / 1000);
  const token = part({ alg: 'none', typ: 'JWT' }) + '.' + part({ sub: 'writer', user_id: 'writer', aud: projectId,
    iss: `https://securetoken.google.com/${projectId}`, iat: now, exp: now + 3600, auth_time: now,
    firebase: { sign_in_provider: 'anonymous', identities: {} } }) + '.';
  const url = `http://${process.env.FIRESTORE_EMULATOR_HOST}/v1/projects/${projectId}/databases/(default)/documents/telemetryEvents?documentId=${id}`;
  const request = { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token }, body: JSON.stringify({ fields: encode(r) }) };
  const first = await fetch(url, request);
  assert.equal(first.status, 200, await first.text());
  const second = await fetch(url, request);
  assert.equal(second.status, 409, await second.text());
});
