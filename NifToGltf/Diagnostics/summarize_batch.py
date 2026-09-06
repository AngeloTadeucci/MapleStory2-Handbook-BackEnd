"""Keep a portable summary of a native batch and its glTF validation report."""

import argparse
from collections import Counter
import json
from pathlib import Path
import re


def category(error):
    match = re.search(r'Referenced scene block \d+ \(([^)]+)\)', error)
    if match:
        return match[1]
    for text, name in [
        ('expected one match under', 'Missing or ambiguous external texture'),
        ('missing beside model', 'Missing external texture'),
        ('is ambiguous across', 'Ambiguous external texture'),
        ('uses scene effect block', 'Visible scene effect material'),
        ('Embedded texture', 'Embedded texture'),
        ('texture transform', 'UV texture transform'),
        ('primitive type', 'Unsupported primitive type'),
        ('NiSkinningMeshModifier reference', 'Unsupported mesh modifier'),
        ('BONE_PALETTE', 'Missing bone palette'),
        ('No meshes', 'No meshes'),
        ('Zero tangent', 'Zero tangent'),
        ('Zero-length normal', 'Zero-length normal'),
        ('DDS format', 'Unsupported DDS format'),
        ('Non-finite float', 'Non-finite transform')
    ]:
        if text.lower() in error.lower():
            return name
    return 'Other'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('batch', type=Path)
    parser.add_argument('validation', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    batch = json.loads(args.batch.read_text(encoding='utf-8'))
    validation = json.loads(args.validation.read_text(encoding='utf-8'))
    failed = [{'input': f['input'].replace('\\', '/'), 'category': category(f['error']),
               'error': f['error'].replace(batch['input'] + '\\', '').replace('\\', '/')}
              for f in batch['failed']]
    warnings = [{'input': Path(f['path']).relative_to(args.batch.parent).as_posix(),
                 'issues': f['issues']} for f in validation['bad']]
    result = {'mode': batch['mode'], 'total': batch['total'],
              'converted': batch['convertedCount'], 'rejected': batch['failedCount'],
              'effect_excluded': batch.get('excludedCount', 0), 'missing': batch.get('missingCount', 0),
              'effect_exclusions': batch.get('excluded', []), 'missing_assets': batch.get('missing', []),
              'validated': validation['total'], 'invalid': validation['invalid'],
              'warning_files': validation['warningFiles'],
              'rejection_categories': dict(sorted(Counter(f['category'] for f in failed).items())),
              'validation_issues': warnings, 'rejections': failed}
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result['rejection_categories'], indent=2))
