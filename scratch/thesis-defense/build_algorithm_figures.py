from pathlib import Path
import json
import re
import sys
import textwrap
from xml.sax.saxutils import escape

from PIL import Image, ImageDraw, ImageFont
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph

sys.stdout.reconfigure(encoding='utf-8')
ROOT = Path('C:/Unity/HadalDescent')
OUT = ROOT / 'output/thesis-defense'
WORK = ROOT / 'scratch/thesis-defense/code-figures'
OUT.mkdir(parents=True, exist_ok=True)
WORK.mkdir(parents=True, exist_ok=True)
MESH = 'Assets/Scripts/Managers/OceanFloorMeshGenerator.cs'
TERRAIN = 'Assets/Scripts/Managers/TerrainGenerator.cs'
SPAWN = 'Assets/Scripts/Managers/SpeciesSpawner.cs'
STEER = 'Assets/Scripts/Creatures/ContextSteering.cs'

figures = [
 dict(title='Perlin Noise Sampling Code Snippet', file=MESH, start=341, end=347,
      explanation='This method converts a horizontal world position into a smoothly varying noise value. The frequency controls the scale of the pattern, while the seed and offset shift the sampled location. Mathf.PerlinNoise supplies the base value, and the final polynomial reshapes it. This is needed to give the terrain a continuous starting pattern instead of assigning unrelated random heights to neighboring points.',
      simple='Think of it as getting a smoothly changing height guide for each place on the map.',
      objective='Supports Objective 2 by supplying the noise pattern used for generated terrain.'),
 dict(title='Layered Terrain Patterns Code Snippet', file=MESH, start=256, end=266,
      explanation='The terrain generator combines noise samples taken at different frequencies. The macro values contribute broader formations, while the mound values contribute smaller features. The multipliers determine how strongly each pattern contributes. Combining these layers gives the generator a more varied starting shape, which is later adjusted by biome influence and terrain shaping. This supports exploration by creating differences in the seafloor.',
      simple='One pattern shapes the large formations, and another adds smaller formations.',
      objective='Supports Objective 2 through configurable terrain variation.'),
 dict(title='Terrain Height and Depth Limit Code Snippet', file=MESH, start=325, end=329,
      explanation='This part converts the shaped terrain value into a vertical world coordinate. The baseline is combined with the shaped value multiplied by amplitude. In the Sunlight Zone, the code limits the generated seabed to a maximum Y coordinate of -10, keeping it at least 10 world units below Y = 0. These limits help the generated terrain fit the intended underwater play area.',
      simple='The pattern becomes an actual terrain height, with a limit to keep the seabed underwater.',
      objective='Supports Objective 2 by controlling the generated surface height.'),
 dict(title='Habitat Classification Code Snippet', file=TERRAIN, start=225, end=232,
      explanation='This method assigns a habitat category by comparing a sampled value with configured thresholds. It returns categories such as open water, soft seabed, hard reef, rock, or special habitat. GetBiomeAt supplies the value from a Perlin Noise sample. The categories give the placement system something to compare against a species preferred habitat. The thresholds are authored settings; they do not automatically establish ecological accuracy.',
      simple='It labels areas of the map so the placement system can check which habitat they represent.',
      objective='Supports Objective 2 by providing habitat categories for rule-based placement.'),
 dict(title='Seafloor Depth and Slope Checks Code Snippet', file=SPAWN, start=438, end=446,
      explanation='Within the branch for organisms placed on the seabed, the spawner checks the floor height against the configured depth limits, allowing a 2.5-unit tolerance. It also measures the surface angle and compares it with the maximum slope allowed for that species. The word continue rejects this candidate and moves to another search attempt. These checks help avoid positions that are too shallow, too deep, or too steep for the configured placement.',
      simple='Before placing a seabed organism, the game checks whether the floor is suitable.',
      objective='Supports Objective 2 through depth and surface suitability checks.'),
 dict(title='Habitat Depth and Spacing Validation Code Snippet', file=SPAWN, start=515, end=527,
      explanation='During the initial placement search, a candidate must match the preferred habitat, fall within the depth limits with a 3-unit tolerance, and satisfy the species spacing requirement. A failed condition skips that candidate. A successful candidate is returned as the accepted position and rotation. These rules help organize species and reduce crowded placements. Later search and fallback stages use additional logic, so this excerpt is not a guarantee that every final placement satisfies every preferred condition.',
      simple='The game checks the habitat, depth, and available space before accepting a location.',
      objective='Supports Objective 2 and the habitat associations used by Objective 5.'),
 dict(title='Directional Interest Calculation Code Snippet', file=STEER, start=503, end=515,
      explanation='For each possible movement direction, dot products measure alignment with the goal and the creature facing direction. Away from a boundary, the interest value combines 80 percent goal alignment and 20 percent forward alignment. Near a boundary, it instead blends the goal alignment toward the zone center. This helps movement remain purposeful while encouraging creatures to turn inward before leaving the playable area.',
      simple='Interest asks how useful each direction is for where the creature wants to go.',
      objective='Supports Objective 4 through goal-directed movement and boundary response.'),
 dict(title='Obstacle Detection and Danger Calculation Code Snippet', file=STEER, start=644, end=662,
      explanation='SphereCast checks along a movement direction using a probe with thickness. If it detects a valid obstacle, the code converts the hit distance into a danger value: nearby hits receive higher danger than distant hits. It combines that value with the floor penalty and stores information about the closest obstacle for later avoidance behavior. This is needed because useful movement toward a goal may still lead into terrain or props.',
      simple='Danger asks whether there is something in the way and how close it is.',
      objective='Supports Objective 4 by supplying obstacle information for avoidance.'),
 dict(title='Steering Score and Direction Blending Code Snippet', file=STEER, start=760, end=776,
      explanation='Directions with danger below 0.80 are scored using interest multiplied by clearance squared, where clearance is one minus danger. This implements S = I x (1 - D)^2. The code remembers the best safe direction and adds directions with sufficient score to a weighted movement vector. Squaring safeScore again makes stronger candidates contribute more to that blend. The surrounding method normalizes a usable blend and has fallback behavior when one is unavailable.',
      simple='The creature combines useful directions with enough clearance to decide where to move next.',
      objective='Supports Objective 4 by combining interest and danger into steering decisions.'),
]

for i, f in enumerate(figures, 1):
    lines = (ROOT / f['file']).read_text(encoding='utf-8-sig').splitlines()
    f['number'] = i
    f['code'] = textwrap.dedent('\n'.join(lines[f['start']-1:f['end']])).rstrip()
    f['source'] = f"{Path(f['file']).name}, lines {f['start']}-{f['end']}"
    f['url'] = (ROOT / f['file']).as_posix() + ':' + str(f['start'])
    assert f['code'], f

font_dir = Path('C:/Windows/Fonts')
pdfmetrics.registerFont(TTFont('Body', str(font_dir/'arial.ttf')))
pdfmetrics.registerFont(TTFont('BodyBold', str(font_dir/'arialbd.ttf')))
pdfmetrics.registerFont(TTFont('BodyItalic', str(font_dir/'ariali.ttf')))
pdfmetrics.registerFontFamily('Body', normal='Body', bold='BodyBold', italic='BodyItalic', boldItalic='BodyBold')
code_font = ImageFont.truetype(str(font_dir/'consola.ttf'), 30)
token_re = re.compile(r'//.*$|"(?:\\.|[^"\\])*"|\b(?:private|public|static|float|int|bool|return|if|else|continue|out|new)\b|\b\d+(?:\.\d+)?f?\b')

def code_panel(f):
    source_lines = f['code'].splitlines()
    # Wrap only the displayed figure; the Markdown retains the exact source excerpt.
    shown = []
    for line in source_lines:
        if len(line) <= 98:
            shown.append(line)
        else:
            indent = len(line) - len(line.lstrip())
            shown.extend(textwrap.wrap(line, width=98, subsequent_indent=' '*(indent+4),
                                       replace_whitespace=False, drop_whitespace=True,
                                       break_long_words=False, break_on_hyphens=False))
    line_height = 41
    pad = 30
    width = 1860
    img = Image.new('RGB', (width, pad*2+len(shown)*line_height), '#303030')
    d = ImageDraw.Draw(img)
    for row, line in enumerate(shown):
        x, y = pad, pad+row*line_height
        pos = 0
        for match in token_re.finditer(line):
            plain = line[pos:match.start()]
            d.text((x,y), plain, font=code_font, fill='#f3f3f3')
            x += d.textlength(plain, font=code_font)
            token = match.group()
            color = '#8fbb87' if token.startswith('"') else '#9e9e9e' if token.startswith('//') else '#df9c86' if token[0].isdigit() else '#eab47c'
            d.text((x,y), token, font=code_font, fill=color)
            x += d.textlength(token, font=code_font)
            pos = match.end()
        d.text((x,y), line[pos:], font=code_font, fill='#f3f3f3')
        assert d.textlength(line, font=code_font) <= width-pad*2, (f['number'], line)
    path = WORK/f"figure-{f['number']:02d}.png"
    img.save(path)
    return path, img.height/img.width

for f in figures:
    f['image'], f['aspect'] = code_panel(f)

pdf_path = OUT/'algorithm-code-snippets-explained.pdf'
md_path = OUT/'algorithm-code-snippets-explained.md'
c = canvas.Canvas(str(pdf_path), pagesize=A4)
c.setTitle('Hadal Descent Algorithm Code Snippets Explained')
c.setAuthor('Hadal Descent')
W,H = A4
LEFT, RIGHT = 52, 52
CW = W-LEFT-RIGHT
BOTTOM = 47
y = H-48
page_no = 1
body_style = ParagraphStyle('Body', fontName='Body', fontSize=10.3, leading=15, spaceAfter=0)

def paragraph(text, size=10.3, leading=15, font='Body', color=colors.black, gap=9):
    global y
    style = ParagraphStyle('p', parent=body_style, fontName=font, fontSize=size, leading=leading, textColor=color)
    p = Paragraph(text,style)
    _,h = p.wrap(CW, 1000)
    p.drawOn(c, LEFT, y-h)
    y -= h+gap

def footer():
    c.setFont('Body',8)
    c.setFillColor(colors.HexColor('#555555'))
    c.drawString(LEFT,28,'Hadal Descent | Algorithm code explanations')
    c.drawRightString(W-RIGHT,28,str(page_no))
    c.setFillColor(colors.black)

def next_page():
    global y,page_no
    footer()
    c.showPage()
    page_no += 1
    y = H-48

def draw_figure(f):
    global y
    paragraph('Figure '+str(f['number']),size=10.5,font='BodyBold',gap=5)
    paragraph(escape(f['title']),size=10.5,font='BodyItalic',gap=9)
    ih = CW*f['aspect']
    c.drawImage(str(f['image']),LEFT,y-ih,width=CW,height=ih)
    y -= ih+8
    paragraph(escape(f['source']),size=8.2,leading=11,color=colors.HexColor('#555555'),gap=8)
    paragraph(escape(f['explanation']),gap=7)
    paragraph('<b>In simple terms:</b> '+escape(f['simple']),gap=7)
    paragraph('<b>Objective connection:</b> '+escape(f['objective']),size=9.3,leading=13,gap=18)
    assert y >= BOTTOM, (page_no,f['number'],y)

paragraph('Hadal Descent Algorithm Code Snippets Explained',size=17,leading=21,font='BodyBold',gap=10)
paragraph('These excerpts explain how Perlin Noise builds the seafloor, placement rules organize species, and Context Steering guides movement. Each figure connects the code to its purpose in the game.',gap=8)
paragraph('The code is excerpted from the current Unity source. Indentation is normalized and long lines wrap in the figures. Excerpts use surrounding method variables and are not standalone programs. Figure numbers are local to this handout and can be renumbered for the thesis.',size=9.1,leading=13,gap=17)

# Two related figures per page, with the final steering calculation and defense notes together.
for group_idx, group in enumerate([figures[0:2],figures[2:4],figures[4:6],figures[6:8],figures[8:9]]):
    if group_idx:
        next_page()
    for f in group:
        draw_figure(f)

paragraph('Explaining the steering formula',size=11,font='BodyBold',gap=7)
paragraph('If interest is 0.8 and danger is 0.2, the score is 0.512. With the same interest and danger of 0.7, the score becomes 0.072. The direction becomes much less attractive as danger rises. These are illustrative calculations, not recorded gameplay results.',gap=13)
paragraph('A short defense explanation',size=11,font='BodyBold',gap=7)
paragraph('We use Perlin Noise to create varied seafloor shapes, rule-based placement to organize species according to configured conditions, and Context Steering to guide organisms around nearby obstacles. These systems support the dynamic environment and autonomous movement objectives. Our assets and researched Bestiary content give the exploration educational meaning.',gap=12)
paragraph('Technical functionality, enjoyment, and learning need separate evaluation. Procedural terrain does not automatically provide biological accuracy, and obstacle avoidance does not establish a complete simulation of animal behavior.',gap=10)
assert y >= BOTTOM, ('last page',y)
footer()
c.save()

md = ['# Hadal Descent Algorithm Code Snippets Explained',
      'These are excerpts from the current Unity source, with indentation normalized. They rely on surrounding method variables. Figure numbers are local to this handout; renumber them to fit the manuscript.',
      'Perlin Noise builds the seafloor, placement rules organize species, and Context Steering guides movement.']
for f in figures:
    md += [f"**Figure {f['number']}**", f"*{f['title']}*", '```csharp\n'+f['code']+'\n```',
           f"Source: [{f['source']}]({f['url']})", f['explanation'],
           '**In simple terms:** '+f['simple'], '**Objective connection:** '+f['objective']]
md += ['## Notes for the defense',
       'The steering score is S = I x (1 - D)^2. With interest 0.8 and danger 0.2, the score is 0.512; with interest 0.8 and danger 0.7, it is 0.072. These illustrate the formula and are not measured gameplay results.',
       'The three techniques support the chosen procedural design. Perlin Noise and placement directly support Objective 2; Context Steering directly supports Objective 4. The Bestiary, researched information, assets, and progression systems also contribute to Objectives 1, 3, and 5.',
       'The code excerpts describe implementation. Enjoyment, content accuracy, and player understanding still need evaluation.',
       'Placement fallbacks and additional steering adjustments are outside some excerpts. Read the surrounding methods before claiming absolute guarantees.']
md_path.write_text('\n\n'.join(md)+'\n',encoding='utf-8')
manifest = [{k:v for k,v in f.items() if k not in ('image','aspect')} for f in figures]
(WORK/'source-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({'pdf':str(pdf_path),'markdown':str(md_path),'figures':len(figures),'pages':page_no},indent=2))
