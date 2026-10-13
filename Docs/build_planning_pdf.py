#!/usr/bin/env python3
"""Build the HAP 3D six-page planning PDF from its UTF-8 source text."""
from pathlib import Path
import re
from xml.sax.saxutils import escape

from PIL import Image as PILImage
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate, Frame, PageTemplate, Paragraph, Spacer, PageBreak,
    Table, TableStyle, Image, Flowable, KeepTogether,
)

ROOT = Path(__file__).resolve().parent.parent
SOURCE = Path(__file__).with_name('이선동_HAP3D_기획서_원문.txt')
OUTPUT = Path(__file__).with_name('이선동_HAP3D_기획서.pdf')
SCREENSHOT = ROOT / 'Builds' / 'Validation' / 'start.png'
FONT_PATH = Path('/System/Library/Fonts/Supplemental/AppleGothic.ttf')

INK = colors.HexColor('#192630')
MUTED = colors.HexColor('#56656F')
TEAL = colors.HexColor('#167D83')
CYAN = colors.HexColor('#60D9E2')
PALE = colors.HexColor('#EAF4F2')
PALE_BLUE = colors.HexColor('#EEF5F9')
LINE = colors.HexColor('#D7E0E2')
FLOOR = colors.HexColor('#66788A')
YANG = colors.HexColor('#FFFFFF')
YIN = colors.HexColor('#55A9D1')
RED = colors.HexColor('#D66B68')

pdfmetrics.registerFont(TTFont('AppleGothic', str(FONT_PATH)))

styles = getSampleStyleSheet()
styles.add(ParagraphStyle(
    name='TitleHap', fontName='AppleGothic', fontSize=23, leading=27,
    textColor=INK, spaceAfter=1.4*mm,
))
styles.add(ParagraphStyle(
    name='Subtitle', fontName='AppleGothic', fontSize=10.6, leading=14,
    textColor=MUTED, spaceAfter=4*mm,
))
styles.add(ParagraphStyle(
    name='Section', fontName='AppleGothic', fontSize=16.5, leading=21,
    textColor=TEAL, spaceAfter=1.2*mm, keepWithNext=True,
))
styles.add(ParagraphStyle(
    name='Deck', fontName='AppleGothic', fontSize=11.2, leading=15,
    textColor=MUTED, spaceAfter=3*mm, keepWithNext=True,
))
styles.add(ParagraphStyle(
    name='Sub', fontName='AppleGothic', fontSize=11.4, leading=15,
    textColor=INK, spaceBefore=3*mm, spaceAfter=1.5*mm,
    keepWithNext=True,
))
styles.add(ParagraphStyle(
    name='Body', fontName='AppleGothic', fontSize=10.6, leading=14.5,
    textColor=INK, spaceAfter=1.5*mm, wordWrap='CJK', splitLongWords=1,
))
styles.add(ParagraphStyle(
    name='BodyTight', fontName='AppleGothic', fontSize=10.5, leading=13.7,
    textColor=INK, spaceAfter=0.8*mm, wordWrap='CJK', splitLongWords=1,
))
styles.add(ParagraphStyle(
    name='TableHeader', fontName='AppleGothic', fontSize=9.8, leading=12.4,
    textColor=colors.white, wordWrap='CJK',
))
styles.add(ParagraphStyle(
    name='TableCell', fontName='AppleGothic', fontSize=9.8, leading=12.6,
    textColor=INK, wordWrap='CJK', splitLongWords=1,
))
styles.add(ParagraphStyle(
    name='Callout', fontName='AppleGothic', fontSize=10.6, leading=14.3,
    textColor=INK, backColor=PALE, borderColor=LINE, borderWidth=0.55,
    borderPadding=7, spaceBefore=1*mm, spaceAfter=2*mm,
    wordWrap='CJK',
))
styles.add(ParagraphStyle(
    name='Caption', fontName='AppleGothic', fontSize=9.3, leading=12,
    textColor=MUTED, alignment=TA_CENTER, spaceBefore=1*mm, spaceAfter=1.5*mm,
))
styles.add(ParagraphStyle(
    name='LinkItem', fontName='AppleGothic', fontSize=10.5, leading=14,
    textColor=TEAL, spaceAfter=2*mm, wordWrap='CJK',
))


def P(text, style='Body'):
    return Paragraph(escape(text).replace('  ', '&nbsp; '), styles[style])


def A(label, url):
    return Paragraph(f'<link href="{escape(url)}" color="#167D83">{escape(label)}</link>', styles['LinkItem'])


def line_value(lines, heading):
    try:
        i = lines.index(heading)
    except ValueError:
        return []
    out = []
    for line in lines[i+1:]:
        if not line.strip():
            if out:
                break
            continue
        if re.match(r'^0[1-5]\.\s+', line):
            break
        out.append(line.strip())
    return out


def section_slice(lines, prefix):
    starts = [i for i, line in enumerate(lines) if line.startswith(prefix + '.')]
    if not starts:
        raise ValueError(f'Missing source section {prefix}')
    i = starts[0]
    j = next((k for k in range(i+1, len(lines)) if re.match(r'^0[1-5]\.\s+', lines[k])), len(lines))
    return lines[i:j]


def source_table(lines, heading):
    block = line_value(lines, heading)
    rows = []
    for line in block:
        if '|' not in line:
            break
        rows.append([cell.strip() for cell in line.split('|')])
    return rows


def text_after_table(lines, heading):
    """Return the first prose paragraph after a pipe-delimited source table."""
    try:
        i = lines.index(heading) + 1
    except ValueError:
        return []
    while i < len(lines) and lines[i].strip() and '|' in lines[i]:
        i += 1
    while i < len(lines) and not lines[i].strip():
        i += 1
    result = []
    while i < len(lines) and lines[i].strip():
        result.append(lines[i].strip())
        i += 1
    return result


def make_table(rows, widths, header=True, font='TableCell', row_bgs=None):
    converted = []
    for ri, row in enumerate(rows):
        sty = 'TableHeader' if header and ri == 0 else font
        converted.append([Paragraph(escape(str(cell)), styles[sty]) for cell in row])
    t = Table(converted, colWidths=widths, repeatRows=(1 if header else 0), hAlign='LEFT')
    cmds = [
        ('GRID', (0,0), (-1,-1), 0.45, LINE),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('LEFTPADDING', (0,0), (-1,-1), 6),
        ('RIGHTPADDING', (0,0), (-1,-1), 6),
        ('TOPPADDING', (0,0), (-1,-1), 5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 5),
    ]
    if header:
        cmds.append(('BACKGROUND', (0,0), (-1,0), TEAL))
        cmds.append(('BACKGROUND', (0,1), (-1,-1), colors.HexColor('#F5F8F8')))
    if row_bgs:
        for row, color in row_bgs.items():
            cmds.append(('BACKGROUND', (0,row), (-1,row), color))
    t.setStyle(TableStyle(cmds))
    return t


class RelationshipDiagram(Flowable):
    """Three like-shaped cards explain the authoritative block relationships."""
    def __init__(self, width=170*mm, height=50*mm):
        super().__init__(); self.width = width; self.height = height

    def draw(self):
        c = self.canv
        gap = 4*mm
        cw = (self.width - 2*gap) / 3
        h = self.height - 1*mm
        specs = [
            ('양 단독', '길 유지', '양', 'stable'),
            ('음 단독 접근', '붕괴 시작', '음', 'broken'),
            ('양과 음 함께', '안정된 길', '양음', 'stable2'),
        ]
        for i, (title, result, actors, state) in enumerate(specs):
            x = i * (cw + gap)
            c.setStrokeColor(LINE); c.setFillColor(colors.white)
            c.roundRect(x, 0, cw, h, 3*mm, fill=1, stroke=1)
            c.setFillColor(INK); c.setFont('AppleGothic', 10.7)
            c.drawCentredString(x+cw/2, h-6*mm, title)
            floor_y = 17*mm
            if actors == '양':
                dots = [(x+cw/2, YANG, '양')]
            elif actors == '음':
                dots = [(x+cw/2, YIN, '음')]
            else:
                dots = [(x+cw/2-6*mm, YANG, '양'), (x+cw/2+6*mm, YIN, '음')]
            for dx, col, label in dots:
                c.setFillColor(col); c.setStrokeColor(INK)
                c.circle(dx, floor_y+9*mm, 4*mm, fill=1, stroke=1)
                c.setFillColor(INK); c.setFont('AppleGothic', 7.5)
                c.drawCentredString(dx, floor_y+8*mm-1, label)
            if state == 'stable':
                c.setFillColor(TEAL); c.roundRect(x+8*mm, floor_y, cw-16*mm, 3*mm, 1.2*mm, fill=1, stroke=0)
            elif state == 'broken':
                c.setFillColor(FLOOR)
                c.rect(x+8*mm, floor_y, 12*mm, 3*mm, fill=1, stroke=0)
                c.rect(x+cw-20*mm, floor_y, 12*mm, 3*mm, fill=1, stroke=0)
                c.setStrokeColor(RED); c.setLineWidth(1.5)
                c.line(x+cw/2-2*mm, floor_y-1*mm, x+cw/2+2*mm, floor_y+4*mm)
                c.line(x+cw/2+2*mm, floor_y-1*mm, x+cw/2-2*mm, floor_y+4*mm)
            else:
                c.setFillColor(CYAN); c.roundRect(x+8*mm, floor_y, cw-16*mm, 3.6*mm, 1.2*mm, fill=1, stroke=0)
                c.setStrokeColor(TEAL); c.setLineWidth(0.8)
                c.roundRect(x+6.7*mm, floor_y-1.3*mm, cw-13.4*mm, 6.2*mm, 2*mm, fill=0, stroke=1)
            c.setFillColor(MUTED); c.setFont('AppleGothic', 9.6)
            c.drawCentredString(x+cw/2, 5*mm, result)


class PlatformPlan(Flowable):
    """Side-view sketch of the implemented floor, bridge, landing and ceiling goal."""
    def __init__(self, width=170*mm, height=66*mm):
        super().__init__(); self.width = width; self.height = height

    def draw(self):
        c = self.canv; w = self.width; h = self.height
        c.setFillColor(colors.HexColor('#F7FAFB')); c.setStrokeColor(LINE)
        c.roundRect(0, 0, w, h, 3*mm, fill=1, stroke=1)
        base_y = 19*mm; thick = 5*mm
        # Floor: start, single light bridge and landing.
        c.setFillColor(FLOOR)
        c.rect(9*mm, base_y, 35*mm, thick, fill=1, stroke=0)
        c.setFillColor(CYAN); c.rect(44*mm, base_y+2*mm, 62*mm, 2.5*mm, fill=1, stroke=0)
        c.setFillColor(FLOOR); c.rect(106*mm, base_y, 31*mm, thick, fill=1, stroke=0)
        # Ceiling goal over landing.
        goal_x, goal_y, goal_w, goal_h = 102*mm, h-16*mm, 39*mm, 6*mm
        c.setFillColor(TEAL); c.roundRect(goal_x, goal_y, goal_w, goal_h, 1.5*mm, fill=1, stroke=0)
        c.setFillColor(colors.white); c.setFont('AppleGothic', 9.3)
        c.drawCentredString(goal_x+goal_w/2, h-14.5*mm, '천장 목표')
        # Player marks and bridge label.
        for x, col, label in [(20*mm, YANG, '양'), (32*mm, YIN, '음')]:
            c.setFillColor(col); c.setStrokeColor(INK); c.circle(x, base_y+thick+5*mm, 3.5*mm, fill=1, stroke=1)
            c.setFillColor(INK); c.setFont('AppleGothic', 7.5); c.drawCentredString(x, base_y+thick+4*mm-1, label)
        c.setFillColor(MUTED); c.setFont('AppleGothic', 9)
        c.drawCentredString(75*mm, base_y+8*mm, '빛다리')
        c.drawCentredString(26*mm, base_y-5*mm, '시작 발판')
        c.drawCentredString(121*mm, base_y-5*mm, '착지 발판')
        # Gravity reversal and upward route.
        ax = 151*mm
        c.setStrokeColor(TEAL); c.setLineWidth(1.8)
        c.line(ax, base_y+5*mm, ax, h-23*mm)
        c.line(ax, h-23*mm, ax-2.5*mm, h-28*mm); c.line(ax, h-23*mm, ax+2.5*mm, h-28*mm)
        c.setFillColor(TEAL); c.setFont('AppleGothic', 9)
        c.drawCentredString(ax, h-20*mm, '중력 반전')


class SpacePlan(Flowable):
    """Distinguishes the current flat prototype from later planetary concepts."""
    def __init__(self, width=170*mm, height=61*mm):
        super().__init__(); self.width = width; self.height = height

    def draw(self):
        c = self.canv; gap = 3*mm; w = self.width; h = self.height
        widths = [47*mm, 57*mm, w-47*mm-57*mm-2*gap]
        labels = [('현재 프로토타입', 0), ('행성 외부 · 후속 구상', 1), ('행성 내부 · 후속 구상', 2)]
        x = 0
        for idx, (pw, (label, kind)) in enumerate(zip(widths, labels)):
            c.setLineWidth(0.8)
            if kind == 0:
                c.setStrokeColor(LINE); c.setDash()
            else:
                c.setStrokeColor(TEAL); c.setDash(3, 2)
            c.setFillColor(colors.white); c.roundRect(x, 0, pw, h, 2.5*mm, fill=1, stroke=1)
            c.setDash()
            c.setFillColor(INK); c.setFont('AppleGothic', 9.3)
            c.drawCentredString(x+pw/2, h-6*mm, label)
            midx = x+pw/2
            if kind == 0:
                y = 20*mm
                c.setFillColor(FLOOR)
                c.rect(x+6*mm, y, 9*mm, 3*mm, fill=1, stroke=0)
                c.setFillColor(CYAN); c.rect(x+15*mm, y+1*mm, 16*mm, 2*mm, fill=1, stroke=0)
                c.setFillColor(FLOOR); c.rect(x+31*mm, y, 9*mm, 3*mm, fill=1, stroke=0)
                c.setFillColor(MUTED); c.setFont('AppleGothic', 8.5)
                c.drawCentredString(midx, 11*mm, '발판 → 빛다리 → 착지')
            elif kind == 1:
                radius = 15*mm; cx = midx; cy = 27*mm
                c.setStrokeColor(TEAL); c.setLineWidth(1.5); c.circle(cx, cy, radius, fill=0, stroke=1)
                c.setStrokeColor(TEAL); c.setLineWidth(2.0); c.arc(cx-radius, cy-radius, cx+radius, cy+radius, 25, 115)
                c.setFillColor(FLOOR); c.roundRect(cx+radius-2*mm, cy+4*mm, 12*mm, 3*mm, 1*mm, fill=1, stroke=0)
                c.setFillColor(MUTED); c.setFont('AppleGothic', 8.5)
                c.drawCentredString(midx, 7*mm, '표면과 주변 발판')
            else:
                cx = midx; cy = 28*mm
                c.setStrokeColor(TEAL); c.setLineWidth(1.2)
                c.ellipse(cx-15*mm, cy-13*mm, cx+15*mm, cy+13*mm, fill=0, stroke=1)
                c.ellipse(cx-8*mm, cy-7*mm, cx+8*mm, cy+7*mm, fill=0, stroke=1)
                c.setFillColor(CYAN); c.roundRect(cx-5*mm, cy-1.4*mm, 10*mm, 2.8*mm, 1*mm, fill=1, stroke=0)
                c.setFillColor(MUTED); c.setFont('AppleGothic', 8.5)
                c.drawCentredString(midx, 7*mm, '안쪽 통로 후보')
            x += pw + gap


def footer(canvas, doc):
    canvas.saveState(); width, _ = A4
    canvas.setStrokeColor(LINE); canvas.setLineWidth(0.55)
    canvas.line(19*mm, 14*mm, width-19*mm, 14*mm)
    canvas.setFont('AppleGothic', 8.3); canvas.setFillColor(MUTED)
    canvas.drawString(19*mm, 8.8*mm, 'HAP 3D  ·  이선동  ·  2026.10.02')
    canvas.drawRightString(width-19*mm, 8.8*mm, f'v0.4  ·  {doc.page}')
    canvas.restoreState()


class PlanningDoc(BaseDocTemplate):
    def __init__(self, filename):
        super().__init__(str(filename), pagesize=A4,
                         leftMargin=20*mm, rightMargin=20*mm,
                         topMargin=16*mm, bottomMargin=24*mm,
                         title='HAP 3D | 게임개발캠프 2차 프로젝트',
                         author='이선동', subject='HAP 3D 프로젝트 기획서')
        frame = Frame(self.leftMargin, self.bottomMargin, self.width, self.height,
                      id='main', leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
        self.addPageTemplates([PageTemplate(id='pages', frames=[frame], onPage=footer)])


def body_section(story, title, deck):
    story.append(Paragraph(escape(title), styles['Section']))
    if deck:
        story.append(Paragraph(escape(deck), styles['Deck']))


def table_source(lines, heading, widths, header=True):
    rows = source_table(lines, heading)
    if rows:
        return make_table(rows, widths, header=header)
    return None


def references(lines):
    idx = lines.index('출처 부록')
    items = []
    tail = lines[idx+1:]
    i = 0
    while i < len(tail):
        label = tail[i].strip()
        if not label:
            i += 1; continue
        if i+1 < len(tail) and tail[i+1].strip().startswith('http'):
            items.append((label, tail[i+1].strip())); i += 2
        else:
            i += 1
    return items


def build():
    lines = SOURCE.read_text(encoding='utf-8').splitlines()
    if len(lines) < 3 or lines[0] != 'HAP 3D':
        raise ValueError('Unexpected source header')
    s1 = section_slice(lines, '01')
    s2 = section_slice(lines, '02')
    s3 = section_slice(lines, '03')
    s4 = section_slice(lines, '04')
    s5 = section_slice(lines, '05')
    story = []

    # Page 1 — project premise, core information and real prototype screenshot.
    story.append(Paragraph('HAP 3D', styles['TitleHap']))
    story.append(P('게임개발캠프 2차 프로젝트  |  이선동  |  2026.10.02', 'Subtitle'))
    body_section(story, '01. 게임 개요', s1[1] if len(s1) > 1 else '')
    intro = line_value(s1, '게임 소개')
    for text in intro: story.append(P(text))
    story.append(Paragraph('기획 의도', styles['Sub']))
    for text in line_value(s1, '기획 의도'): story.append(P(text))
    story.append(Paragraph('게임 정보', styles['Sub']))
    t = table_source(s1, '게임 정보', [36*mm, 133*mm])
    if t: story.append(t)
    story.append(Spacer(1, 1.5*mm))
    story.append(Paragraph('플레이의 중심', styles['Sub']))
    story.append(Spacer(1, 2*mm))
    flow = line_value(s1, '플레이의 중심')
    if flow: story.append(P(flow[0], 'Callout'))
    if len(flow) > 1: story.append(P(flow[1], 'BodyTight'))
    if SCREENSHOT.exists():
        with PILImage.open(SCREENSHOT) as screenshot:
            pixel_width, pixel_height = screenshot.size
        image_width = 170*mm
        image_height = image_width * pixel_height / pixel_width
        body_width = A4[0] - 40*mm
        assert image_width <= body_width, 'Screenshot exceeds the document body width'
        im = Image(str(SCREENSHOT), width=image_width, height=image_height,
                   hAlign='LEFT', lazy=0)
        story.append(Spacer(1, 1.5*mm)); story.append(im)
        story.append(P('현재 구현 화면 · 시작 발판, 빛다리, 두 캐릭터와 목표 발판', 'Caption'))

    # Page 2 — the three character/block relationships and gravity rule.
    story.append(PageBreak())
    body_section(story, '02. 핵심 규칙', s2[1] if len(s2) > 1 else '')
    story.append(Paragraph('빛블록의 세 관계', styles['Sub']))
    story.append(RelationshipDiagram())
    story.append(Paragraph('이동 순서', styles['Sub']))
    for text in line_value(s2, '이동 순서'): story.append(P(text))
    story.append(Paragraph('중력 반전', styles['Sub']))
    for text in line_value(s2, '중력 반전'): story.append(P(text))
    story.append(Paragraph('상태 전달', styles['Sub']))
    for text in line_value(s2, '상태 전달'): story.append(P(text))

    # Page 3 — current level sketch and five-step player action.
    story.append(PageBreak())
    body_section(story, '03. 첫 플레이 구간', s3[1] if len(s3) > 1 else '')
    story.append(Paragraph('구간 배치 · 현재 프로토타입', styles['Sub']))
    story.append(PlatformPlan())
    story.append(P('아래쪽 발판에서 시작해 빛다리와 착지 발판을 건넌다. 착지 뒤 각 캐릭터의 중력을 반전해 위쪽 천장 목표로 이동한다.', 'Caption'))
    story.append(Paragraph('진행 순서', styles['Sub']))
    for line in line_value(s3, '진행 순서'):
        story.append(P(line, 'BodyTight'))
    story.append(Paragraph('실패와 재시도', styles['Sub']))
    for text in line_value(s3, '실패와 재시도'): story.append(P(text))
    story.append(Paragraph('확인할 경험', styles['Sub']))
    for text in line_value(s3, '이 구간에서 확인할 경험'): story.append(P(text))

    # Page 4 — distinguish built space from planetary follow-up concepts.
    story.append(PageBreak())
    body_section(story, '04. 공간과 화면 구성', s4[1] if len(s4) > 1 else '')
    story.append(SpacePlan())
    story.append(P('실선은 현재 평면 발판 프로토타입, 점선은 후속 공간 구상이다. 행성 외부·내부와 구면 중력은 구현 완료 항목이 아니다.', 'Caption'))
    for heading in ['발판 구간', '행성 외부 — 후속 공간 구상', '행성 내부 — 후속 공간 구상']:
        story.append(Paragraph(heading, styles['Sub']))
        for text in line_value(s4, heading): story.append(P(text))
    story.append(Paragraph('비주얼·화면 구성', styles['Sub']))
    for heading in ['비주얼 방향', '화면 구성']:
        for text in line_value(s4, heading): story.append(P(text))

    # Page 5 — implementation facts, temporary settings and school milestones.
    story.append(PageBreak())
    body_section(story, '05. 제작 계획', s5[1] if len(s5) > 1 else '')
    story.append(Paragraph('제작 범위', styles['Sub']))
    for text in line_value(s5, '제작 범위'): story.append(P(text, 'BodyTight'))
    story.append(Paragraph('제출 일정과 작업', styles['Sub']))
    t = table_source(s5, '제출 일정과 작업', [29*mm, 42*mm, 98*mm])
    if t: story.append(t)
    story.append(Spacer(1, 3*mm))
    schedule_note_start = next((i for i, x in enumerate(s5) if x.startswith('프로젝트 기간은')), None)
    if schedule_note_start is not None:
        note_lines = []
        for x in s5[schedule_note_start:]:
            if not x.strip(): break
            note_lines.append(x)
        for x in note_lines: story.append(P(x, 'BodyTight'))

    # Page 6 — prototype detail, then concise references.
    story.append(PageBreak())
    body_section(story, '부록. 현재 프로토타입', '구현 확인 내용과 조정 전 실험값')
    story.append(Paragraph('현재 구현', styles['Sub']))
    for text in line_value(s5, '현재 구현'): story.append(P(text))
    story.append(Paragraph('현재 실험 설정 · 모두 임시값', styles['Sub']))
    t = table_source(s5, '현재 실험 설정', [39*mm, 130*mm])
    if t: story.append(t)
    story.append(Spacer(1, 3*mm))
    for text in text_after_table(s5, '현재 실험 설정'):
        story.append(P(text, 'BodyTight'))
    story.append(Spacer(1, 2*mm))
    story.append(Paragraph('참고 문서와 제출 경로', styles['Section']))
    story.append(P('Unity 자료는 구성 참고용이며 학교 지정 양식이 아니다. 기존 HAP 문서는 짧은 핵심 문장, 공간 배치도, 규칙·일정표 구성에 참고했다.', 'BodyTight'))
    refs = references(lines)
    unity = refs[:3]
    important_school_links = {
        '학교 2차 프로젝트 안내', '기획서·빌드 제출 안내',
        'Unity 실행파일 제작 안내', '11월 13일 최종 빌드 일정',
        '기획서 대시보드', '기획서 제출 폴더',
    }
    school = [(label, url) for label, url in refs[3:] if label in important_school_links]
    story.append(Paragraph('기획 구성 참고', styles['Sub']))
    story.append(P('Hap_World_LevelDesign_Plan_v0.1.docx', 'BodyTight'))
    for label, url in unity: story.append(A(label, url))
    story.append(Spacer(1, 2*mm))
    story.append(Paragraph('학교 안내와 제출 경로', styles['Sub']))
    # Two balanced columns improve scan speed while retaining every supplied link.
    cut = (len(school)+1)//2
    col1 = [A(label, url) for label, url in school[:cut]]
    col2 = [A(label, url) for label, url in school[cut:]]
    maxlen = max(len(col1), len(col2))
    while len(col1) < maxlen: col1.append(Spacer(1, 1))
    while len(col2) < maxlen: col2.append(Spacer(1, 1))
    cols = Table([[col1[i], col2[i]] for i in range(maxlen)], colWidths=[84*mm, 84*mm], hAlign='LEFT')
    cols.setStyle(TableStyle([
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('LEFTPADDING', (0,0), (-1,-1), 0), ('RIGHTPADDING', (0,0), (-1,-1), 5*mm),
        ('TOPPADDING', (0,0), (-1,-1), 0), ('BOTTOMPADDING', (0,0), (-1,-1), 1*mm),
    ]))
    story.append(cols)

    PlanningDoc(OUTPUT).build(story)
    print(f'Wrote {OUTPUT}')

if __name__ == '__main__':
    build()
