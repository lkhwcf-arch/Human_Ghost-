"""무손실 PNG 압축 계약 검사입니다. 외부 이미지 라이브러리를 사용하지 않습니다."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest
import zlib

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('png_optimizer', Path(__file__).with_name('Optimize-Png.py'))
optimizer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(optimizer)


def chunk(name, payload):
    return struct.pack('>I', len(payload))+name+payload+struct.pack('>I', zlib.crc32(name+payload) & 0xffffffff)


class CompressionChecks(unittest.TestCase):
    def test_pixels_and_metadata_stay_identical(self):
        raw = (b'\0'+b'\xff\0\0\xff'*32)*32
        image = b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR', struct.pack('>IIBBBBB', 32, 32, 8, 6, 0, 0, 0))
        image += chunk(b'tEXt', b'author\0sample')+chunk(b'IDAT', zlib.compress(raw, 0))+chunk(b'IEND', b'')
        result, _ = optimizer.recompress(image)
        self.assertLess(len(result), len(image))
        self.assertEqual(zlib.decompress(b''.join(payload for name, payload, _ in optimizer.chunks(result) if name == b'IDAT')), raw)
        self.assertEqual(optimizer.recompress(result)[0], result)

    def test_corrupt_crc_is_rejected(self):
        with self.assertRaises(ValueError):
            optimizer.chunks(b'\x89PNG\r\n\x1a\n'+chunk(b'IEND', b'')[:-1]+b'\xff')


if __name__ == '__main__':
    unittest.main()
