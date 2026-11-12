"""PNG 내부 영상 데이터를 변경하지 않고 IDAT 압축만 개선합니다. 기본은 점검만 수행합니다."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import struct
import zlib


def chunks(data):
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('PNG 서명이 아닙니다.')
    offset, result = 8, []
    while offset < len(data):
        size = struct.unpack('>I', data[offset:offset+4])[0]
        block = data[offset:offset+size+12]
        if len(block) != size+12:
            raise ValueError('PNG 청크가 잘렸습니다.')
        name, payload = block[4:8], block[8:-4]
        if zlib.crc32(name+payload) & 0xffffffff != struct.unpack('>I', block[-4:])[0]:
            raise ValueError('PNG 청크 검사값이 다릅니다.')
        result.append((name, payload, block))
        offset += len(block)
    if not result or result[-1][0] != b'IEND':
        raise ValueError('PNG 종료 청크가 없습니다.')
    return result


def recompress(data):
    parts = chunks(data)
    if any(name == b'acTL' for name, _, _ in parts):
        return data, '움직이는 PNG 제외'
    compressed = b''.join(payload for name, payload, _ in parts if name == b'IDAT')
    decompressor = zlib.decompressobj()
    raw = decompressor.decompress(compressed, 256*1024*1024+1)
    if len(raw) > 256*1024*1024 or not decompressor.eof:
        return data, '해제 데이터 한도 초과·비정상 압축 제외'
    optimized = zlib.compress(raw, 9)
    if zlib.decompress(optimized) != raw:
        raise ValueError('압축 전후 영상 데이터가 다릅니다.')
    block = struct.pack('>I', len(optimized))+b'IDAT'+optimized+struct.pack('>I', zlib.crc32(b'IDAT'+optimized) & 0xffffffff)
    result, inserted = bytearray(data[:8]), False
    for name, _, original in parts:
        if name == b'IDAT':
            if not inserted:
                result.extend(block)
                inserted = True
        else:
            result.extend(original)
    if len(result) >= len(data):
        return data, '이미 압축된 파일 유지'
    # 필터·색상·투명도·메타데이터를 포함한 IDAT 밖 청크는 완전히 동일합니다.
    assert [(n, p) for n, p, _ in chunks(result) if n != b'IDAT'] == [(n, p) for n, p, _ in parts if n != b'IDAT']
    return bytes(result), hashlib.sha256(raw).hexdigest()


def run(root, limit, minimum, apply):
    result = []
    files = sorted((p for p in root.rglob('*.png') if p.stat().st_size >= minimum), key=lambda p: p.stat().st_size, reverse=True)[:limit]
    for path in files:
        before = path.read_bytes()
        after, check = recompress(before)
        changed = len(after) < len(before)
        if apply and changed:
            temporary = path.with_name(path.name+'.compression-tmp')
            try:
                temporary.write_bytes(after)
                os.replace(temporary, path)
            finally:
                if temporary.exists(): temporary.unlink()
        row = dict(path=path.as_posix(), before=len(before), after=len(after), saved=len(before)-len(after),
                   applied=apply and changed, verification=check)
        result.append(row)
        print(f'{path.name}: {len(before):,} → {len(after):,} 바이트', flush=True)
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    parser.add_argument('--limit', type=int, default=20)
    parser.add_argument('--minimum-bytes', type=int, default=4_000_000)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    if args.limit <= 0 or args.minimum_bytes < 0 or not args.root.is_dir():
        parser.error('폴더·대상 개수·최소 용량을 확인하세요.')
    results = run(args.root, args.limit, args.minimum_bytes, args.apply)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(results, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(f"합계 절감: {sum(r['saved'] for r in results):,} 바이트 / 실제 반영: {args.apply}")
