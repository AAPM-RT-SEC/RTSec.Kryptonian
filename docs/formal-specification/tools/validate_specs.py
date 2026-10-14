"""Validate publication-critical register, field and traceability integrity."""
from collections import Counter
from datetime import datetime, timezone
import json
import re
import xml.etree.ElementTree as ET
from spec_model import BASE, SOURCES, SR_PATTERN, read, sr_records, vt_records, vt_links, table_rows

def validate():
    errors = []
    srs, tests, links = sr_records(), vt_records(), vt_links()
    required = ['ID', 'Requirement', 'Rationale', 'Source', 'Classification', 'Priority',
                'Status', 'Verification', 'Evidence', 'Approval']
    for ident, fields in srs.items():
        for key in required:
            if not fields.get(key): errors.append(f'{ident} lacks {key}')
        if fields.get('ID') != ident: errors.append(f'{ident} record ID mismatch')
        if fields.get('Approval') != 'Review Required': errors.append(f'{ident} unexpected approval')
    registry = {r[0]:r for r in table_rows(read('03-SOFTWARE-REQUIREMENTS.md'))
                if len(r)==5 and re.fullmatch(SR_PATTERN, r[0])}
    if set(registry)!=set(srs): errors.append('Requirement register/body set mismatch')
    for ident, row in registry.items():
        if row[2:] != [srs[ident].get(x) for x in ['Priority', 'Status', 'Verification']]:
            errors.append(f'{ident} canonical register fields mismatch')
    trace = {r[0]:r for r in table_rows(read('09-TRACEABILITY.md'))
             if len(r)==9 and re.fullmatch(SR_PATTERN, r[0])}
    if set(trace)!=set(srs): errors.append('Forward traceability/body set mismatch')
    for ident, row in trace.items():
        if row[8].strip('*') != srs[ident]['Status']: errors.append(f'{ident} trace status mismatch')
        if set(re.findall(r'VT-\d{3}',row[5])) != set(links[ident]):
            errors.append(f'{ident} forward/reverse VT edge mismatch')
    users={r[0]:r for r in table_rows(read('02-USER-REQUIREMENTS.md'))
           if len(r)==9 and re.fullmatch(r'UR-\d{3}',r[0])}
    usertrace={r[0]:r for r in table_rows(read('09-TRACEABILITY.md'))
               if len(r)==6 and re.fullmatch(r'UR-\d{3}',r[0])}
    if len(users)!=38 or set(users)!=set(usertrace): errors.append('UR register/trace set mismatch')
    for ident,row in usertrace.items():
        if row[4].strip('*')!=users[ident][5].strip('*'): errors.append(f'{ident} user trace status mismatch')
    vt_fields = ['Test ID', 'Linked requirements', 'Preconditions', 'Test inputs', 'Procedure',
                 'Expected results', 'Acceptance criteria', 'Evidence status']
    for ident, fields in tests.items():
        for key in vt_fields:
            if not fields.get(key): errors.append(f'{ident} lacks {key}')
    if len(srs)!=154: errors.append(f'Expected 154 requirements; found {len(srs)}')
    if set(tests)!={f'VT-{i:03}' for i in range(1,61)}: errors.append('VT allocation is not 001-060')
    for name in SOURCES:
        text=read(name)
        if text.startswith('\ufeff'): errors.append(f'{name} starts with a BOM that hides its Markdown title')
        header=text.split('\n\n',2)[1]
        for key in ['Document ID', 'Version', 'Author', 'Reviewer', 'Approver', 'Source references', 'Revision history']:
            if f'| {key} |' not in header: errors.append(f'{name} control header lacks {key}')
        if name!='REVIEW-FINDINGS.md' and any(x in text for x in ['â†', 'Â§', 'â€']):
            errors.append(f'{name} has mojibake')
    # Track fresh tests by result assembly, so repeated API runs do not inflate totals.
    assemblies={}
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    for p in sorted((BASE/'validation/test-results').glob('*.trx')):
        tree=ET.parse(p); counters=tree.find('.//t:Counters',ns)
        methods=tree.findall('.//t:TestMethod',ns)
        if counters is None or not methods: continue
        assembly=methods[0].get('codeBase','').replace('\\','/').split('/')[-1]
        assemblies[assembly]={'file':p.name,'total':int(counters.get('total','0')),
            'passed':int(counters.get('passed','0')),'failed':int(counters.get('failed','0')),
            'skipped':int(counters.get('total','0'))-int(counters.get('executed','0'))}
    report={'checked_at_utc':datetime.now(timezone.utc).isoformat(),
            'application_baseline':'682f996c8447afec13bc1fce5b7f59e6a06efa25',
            'source_documents':len(SOURCES),'requirement_count':len(srs),
            'user_requirement_status':dict(Counter(r[5] for r in users.values())),
            'requirement_status':dict(Counter(x['Status'] for x in srs.values())),
            'verification_cases':len(tests),
            'vt_evidence':dict(Counter(x['Evidence status'].split(' — ')[0].strip() for x in tests.values())),
            'vt_linked_requirements':sum(bool(x) for x in links.values()),
            'without_vt_link':[k for k,v in links.items() if not v],
            'fresh_tests':assemblies,'errors':errors}
    report['fresh_test_totals']={key:sum(v[key] for v in assemblies.values()) for key in ['total','passed','failed','skipped']}
    (BASE/'validation').mkdir(exist_ok=True)
    (BASE/'validation/verification.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k not in ['without_vt_link','fresh_tests']},indent=2))
    if errors: raise SystemExit(1)
    return report

if __name__=='__main__': validate()
