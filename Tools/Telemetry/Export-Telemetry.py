"""로컬 수집 기록을 중복 제거하고 엑셀에서 열 수 있는 통계·상세·오류 CSV로 내보냅니다."""
import argparse
import csv
import hashlib
import json
from collections import defaultdict
from pathlib import Path


def write_csv(path, rows):
    columns = sorted({key for row in rows for key in row})
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=columns)
        writer.writeheader()
        for row in rows:
            # 오류·이름 문자열이 엑셀 수식으로 실행되지 않게 합니다. 정수의 음수는 유지합니다.
            writer.writerow({key: "'" + value if isinstance(value, str)
                             and value.startswith(("=", "+", "-", "@", "\t", "\r")) else value
                             for key, value in row.items()})


def export(source, output, build=None, data_version=None):
    records, malformed = {}, 0
    for file in sorted(source.rglob("*.jsonl")):
        for line in file.read_text(encoding="utf-8-sig").splitlines():
            try:
                row = json.loads(line)
                if not isinstance(row["Fields"], dict) or not row["EventId"]:
                    raise ValueError("기록 구조 오류")
                if build is not None and row.get("BuildVersion") != build:
                    continue
                if data_version is not None and row.get("DataVersion") != data_version:
                    continue
                records.setdefault(row["EventId"], row)
            except (ValueError, KeyError, TypeError):
                malformed += 1
    details, errors, groups, error_groups = [], [], {}, {}
    dimensions = ("stage_id", "scene", "currency_id", "item_id", "monster_id", "level",
                  "junction_id", "route_id", "destination_stage_id")
    for row in records.values():
        flat = {key: value for key, value in row.items() if key != "Fields"}
        flat.update({"Field." + key: value for key, value in row["Fields"].items()})
        if row["Category"] == "errors":
            errors.append(flat)
            fields = row["Fields"]
            fingerprint = hashlib.sha256(json.dumps([fields.get("kind"), fields.get("message"),
                                                     fields.get("stack")], ensure_ascii=False).encode()).hexdigest()
            key = (row["BuildVersion"], row["DataVersion"], fingerprint)
            aggregate = error_groups.setdefault(key, {"BuildVersion": key[0], "DataVersion": key[1],
                "ErrorKey": fingerprint, "Count": 0, "Sessions": set(), "Message": fields.get("message", "")})
            aggregate["Count"] += 1
            aggregate["Sessions"].add(row["SessionId"])
            continue
        details.append(flat)
        group = {key: row.get(key, "") for key in ("EventName", "SchemaVersion", "BuildVersion", "BuildMode", "DataVersion")}
        group.update({key: row["Fields"].get(key, "") for key in dimensions})
        key = tuple(group.items())
        aggregate = groups.setdefault(key, dict(group, Count=0, TotalDelta=0, TotalQuantity=0, Sessions=set()))
        aggregate["Count"] += 1
        aggregate["Sessions"].add(row["SessionId"])
        aggregate["TotalDelta"] += row["Fields"].get("delta", 0)
        aggregate["TotalQuantity"] += row["Fields"].get("quantity", 0)
    for aggregate in list(groups.values()) + list(error_groups.values()):
        aggregate["SessionCount"] = len(aggregate.pop("Sessions"))
    output.mkdir(parents=True, exist_ok=True)
    write_csv(output / "statistics-summary.csv", list(groups.values()))
    write_csv(output / "statistics-details.csv", details)
    write_csv(output / "errors-summary.csv", list(error_groups.values()))
    write_csv(output / "errors-details.csv", errors)
    print(f"고유 기록 {len(records)}개 내보내기 완료. 손상·불완전 줄 {malformed}개 제외.")
    return len(records), malformed


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="수집 jsonl 파일이 들어 있는 폴더")
    parser.add_argument("output", type=Path, help="CSV 결과를 쓸 폴더")
    parser.add_argument("--build", help="조회할 빌드 버전")
    parser.add_argument("--data-version", help="조회할 데이터 버전")
    args = parser.parse_args()
    if not args.source.is_dir():
        parser.error("기록 원본 폴더가 없습니다.")
    export(args.source, args.output, args.build, args.data_version)
