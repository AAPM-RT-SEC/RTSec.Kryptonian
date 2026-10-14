"""Read the controlled Markdown registers without guessing implementation coverage."""
from pathlib import Path
import re

BASE = Path(__file__).resolve().parents[1]
SR_PATTERN = r'SR-[A-Z-]+\.\d{3}'
SOURCES = ['README.md', '00-SPEC-CONVENTIONS.md', '01-PRODUCT-DEFINITION.md',
           '02-USER-REQUIREMENTS.md', '03-SOFTWARE-REQUIREMENTS.md',
           '04-SOFTWARE-DESIGN.md', '05-INTERFACE-CONTROL.md', '06-CYBERSECURITY.md',
           '07-OPERATIONAL-RISK.md', '08-VERIFICATION-VALIDATION.md',
           '09-TRACEABILITY.md', '10-GAP-ANALYSIS.md', '11-STANDARDS-CONFORMANCE.md',
           '12-ENGINEERING-HANDOFF.md', 'REVIEW-FINDINGS.md',
           '13-CODE-DOCUMENT-RECONCILIATION.md', 'FINAL-SUMMARY.md']

def read(name):
    return (BASE / name).read_text(encoding='utf-8')

def sr_records():
    s = read('03-SOFTWARE-REQUIREMENTS.md')
    result = {}
    for m in re.finditer(rf'(?ms)^### ({SR_PATTERN})[^\n]*\n(.*?)(?=^### SR-|^## |\Z)', s):
        ident, body = m.groups()
        if ident in result:
            raise ValueError(f'Duplicate requirement {ident}')
        result[ident] = dict(re.findall(r'(?m)^- \*\*([^*]+):\*\* (.*)$', body))
    return result

def vt_records():
    s = read('08-VERIFICATION-VALIDATION.md')
    result = {}
    for m in re.finditer(r'(?ms)^#### (VT-\d{3})[^\n]*\n(.*?)(?=^#### VT-|^## |\Z)', s):
        ident, body = m.groups()
        if ident in result:
            raise ValueError(f'Duplicate test {ident}')
        fields = dict(re.findall(r'(?m)^\| ([^|]+?) \| ([^\n]+?) \|$', body))
        fields['sr_ids'] = sorted(set(re.findall(SR_PATTERN, fields.get('Linked requirements', ''))))
        result[ident] = fields
    return result

def vt_links():
    links = {ident: [] for ident in sr_records()}
    for vt, record in vt_records().items():
        for ident in record['sr_ids']:
            if ident not in links:
                raise ValueError(f'{vt} references unallocated {ident}')
            links[ident].append(vt)
    return links

def table_rows(text):
    for line in text.splitlines():
        if line.startswith('| '):
            yield [v.strip() for v in re.split(r'(?<!\\)\|', line.strip('|'))]
