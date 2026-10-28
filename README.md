# Human_Ghost-

지스타 출전을 준비하는 Unity 프로젝트입니다. 데이터 테이블은 별도 `DataSheet` 저장소에서 관리합니다.

## 협업 코드 규칙

프로그래머 공동 작업 시 다음 규칙을 적용합니다.

- 멤버 변수: `_camelCase` — 예: `private int _playerId;`
- 함수명: 대문자로 시작하는 `PascalCase` — 예: `LoadAsync()`, `TryGet()`

세부 작업 규칙은 [AGENTS.md](AGENTS.md)를 따릅니다.

## 현재 구현

`listen` 프로젝트에서 공통 CSV 파서·로더와 UTF-8 파일/TextAsset 어댑터를 이식했습니다. 사용 방법과 지원 범위는 [CSV 리더기](Docs/CsvReader.md)를 참고합니다. 게임 전용 테이블 모델과 검증 규칙은 아직 구성하지 않았습니다.
