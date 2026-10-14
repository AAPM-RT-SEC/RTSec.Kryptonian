"""Build a complete, navigable PDF publication directly from controlled Markdown."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, html, importlib.metadata, json, os, re, subprocess, textwrap
from markdown_it import MarkdownIt
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer,
    Table, TableStyle, PageBreak, Image, KeepTogether, NextPageTemplate)
from reportlab.platypus.tableofcontents import TableOfContents
from spec_model import BASE, SOURCES, read
from validate_specs import validate

WIDTH, HEIGHT = 612, 792
MARGIN=48
CONTENT=WIDTH-2*MARGIN
INK=colors.HexColor('#172b3a'); ACCENT=colors.HexColor('#166c80')
MUTED=colors.HexColor('#5b6873'); LIGHT=colors.HexColor('#eef3f6')
MD=MarkdownIt('commonmark').enable('table').enable('strikethrough')

def normalize(s):
    return (s.replace('\u2011','-').replace('\u2013','-').replace('\u2014','-')
            .replace('\u2212','-').replace('∈','in').replace('⇒','implies'))

def inline(text):
    tokens=MD.parseInline(text)[0].children or []
    out=[]
    for t in tokens:
        if t.type=='text': out.append(html.escape(normalize(t.content)))
        elif t.type=='code_inline': out.append('<font name="SpecMono">'+html.escape(normalize(t.content))+'</font>')
        elif t.type=='strong_open': out.append('<b>')
        elif t.type=='strong_close': out.append('</b>')
        elif t.type=='em_open': out.append('<i>')
        elif t.type=='em_close': out.append('</i>')
        elif t.type=='s_open': out.append('<strike>')
        elif t.type=='s_close': out.append('</strike>')
        elif t.type in ['softbreak','hardbreak']: out.append(' ' if t.type=='softbreak' else '<br/>')
        elif t.type=='link_open':
            url=t.attrGet('href') or ''
            if url.startswith(('https://','http://')): out.append('<link href="'+html.escape(url,quote=True)+'" color="#166c80">')
            else: out.append('<font color="#166c80">')
        elif t.type=='link_close':
            opening=next((x for x in reversed(out) if x.startswith(('<link ','<font color='))), '')
            out.append('</link>' if opening.startswith('<link ') else '</font>')
        elif t.type=='html_inline':
            if re.fullmatch(r'<br\s*/?>',t.content,re.I): out.append('<br/>')
            else: out.append(html.escape(t.content))
        elif t.type=='image': out.append(html.escape(t.content))
    return ''.join(out)

def styles():
    fontdir=Path(os.environ.get('WINDIR','C:/Windows'))/'Fonts'
    for name,file in [('Spec','arial.ttf'),('SpecBold','arialbd.ttf'),('SpecItalic','ariali.ttf'),
                      ('SpecBoldItalic','arialbi.ttf'),('SpecMono','consola.ttf')]:
        pdfmetrics.registerFont(TTFont(name,str(fontdir/file)))
    pdfmetrics.registerFontFamily('Spec',normal='Spec',bold='SpecBold',italic='SpecItalic',boldItalic='SpecBoldItalic')
    result={}
    result['body']=ParagraphStyle('body',fontName='Spec',fontSize=9.2,leading=13,
        textColor=INK,spaceAfter=6,splitLongWords=True,allowWidows=0,allowOrphans=0)
    for level,size in [(1,22),(2,15),(3,11.5),(4,10.2),(5,9.5)]:
        result[f'h{level}']=ParagraphStyle(f'h{level}',parent=result['body'],fontName='SpecBold',
            fontSize=size,leading=size*1.25,textColor=ACCENT,spaceBefore=12,spaceAfter=8,keepWithNext=True)
    result['cell']=ParagraphStyle('cell',parent=result['body'],fontSize=8.3,leading=11,spaceAfter=2)
    result['label']=ParagraphStyle('label',parent=result['cell'],fontName='SpecBold',textColor=ACCENT)
    result['code']=ParagraphStyle('code',parent=result['body'],fontName='SpecMono',fontSize=7.5,
        leading=10,backColor=LIGHT,borderPadding=7,spaceBefore=5,spaceAfter=5)
    result['caption']=ParagraphStyle('caption',parent=result['body'],fontSize=8,leading=11,textColor=MUTED)
    return result

class Publication(BaseDocTemplate):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs)
        self.addPageTemplates(PageTemplate(id='main',frames=Frame(MARGIN,MARGIN,CONTENT,HEIGHT-2*MARGIN-15,
            leftPadding=0,rightPadding=0,topPadding=0,bottomPadding=0),onPageEnd=self.decorate))
        self.docid='';self.dest_count=0
    def beforeDocument(self): self.docid='';self.dest_count=0
    def decorate(self,c,doc):
        width,height=c._pagesize
        c.saveState();c.setStrokeColor(colors.HexColor('#ced9df'));c.line(MARGIN,height-34,width-MARGIN,height-34)
        c.setFillColor(MUTED);c.setFont('Spec',8)
        c.drawString(MARGIN,height-26,'RTSec.Kryptonian  /  Formal Engineering Specification')
        c.drawRightString(width-MARGIN,height-26,self.docid)
        c.drawString(MARGIN,28,'Revision 0.2  |  Draft - stakeholder approval pending')
        c.drawRightString(width-MARGIN,28,str(doc.page));c.restoreState()
    def afterFlowable(self,f):
        if hasattr(f,'docid'): self.docid=f.docid
        if hasattr(f,'heading_level'):
            key=f.dest; self.canv.bookmarkPage(key)
            if f.heading_level==1:
                self.canv.addOutlineEntry(f.getPlainText(),key,level=0)
                self.notify('TOCEntry',(0,f.getPlainText(),self.page,key))
            elif f.heading_level==2: self.canv.addOutlineEntry(f.getPlainText(),key,level=1)

def table_flow(rows,sty):
    if not rows:return []
    count=len(rows[0]); out=[]
    if count>3:
        # Preserve all columns as readable records rather than shrink a nine-column matrix.
        headers=rows[0]
        for row in rows[1:]:
            label=Paragraph(inline(row[0]),sty['h4']);out.append(label)
            for header,value in zip(headers[1:],row[1:]):
                out.append(Paragraph('<b>'+inline(header)+':</b> '+inline(value),sty['cell']))
            out.append(Spacer(1,6))
        return out
    widths=[CONTENT/count]*count
    if count==2:widths=[CONTENT*.28,CONTENT*.72]
    if count==3:widths=[CONTENT*.23,CONTENT*.32,CONTENT*.45]
    cells=[[Paragraph(inline(v),sty['label'] if i==0 else sty['cell']) for v in r] for i,r in enumerate(rows)]
    tab=Table(cells,colWidths=widths,repeatRows=1,hAlign='LEFT',splitByRow=1,splitInRow=1)
    tab.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),LIGHT),
        ('VALIGN',(0,0),(-1,-1),'TOP'),('LINEBELOW',(0,0),(-1,0),.6,ACCENT),
        ('LINEBELOW',(0,1),(-1,-1),.3,colors.HexColor('#d8e0e6')),
        ('TOPPADDING',(0,0),(-1,-1),5),('BOTTOMPADDING',(0,0),(-1,-1),5),
        ('LEFTPADDING',(0,0),(-1,-1),5),('RIGHTPADDING',(0,0),(-1,-1),5)]))
    return [tab,Spacer(1,8)]

def convert(name,sty,diagram_map):
    tokens=MD.parse(read(name)); out=[]; i=0;depth=0;lists=[]; heading=0;diagram=0; first_item_paragraph=False
    docid=re.search(r'\| Document ID \| (KSPEC-\d+) \|',read(name)).group(1)
    while i<len(tokens):
        t=tokens[i]
        if t.type=='heading_open':
            level=int(t.tag[1]);heading+=1
            para=Paragraph(inline(tokens[i+1].content),sty[f'h{min(level,5)}'])
            para.heading_level=level;para.dest=f'{docid}-{heading}';para.docid=docid
            out.append(para);i+=3;continue
        if t.type=='paragraph_open':
            text=inline(tokens[i+1].content)
            if depth:
                style=ParagraphStyle(f'list{depth}',parent=sty['body'],leftIndent=12*depth,firstLineIndent=0)
                if first_item_paragraph:
                    text=(str(lists[-1]['item'])+'. ' if lists[-1]['ordered'] else '&#8226; ')+text
                    first_item_paragraph=False
            else:style=sty['body']
            out.append(Paragraph(text,style));i+=3;continue
        if t.type in ['bullet_list_open','ordered_list_open']:
            depth+=1;lists.append({'ordered':t.type=='ordered_list_open','item':int(t.attrGet('start') or 1)-1})
        elif t.type in ['bullet_list_close','ordered_list_close']:depth-=1;lists.pop()
        elif t.type=='list_item_open':
            lists[-1]['item']+=1;first_item_paragraph=True
        elif t.type=='table_open':
            rows=[]; row=[];i+=1
            while tokens[i].type!='table_close':
                if tokens[i].type=='tr_open':row=[]
                elif tokens[i].type=='inline':row.append(tokens[i].content)
                elif tokens[i].type=='tr_close':rows.append(row)
                i+=1
            out.extend(table_flow(rows,sty))
        elif t.type=='fence':
            if t.info.strip()=='mermaid':
                diagram+=1; file=diagram_map[(name,diagram)]
                w,h=ImageReader(str(file)).getSize()
                # Mermaid screenshot is 2x CSS resolution. Foldout sheets retain
                # roughly 10-point diagram labels instead of shrinking a large graph.
                image=Image(str(file),width=w*.3,height=h*.3)
                sheet=file.stem
                headings=[]
                while out and hasattr(out[-1],'heading_level'):
                    headings.insert(0,out.pop())
                if len(out)>=2 and isinstance(out[-1],PageBreak) and isinstance(out[-2],NextPageTemplate):
                    out.pop();out.pop()
                out.extend([NextPageTemplate(sheet),PageBreak(),
                    *headings,
                    Paragraph(f'Diagram {docid}-{diagram}',sty['h2']),image,
                    Paragraph('Rendered from '+html.escape(name)+'. Diagram sheets use larger page sizes to preserve readable labels.',sty['caption']),
                    NextPageTemplate('main'),PageBreak()])
            else:
                content=normalize(t.content)
                # Preserve numbered/sequential source lines; wrap only for publication.
                lines=[]
                for line in content.splitlines():
                    lines.extend(textwrap.wrap(line,width=100,replace_whitespace=False,drop_whitespace=False) or [''])
                for chunk in range(0,len(lines),35):
                    out.append(Paragraph('<br/>'.join(html.escape(l).replace(' ','&#160;') for l in lines[chunk:chunk+35]),sty['code']))
        elif t.type=='hr':out.append(Spacer(1,6))
        elif t.type=='html_block':out.append(Paragraph(html.escape(normalize(t.content)),sty['body']))
        i+=1
    return out

def build():
    validate()
    diagdir=BASE/'validation/diagrams';diagdir.mkdir(exist_ok=True)
    items=[];diagram_map={}
    for name in SOURCES:
        for index,m in enumerate(re.finditer(r'```mermaid\s*\n(.*?)\n```',read(name),re.S),1):
            ident=f'diagram{len(items)+1:03}'
            items.append({'id':ident,'file':name,'source':m.group(1)})
            diagram_map[(name,index)]=diagdir/(ident+'.png')
    (diagdir/'input.json').write_text(json.dumps(items),encoding='utf-8')
    subprocess.run(['node',str(BASE/'tools/render_diagrams.mjs')],check=True)
    sty=styles();story=[]
    story.extend([Spacer(1,90),Paragraph('RTSec.Kryptonian',sty['h1']),
        Paragraph('Formal Engineering<br/>Specification',ParagraphStyle('cover',parent=sty['h1'],fontSize=31,leading=38)),
        Spacer(1,26),Paragraph('Revision 0.2  /  13 October 2026',sty['h2']),
        Paragraph('Production publication of the Draft specification',sty['body']),
        Spacer(1,24),Paragraph('Enterprise healthcare IT cybersecurity infrastructure: EST enrollment, institutional CA integration and credential lifecycle management.',sty['body']),
        Paragraph('Application baseline: 682f996c8447afec13bc1fce5b7f59e6a06efa25',sty['caption']),
        Paragraph('Markdown is the authoritative editable source. Publication does not imply stakeholder approval, implementation completeness, standards conformity or production readiness.',sty['body']),PageBreak()])
    story.append(Paragraph('Contents',sty['h1']))
    toc=TableOfContents();toc.levelStyles=[ParagraphStyle('toc',parent=sty['body'],fontSize=9.5,leading=14,spaceAfter=8)]
    story.append(toc)
    for name in SOURCES:
        story.append(PageBreak());story.extend(convert(name,sty,diagram_map))
    output=BASE/'Kryptonian-Formal-Specification.pdf'
    doc=Publication(str(output),pagesize=(WIDTH,HEIGHT),title='RTSec.Kryptonian Formal Engineering Specification - Revision 0.2 Draft',
        author='RTSec.Kryptonian Engineering',subject='Controlled Markdown specification publication',
        leftMargin=MARGIN,rightMargin=MARGIN,topMargin=MARGIN,bottomMargin=MARGIN)
    for file in diagram_map.values():
        w,h=ImageReader(str(file)).getSize()
        pw=max(WIDTH,w*.3+2*MARGIN);ph=max(HEIGHT,h*.3+2*MARGIN+180)
        doc.addPageTemplates(PageTemplate(id=file.stem,pagesize=(pw,ph),
            frames=Frame(MARGIN,MARGIN,pw-2*MARGIN,ph-2*MARGIN-15,
                leftPadding=0,rightPadding=0,topPadding=0,bottomPadding=0),onPageEnd=doc.decorate))
    doc.multiBuild(story)
    import pymupdf as fitz
    pdf=fitz.open(output)
    # Check the actual PDF for printable content outside its physical page boundary.
    overflow=[]
    for index,page in enumerate(pdf):
        for b in page.get_text('dict')['blocks']:
            if b['type']!=0:continue
            x0,y0,x1,y1=b['bbox']
            if x0 < -1 or y0 < -1 or x1>page.rect.width+1 or y1>page.rect.height+1:overflow.append(index+1)
    if overflow:raise RuntimeError(f'Text outside physical page boundaries: {sorted(set(overflow))}')
    preview=BASE/'validation/rendered';preview.mkdir(exist_ok=True)
    sample=set([0,1,len(pdf)-1])
    for bookmark in pdf.get_toc():sample.add(bookmark[2]-1)
    for i in sorted(sample):
        pdf[i].get_pixmap(matrix=fitz.Matrix(1.1,1.1)).save(preview/f'page-{i+1:04}.png')
    manifest={'built_at_utc':datetime.now(timezone.utc).isoformat(),'revision':'0.2','approval':'Draft',
        'sources':[{'file':name,'sha256':hashlib.sha256((BASE/name).read_bytes()).hexdigest()} for name in SOURCES],
        'output':output.name,'pdf_sha256':hashlib.sha256(output.read_bytes()).hexdigest(),
        'pages':len(pdf),'rendered_diagrams':len(items),'bookmark_count':len(pdf.get_toc()),
        'diagram_page_policy':'Foldout sheets at 0.6 PDF points per Mermaid CSS pixel; approximately 10-point labels',
        'dependencies':{n:importlib.metadata.version(n) for n in ['reportlab','markdown-it-py','PyMuPDF','pypdf']},
        'text_outside_page':overflow,'visual_review':'Pending inspection of rendered final pages'}
    (BASE/'validation/publication-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    from verify_pdf import verify
    verify()
    print(json.dumps({k:v for k,v in manifest.items() if k not in ['sources','dependencies']},indent=2))

if __name__=='__main__':build()
