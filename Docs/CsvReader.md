# CSV 리더기

`listen` 프로젝트의 공통 CSV 리더기를 이식했습니다. 이 문서는 공통 CSV 입력 계층을 설명합니다. 게임 데이터 변환·저장소·초기화 연결은 [데이터 계층 인수인계 가이드](DataHandoff.md)를 참고합니다.

## 책임과 사용

- `Assets/3.Script/Common/Csv`: Unity에 의존하지 않는 파서, 로더, 읽기 전용 문서·행, 오류 정보와 입력 인터페이스.
- `Assets/3.Script/Data/Infrastructure`: 엄격한 UTF-8 파일 입력과 Unity TextAsset 입력 어댑터.

```csharp
using System.Threading;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Infrastructure;

// csvAsset은 Inspector 등에서 전달받은 TextAsset입니다.
// TextAsset 접근은 Unity 메인 스레드에서 시작합니다.
var loader = new CsvLoader(new UnityTextAssetCsvSource(csvAsset), new CsvParser());
CsvDocument document = await loader.LoadAsync(csvAsset.name, CancellationToken.None);
string value = document.Rows[0].Get("ID");
```

파일 입력은 `new CsvLoader(new Utf8CsvFileSource(), new CsvParser())`에 실제 파일 경로를 전달합니다. 메모리 문자열은 `new CsvParser().Read(text, sourceName)`으로 읽습니다. 로더와 파서는 호출자가 생성하고 소유하며 전역 상태나 자동 실행 컴포넌트는 없습니다.

## 입력 계약

첫 번째 비어 있지 않은 레코드가 헤더입니다. 헤더 앞뒤 공백은 제거하고 대소문자를 구분합니다. UTF-8 BOM, 인용된 쉼표·줄바꿈, 이중 따옴표 이스케이프를 지원합니다. 공백 줄은 무시하고 명시적인 빈 셀은 보존합니다. 값의 공백과 앞자리 0은 유지합니다.

빈/중복 헤더, 필드 수 불일치, 잘못된 따옴표는 `CsvParseException`으로 보고합니다. `Code`, `SourceName`, `LineNumber`, `FieldNumber`로 위치와 원인을 확인합니다. UTF-8 디코딩 오류와 파일 I/O 오류는 입력 어댑터에서 전달됩니다. 취소 토큰은 읽기 전후와 파싱 전후에 확인하며 파싱 도중 취소는 지원하지 않습니다.

## 이후 테이블 연결

개발용 파서 검사는 .NET 10 SDK에서 `dotnet run --project Tools/CsvReader.Checks/Check.csproj`로 실행합니다. 이 코드는 Assets 밖에 있으며 런타임 데이터 검사기가 아닙니다. Unity TextAsset 어댑터와 Unity 에디터 실행은 이 검사에 포함되지 않습니다.

클라이언트는 DataSheet 도구의 검증을 통과해 배포된 파일을 신뢰하고 읽습니다. 필수 값·범위·중복 ID·테이블 간 참조 등의 데이터 유효성 검사는 DataSheet에서 배포 전에 수행하며 클라이언트에 별도 검사기를 추가하지 않습니다. 클라이언트는 파싱·타입 변환·조회와 게임 기능 연결을 담당합니다. 파일 I/O·디코딩·CSV 문법 해석 실패는 읽기 오류로 전달합니다. 데이터 정의와 플레이 중 가변 상태를 분리합니다.

`ICsvTextSource`는 입력 방식을 교체하는 Strategy 역할을 하고, `CsvLoader`는 생성자로 입력과 파서를 주입받습니다. 읽은 결과는 공통 변환기와 읽기 전용 저장소로 연결하며, 전체 데이터 초기화 완료는 초기화 객체가 알립니다. 향후 네트워크 입력 어댑터를 붙일 수 있지만 서버 권한, 데이터 버전 일치, 동기화와 재현성은 별도 계약이 필요합니다. CSV 파싱은 초기화 시 수행하고 매 프레임 반복하지 않습니다.
