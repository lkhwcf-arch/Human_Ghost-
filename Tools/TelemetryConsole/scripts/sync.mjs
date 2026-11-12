import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const policy = JSON.parse(fs.readFileSync(path.join(root, '../../Assets/StreamingAssets/Telemetry.json'), 'utf8'));
const { apiKey, projectId, authDomain, appId } = policy.firebase;
fs.mkdirSync(path.join(root, 'public'), { recursive: true });
fs.writeFileSync(path.join(root, 'public/firebase-config.json'), JSON.stringify({ apiKey, projectId, authDomain, appId }, null, 2));

// 수집 정책을 기준으로 규칙을 생성합니다. 관리자 접근과 데이터 형식은 클라이언트 설정과 분리해 강제합니다.
const q = JSON.stringify;
const legacyPath = path.join(root, 'firestore.legacy.rules');
const legacyRules = fs.existsSync(legacyPath) ? fs.readFileSync(legacyPath, 'utf8') : '';
function fieldCheck(name) {
  const field = `d.Fields[${q(name)}]`;
  const type = policy.fieldDefinitions[name]?.type;
  let check;
  if (type === 'string') check = `${field} is string && ${field}.size() <= 8192`;
  else if (type === 'boolean') check = `${field} is bool`;
  else if (['integer', 'positiveInteger', 'nonnegativeInteger'].includes(type)) {
    check = `${field} is int && ${field} >= ${type === 'integer' ? '-9007199254740991' : type === 'positiveInteger' ? '1' : '0'} && ${field} <= 9007199254740991`;
  } else throw new Error(`수집 필드 형식 정의가 없습니다: ${name}`);
  return `(!(${q(name)} in d.Fields) || (${check}))`;
}
const versions = [...Object.entries(policy.events), ...Object.entries(policy.legacyEvents ?? {})];
const checks = versions.map(([name, event]) => {
  if (!Number.isInteger(event.version) || event.version <= 0) throw new Error(`기록 버전 오류: ${name}`);
  return `(d.EventName == ${q(event.eventName ?? name)} && d.Category == ${q(event.category)} && d.SchemaVersion == ${event.version}
        && d.Fields.keys().hasOnly(${q(event.fields)})
        && ${event.fields.map(fieldCheck).join('\n        && ') || 'true'})`;
});
const rules = `rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    function analyst() {
      return request.auth != null
        && request.auth.token.firebase.sign_in_provider == 'google.com'
        && request.auth.token.email_verified == true
        && get(/databases/$(database)/documents/telemetryReaders/$(request.auth.uid)).data.enabled == true;
    }
    function validEvent(d, eventId) {
      return d.keys().hasAll(['EventId','EventName','Category','SchemaVersion','AnonymousId','SessionId','BuildVersion','BuildMode','DataVersion','TimestampMs','ElapsedMs','Fields','OwnerUid'])
        && d.keys().hasOnly(['EventId','EventName','Category','SchemaVersion','AnonymousId','SessionId','BuildVersion','BuildMode','DataVersion','TimestampMs','ElapsedMs','Fields','OwnerUid'])
        && d.EventId == eventId && eventId.matches('^[0-9a-f]{32}$')
        && d.OwnerUid == request.auth.uid
        && d.AnonymousId is string && d.AnonymousId.matches('^[0-9a-f]{32}$')
        && d.SessionId is string && d.SessionId.matches('^[0-9a-f]{32}$')
        && d.EventName is string && d.EventName.size() <= 80
        && d.BuildVersion is string && d.BuildVersion.size() <= 100
        && d.BuildMode in ['development','release']
        && d.DataVersion is string && d.DataVersion.size() <= 100
        && d.TimestampMs is int && d.TimestampMs > 0 && d.TimestampMs <= request.time.toMillis() + 300000
        && d.ElapsedMs is int && d.ElapsedMs >= 0 && d.ElapsedMs <= 9007199254740991
        && d.SchemaVersion is int && d.Fields is map
        && (${checks.join('\n          || ')});
    }
    match /telemetryEvents/{eventId} {
      allow create: if request.auth != null
        && request.auth.token.firebase.sign_in_provider == 'anonymous'
        && validEvent(request.resource.data, eventId);
      allow read: if analyst();
      allow update, delete: if false;
    }
    match /telemetryReaders/{uid} {
      allow get: if request.auth != null && request.auth.uid == uid
        && request.auth.token.firebase.sign_in_provider == 'google.com';
      allow list, write: if false;
    }
${legacyRules}
    match /{document=**} { allow read, write: if false; }
  }
}
`;
fs.writeFileSync(path.join(root, 'firestore.rules'), rules);
console.log('공개 웹 설정과 수집 정책 기반 Firestore 규칙을 생성했습니다. 기존 운영 규칙과 병합 후 배포하세요.');
