import struct
import tempfile
import unittest
from pathlib import Path

from scan_nif import scan_file, scan_stream, survey
from inspect_nif import read_document


def u32(*values):
    return struct.pack("<" + "I" * len(values), *values)


def sized(value):
    return u32(len(value)) + value


def stream(component=0x00030437, payload=bytes(12)):
    return u32(len(payload), 0, 1, 0, 1, 1, component) + payload + b"\x01"


def nif(block=None, metadata=b"", type_index=0, footer=None):
    block = stream() if block is None else block
    return (b"Gamebryo File Format, Version 30.2.0.3\n" + u32(0x1E020003)
            + b"\x01" + u32(0, 1) + sized(metadata) + b"\x01\x00"
            + sized(b"NiDataStream\x011\x0118") + struct.pack("<H", type_index)
            + u32(len(block), 0, 0, 0) + block
            + (u32(1, 0) if footer is None else footer))


class ScanTests(unittest.TestCase):
    def test_inspection_accepts_both_supported_headers_and_rejects_mismatched_binary_version(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            data = nif().replace(b'30.2.0.3', b'30.1.0.3').replace(u32(0x1E020003), u32(0x1E010003))
            (root / 'clip.kf').write_bytes(data)
            _, blocks, roots = read_document(root / 'clip.kf')
            self.assertEqual(roots, [0])
            self.assertEqual(blocks[0][0], 'NiDataStream\x011\x0118')
            (root / 'model.nif').write_bytes(data)
            self.assertEqual(read_document(root / 'model.nif')[2], [0])
            (root / 'model.nif').write_bytes(data.replace(u32(0x1E010003), u32(0x1E020003)))
            with self.assertRaisesRegex(ValueError, 'version/endian'):
                read_document(root / 'model.nif')

    def test_nonempty_metadata_and_masked_type_index(self):
        formats, _, count, metadata = scan_file(nif(metadata=b"abc", type_index=0x8000))
        self.assertEqual(dict(formats), {0x00030437: 1})
        self.assertEqual((count, metadata), (1, 3))

    def test_additional_formats_fail_plan_gate_without_parse_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "sample.nif").write_bytes(nif(stream(0x00010425, bytes(4))))
            report = survey(path)
            self.assertEqual(report["parsed_files"], 1)
            self.assertEqual(report["unexpected_formats"], ["0x00010425"])
            self.assertFalse(report["plan_four_format_gate_passed"])

    def test_stream_bounds_stride_and_trailer(self):
        invalid = [stream()[:-1], stream() + b"\x00", stream(payload=bytes(11)),
                   stream()[:-1] + b"\x02"]
        for data in invalid:
            with self.subTest(data=data), self.assertRaises(ValueError):
                scan_stream(data)

    def test_region_outside_payload(self):
        data = bytearray(stream())
        struct.pack_into("<I", data, 16, 2)
        with self.assertRaisesRegex(ValueError, "Region"):
            scan_stream(data)

    def test_footer_and_block_bounds(self):
        for data in (nif()[:-1], nif() + b"\x00", nif(footer=u32(1, 2)),
                     nif(type_index=1)):
            with self.subTest(data=data), self.assertRaises(ValueError):
                scan_file(data)

    def test_empty_survey_cannot_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            self.assertFalse(survey(Path(directory))["plan_four_format_gate_passed"])


if __name__ == "__main__":
    unittest.main()
