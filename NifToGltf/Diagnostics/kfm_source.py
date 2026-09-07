"""Read explicit KFM model/clip references without converting or changing assets."""
from pathlib import PurePosixPath
import hashlib
from scan_nif import Reader


def read_kfm(data):
    r = Reader(data)
    header = b';Gamebryo KFM File Version 30.2.0.3b\n'
    if bytes(r.take(len(header))) != header or r.number('B') != 1:
        raise ValueError('Expected little-endian KFM 30.2.0.3b')
    model, master = r.string(), r.string()
    r.array('i', 2)
    r.array('f', 2)
    clips = []
    for _ in range(r.number()):
        event, file, name = r.number('i'), r.string(), r.string()
        for _ in range(r.number()):
            r.number('i')
            if r.number() == 5:
                continue
            r.number('f')
            for _ in range(r.number()):
                r.number('i')
                r.string()
            if r.number() != 0:
                raise ValueError('Unsupported KFM transition text-key pairs')
        clips.append(dict(event=event, file=file, name=name))
    r.number('i')
    r.finish()
    return dict(model=model, master=master, clips=clips)


def beside(kfm, reference):
    relative = PurePosixPath(reference.replace('\\', '/'))
    if not reference or relative.is_absolute() or '..' in relative.parts or ':' in reference:
        raise ValueError('KFM reference must remain beside its source')
    return (PurePosixPath(kfm).parent / relative).as_posix()


def referenced_file(kfm, reference):
    relative = PurePosixPath(beside(kfm.name, reference))
    current = kfm.parent
    for part in relative.parts:
        matches = [p for p in current.iterdir() if p.name.lower() == part.lower()]
        if len(matches) != 1:
            raise ValueError(f'Missing or ambiguous KFM reference: {reference}')
        current = matches[0]
    if not current.is_file():
        raise ValueError(f'KFM reference is not a file: {reference}')
    return current


def animation_inputs(source, sources, resolved=None):
    kfm = sources / resolved['path'] if resolved else source.with_suffix('.kfm')
    if not kfm.exists():
        if resolved:
            raise ValueError(f'Resolved KFM is no longer extracted: {kfm}')
        return None, sorted(source.parent.glob(source.stem + '*.kf'))
    payload = kfm.read_bytes()
    if resolved and hashlib.sha256(payload).hexdigest() != resolved['sha256']:
        raise ValueError(f'Resolved KFM changed: {kfm}')
    record = read_kfm(payload)
    if referenced_file(kfm, record['model']).resolve() != source.resolve():
        raise ValueError(f'KFM model does not match selected source: {kfm}')
    return kfm, [referenced_file(kfm, clip['file']) for clip in record['clips']]
