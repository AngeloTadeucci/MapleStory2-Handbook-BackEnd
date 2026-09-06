"""Read-only Plan 9 format survey. Uses only the Python standard library."""

import argparse
from collections import Counter
import json
from pathlib import Path
import struct


PLAN_FORMATS = {0x00010215, 0x00020436, 0x00030437, 0x00040108}


class Reader:
    def __init__(self, data):
        self.data = memoryview(data)
        self.offset = 0

    def take(self, size):
        if size < 0 or size > len(self.data) - self.offset:
            raise ValueError(f"Read outside buffer at {self.offset}, size {size}")
        result = self.data[self.offset:self.offset + size]
        self.offset += size
        return result

    def number(self, fmt="I"):
        return struct.unpack("<" + fmt, self.take(struct.calcsize("<" + fmt)))[0]

    def array(self, fmt, count):
        return [value[0] for value in struct.iter_unpack(
            "<" + fmt, self.take(struct.calcsize("<" + fmt) * count))]

    def string(self):
        return bytes(self.take(self.number())).decode("latin1")

    def finish(self):
        if self.offset != len(self.data):
            raise ValueError(f"{len(self.data) - self.offset} unconsumed bytes")


def scan_stream(data):
    reader = Reader(data)
    byte_count = reader.number()
    reader.number()  # Cloning behavior.
    region_values = reader.array("I", reader.number() * 2)
    formats = reader.array("I", reader.number())
    # Count is bits 16..23; bytes per component is bits 8..15.
    stride = sum(((value >> 16) & 255) * ((value >> 8) & 255) for value in formats)
    if not stride or byte_count % stride:
        raise ValueError(f"Invalid stream stride {stride} for {byte_count} bytes")
    for start, count in zip(region_values[::2], region_values[1::2]):
        if start + count > byte_count // stride:
            raise ValueError("Region exceeds stream element count")
    reader.take(byte_count)
    if reader.number("B") not in (0, 1):
        raise ValueError("Invalid streamable flag")
    reader.finish()
    return formats


def scan_file(data):
    reader = Reader(data)
    line_end = data.find(b"\n")
    if line_end < 0 or data[:line_end] != b"Gamebryo File Format, Version 30.2.0.3":
        raise ValueError("Unsupported NIF header")
    reader.take(line_end + 1)
    version, endian = reader.number(), reader.number("B")
    if version != 0x1E020003 or endian != 1:
        raise ValueError(f"Unsupported version/endian: {version:08x}/{endian}")
    reader.number()  # User version.
    block_count = reader.number()
    metadata_size = reader.number()
    reader.take(metadata_size)
    type_count = reader.number("H")
    types = [reader.string() for _ in range(type_count)]
    indices = reader.array("H", block_count)
    sizes = reader.array("I", block_count)
    string_count = reader.number()
    max_string_length = reader.number()
    for _ in range(string_count):
        if len(reader.string()) > max_string_length:
            raise ValueError("String exceeds declared maximum length")
    reader.array("I", reader.number())  # Groups.
    formats = Counter()
    block_types = Counter()
    stream_count = 0
    for block_id, (type_index, size) in enumerate(zip(indices, sizes)):
        type_index &= 0x7FFF
        if type_index >= len(types):
            raise ValueError(f"Invalid type index in block {block_id}")
        name = types[type_index]
        block = reader.take(size)
        block_types[name] += 1
        if name.split("\x01")[0] == "NiDataStream":
            try:
                formats.update(scan_stream(block))
            except ValueError as error:
                raise ValueError(f"Block {block_id} ({name!r}): {error}") from error
            stream_count += 1
    roots = reader.array("i", reader.number())
    if any(root < -1 or root >= block_count for root in roots):
        raise ValueError("Invalid footer root reference")
    reader.finish()
    return formats, block_types, stream_count, metadata_size


def survey(root):
    paths = sorted(root.rglob("*.nif"))
    formats, block_types, directories = Counter(), Counter(), Counter()
    examples, failures, metadata_files = {}, [], []
    stream_count = 0
    for path in paths:
        relative = path.relative_to(root).as_posix()
        directories[relative.split("/")[0]] += 1
        try:
            found, types, count, metadata_size = scan_file(path.read_bytes())
            formats.update(found)
            block_types.update(types)
            stream_count += count
            for value in found:
                examples.setdefault(value, relative)
            if metadata_size:
                metadata_files.append({"path": relative, "bytes": metadata_size})
        except (ValueError, OSError) as error:
            failures.append({"path": relative, "error": str(error)})
    unexpected = sorted(set(formats) - PLAN_FORMATS)
    return {
        "files": len(paths),
        "parsed_files": len(paths) - len(failures),
        "directories": dict(sorted(directories.items())),
        "streams": stream_count,
        "formats": [{"hex": f"0x{value:08X}", "occurrences": count,
                     "example": examples[value]} for value, count in sorted(formats.items())],
        "unexpected_formats": [f"0x{value:08X}" for value in unexpected],
        "encoded_block_type_count": len(block_types),
        "block_class_count": len({name.split("\x01")[0] for name in block_types}),
        "block_types": dict(sorted(block_types.items())),
        "metadata_files": metadata_files,
        "failures": failures,
        "plan_four_format_gate_passed": bool(paths) and not failures and not unexpected,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("models", type=Path)
    parser.add_argument("--output", type=Path, help="Write the JSON survey to this file")
    args = parser.parse_args()
    if not args.models.is_dir():
        parser.error(f"Model directory does not exist: {args.models}")
    result = survey(args.models)
    report = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.write_text(report, encoding="utf-8")
    else:
        print(report, end="")
    # 1 means malformed/no input; 2 means the plan's four-format assumption failed.
    return 1 if result["failures"] or not result["files"] else (
        0 if result["plan_four_format_gate_passed"] else 2)


if __name__ == "__main__":
    raise SystemExit(main())
