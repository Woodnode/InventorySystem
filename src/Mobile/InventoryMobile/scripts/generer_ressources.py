"""Régénère les polices de l'app mobile à partir de celles du front web.

- Poppins (titres) et Open Sans (texte), paquets @fontsource du front, sous-ensemble latin,
  convertis en TTF : la même typographie que le web.
- Icônes Phosphor (paquet @phosphor-icons/web) réduites aux glyphes de Resources/Icons.cs.
- Icône de l'app et écran de démarrage : le carton Phosphor (glyphe « package », celui de
  l'onglet Produits du web) en blanc sur le bleu de marque #184e77.

Dépendances : fonttools et Pillow (pip install fonttools pillow).

Usage, depuis la racine du dépôt, après `npm install` dans src/Frontend :
    npm pack @phosphor-icons/web@2.1.2 && tar -xzf phosphor-icons-web-2.1.2.tgz
    python src/Mobile/InventoryMobile/scripts/generer_ressources.py --phosphor package
"""

import argparse
import re
from pathlib import Path

from fontTools.subset import Options, Subsetter
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[4]
FONTSOURCE = ROOT / "src/Frontend/node_modules/@fontsource"
MOBILE = ROOT / "src/Mobile/InventoryMobile"
FONTS = MOBILE / "Resources/Fonts"

# (paquet @fontsource, préfixe du fichier, graisse, nom du TTF)
TEXT_FONTS = [
    ("open-sans", "open-sans", 400, "OpenSans-Regular"),
    ("open-sans", "open-sans", 600, "OpenSans-SemiBold"),
    ("open-sans", "open-sans", 700, "OpenSans-Bold"),
    ("poppins", "poppins", 600, "Poppins-SemiBold"),
    ("poppins", "poppins", 700, "Poppins-Bold"),
]

PHOSPHOR_STYLES = {"regular/Phosphor.ttf": "Phosphor-Regular", "bold/Phosphor-Bold.ttf": "Phosphor-Bold",
                   "fill/Phosphor-Fill.ttf": "Phosphor-Fill"}


def generate_text_fonts() -> None:
    for package, prefix, weight, name in TEXT_FONTS:
        font = TTFont(FONTSOURCE / package / "files" / f"{prefix}-latin-{weight}-normal.woff")
        font.flavor = None
        font.save(FONTS / f"{name}.ttf")


def generate_phosphor(package: Path) -> None:
    icons = (MOBILE / "Resources/Icons.cs").read_text(encoding="utf-8")
    codepoints = [int(code, 16) for code in re.findall(r'"\\u([0-9a-fA-F]{4})"', icons)]
    for source, name in PHOSPHOR_STYLES.items():
        font = TTFont(package / "src" / source)
        options = Options()
        options.hinting = False
        options.drop_tables += ["GSUB", "GPOS"]
        subsetter = Subsetter(options)
        subsetter.populate(unicodes=codepoints)
        subsetter.subset(font)
        font.save(FONTS / f"{name}.ttf")


def generate_app_icon(package: Path) -> None:
    # Le web n'a pas encore d'icône propre (favicon Vite par défaut) : l'icône reprend le
    # glyphe des produits, en blanc sur le bleu de marque. Android ne montre d'une icône
    # adaptative qu'un disque central de 66 % : le glyphe occupe 46 % du calque.
    font = ImageFont.truetype(str(package / "src/bold/Phosphor-Bold.ttf"), 1000)
    glyph = chr(0xE390)
    for path, size, share in ((MOBILE / "Resources/AppIcon/appiconfg.png", 1024, 0.46),
                              (MOBILE / "Resources/Splash/splash.png", 512, 0.9)):
        canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        mark = Image.new("RGBA", (1200, 1200), (0, 0, 0, 0))
        ImageDraw.Draw(mark).text((100, 100), glyph, font=font, fill="white")
        mark = mark.crop(mark.getchannel("A").getbbox())
        target = int(size * share)
        mark = mark.resize((target, int(target * mark.height / mark.width)), Image.LANCZOS)
        canvas.alpha_composite(mark, ((size - mark.width) // 2, (size - mark.height) // 2))
        path.parent.mkdir(parents=True, exist_ok=True)
        canvas.save(path, optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--phosphor", type=Path, help="dossier « package » extrait de @phosphor-icons/web")
    args = parser.parse_args()

    FONTS.mkdir(parents=True, exist_ok=True)
    generate_text_fonts()
    if args.phosphor:
        generate_phosphor(args.phosphor)
        generate_app_icon(args.phosphor)


if __name__ == "__main__":
    main()
