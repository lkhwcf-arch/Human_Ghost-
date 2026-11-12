// 공식 Firebase CLI의 로그인 문맥을 재사용해 배포 전 상태만 조회합니다. 토큰은 출력·저장하지 않습니다.
const fs = require('node:fs');
const path = require('node:path');

async function main() {
  const cliRoot = process.env.FIREBASE_CLI_ROOT;
  if (!cliRoot) throw new Error('FIREBASE_CLI_ROOT에 설치된 공식 CLI 경로를 지정하세요.');
  const { getGlobalDefaultAccount } = require(path.join(cliRoot, 'lib/auth.js'));
  const { requireAuth } = require(path.join(cliRoot, 'lib/requireAuth.js'));
  const { Client } = require(path.join(cliRoot, 'lib/apiv2.js'));
  const rules = require(path.join(cliRoot, 'lib/gcp/rules.js'));
  const hosting = require(path.join(cliRoot, 'lib/hosting/api.js'));
  const account = getGlobalDefaultAccount();
  if (!account) throw new Error('CLI 로그인 정보가 없습니다.');
  const projectId = 'humanghost-f85de';
  await requireAuth({ project: projectId, user: account.user, tokens: account.tokens });
  const rulesetName = await rules.getLatestRulesetName(projectId, 'cloud.firestore');
  const files = rulesetName ? await rules.getRulesetContent(rulesetName) : [];
  const versions = await hosting.listVersions(projectId);
  const admin = new Client({ urlPrefix: 'https://identitytoolkit.googleapis.com', apiVersion: 'admin/v2' });
  const authConfig = (await admin.get(`/projects/${projectId}/config`, { skipLog: { resBody: true } })).body;
  let googleEnabled = null, googleInspectionError = null;
  try {
    googleEnabled = (await admin.get(`/projects/${projectId}/defaultSupportedIdpConfigs/google.com`, { skipLog: { resBody: true } })).body.enabled === true;
  } catch (error) { googleInspectionError = error.message; }
  const usersApi = new Client({ urlPrefix: 'https://identitytoolkit.googleapis.com', apiVersion: 'v1' });
  const users = (await usersApi.post(`/projects/${projectId}/accounts:lookup`, { email: [account.user.email] }, { skipLog: { body: true, resBody: true } })).body.users ?? [];
  const viewer = users.find(user => user.providerUserInfo?.some(provider => provider.providerId === 'google.com'));
  const snapshot = { projectId, rulesetName, files, hostingVersions: versions.map(version => ({ name: version.name, status: version.status })),
    anonymousEnabled: authConfig.signIn?.anonymous?.enabled === true, googleEnabled, googleInspectionError, authorizedDomains: authConfig.authorizedDomains,
    currentAccountViewerUid: viewer?.localId ?? null };
  fs.mkdirSync('.firebase', { recursive: true });
  const destination = fs.existsSync('.firebase/deployment-before.json') ? '.firebase/deployment-inspection-latest.json' : '.firebase/deployment-before.json';
  fs.writeFileSync(destination, JSON.stringify(snapshot, null, 2));
  console.log(JSON.stringify(process.argv.includes('--summary') ? { projectId, rulesetName,
    hostingVersions: versions.length, anonymousEnabled: snapshot.anonymousEnabled, googleEnabled, googleInspectionError,
    currentAccountViewerUid: snapshot.currentAccountViewerUid } : snapshot, null, 2));
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
