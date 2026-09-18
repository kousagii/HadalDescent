import os, glob

folder = r"c:\Unity\HadalDescent\Assets\Resources\UI\UpgradeLogos"
for meta in glob.glob(os.path.join(folder, "*.png.meta")):
    with open(meta, "r", encoding="utf-8") as f:
        content = f.read()
    
    content = content.replace("textureType: 0", "textureType: 8")
    content = content.replace("spriteMode: 0", "spriteMode: 1")
    content = content.replace("alphaIsTransparency: 0", "alphaIsTransparency: 1")
    content = content.replace("enableMipMap: 1", "enableMipMap: 0")
    content = content.replace("nPOTScale: 1", "nPOTScale: 0")
    
    with open(meta, "w", encoding="utf-8") as f:
        f.write(content)
    print("Updated", os.path.basename(meta))
