"""Verify the published PDF and render representative pages for visual review."""
from pathlib import Path
import hashlib, json, re
import pymupdf as fitz
from PIL import Image, ImageDraw
from spec_model import BASE, SOURCES, sr_records, vt_records

def verify():
    output=BASE/'Kryptonian-Formal-Specification.pdf'
    pdf=fitz.open(output)
    text='\n'.join(p.get_text() for p in pdf)
    errors=[]
    missing=[i for i in list(sr_records())+list(vt_records()) if i not in text]
    if missing:errors.append('Missing identifiers: '+', '.join(missing))
    if '\x00' in text:errors.append('PDF contains a missing-glyph/null character')
    chapters=[r for r in pdf.get_toc() if r[0]==1]
    if len(chapters)!=len(SOURCES):errors.append('Not all document titles have bookmarks')
    images=[i for i,p in enumerate(pdf) if p.get_images()]
    if len(images)!=21:errors.append('Not all 21 diagrams have dedicated rendered pages')
    body_overflow=[]
    for i,p in enumerate(pdf):
        for b in p.get_text('dict')['blocks']:
            if b['type']!=0:continue
            x0,y0,x1,y1=b['bbox']
            if 45<y0<p.rect.height-45 and (x0<46 or x1>p.rect.width-46):body_overflow.append(i+1)
    if body_overflow:errors.append('Text crosses content margins: '+str(sorted(set(body_overflow))))
    manifest=json.loads((BASE/'validation/publication-manifest.json').read_text())
    for source in manifest['sources']:
        if hashlib.sha256((BASE/source['file']).read_bytes()).hexdigest()!=source['sha256']:
            errors.append('Source changed after publication: '+source['file'])
    if hashlib.sha256(output.read_bytes()).hexdigest()!=manifest['pdf_sha256']:
        errors.append('PDF hash mismatch')
    out=BASE/'validation/rendered';out.mkdir(exist_ok=True)
    samples=sorted(set([0,1,len(pdf)-1]+[c[2]-1 for c in chapters]+images))
    for i in samples:
        p=pdf[i];scale=min(1.4,1500/max(p.rect.width,p.rect.height))
        p.get_pixmap(matrix=fitz.Matrix(scale,scale)).save(out/f'final-{i+1:04}.png')
    for sheet in range((len(samples)+11)//12):
        selection=samples[sheet*12:(sheet+1)*12]
        canvas=Image.new('RGB',(1360,1410),'#d4dde4');draw=ImageDraw.Draw(canvas)
        for j,i in enumerate(selection):
            im=Image.open(out/f'final-{i+1:04}.png');im.thumbnail((326,438))
            x=(j%4)*340;y=(j//4)*470
            canvas.paste(im,(x+(326-im.width)//2,y+24))
            draw.text((x+8,y+5),f'Page {i+1}',fill='black')
        canvas.save(out/f'final-contact-{sheet+1}.png')
    result={'pages':len(pdf),'document_bookmarks':len(chapters),'diagram_pages':len(images),
            'required_identifiers_present':not missing,'content_margin_overflow':sorted(set(body_overflow)),
            'source_and_output_hashes_match':not any('hash' in x or 'Source changed' in x for x in errors),
            'rendered_review_pages':[i+1 for i in samples],'errors':errors}
    (BASE/'validation/pdf-verification.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,indent=2))
    if errors:raise SystemExit(1)

if __name__=='__main__':verify()
