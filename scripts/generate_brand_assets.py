from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageFont


def font(size: int, bold: bool = False):
    candidates = [
        Path(r"C:\Windows\Fonts\arialbd.ttf" if bold else r"C:\Windows\Fonts\arial.ttf"),
        Path("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def icon(size: int) -> Image.Image:
    im = Image.new("RGBA", (size, size), (20, 90, 56, 255))
    d = ImageDraw.Draw(im)
    s = size / 512.0

    def xy(values):
        return tuple(int(round(v * s)) for v in values)

    # Monitor.
    d.rounded_rectangle(xy((78, 104, 434, 342)), radius=int(28*s),
                        outline="white", width=max(2, int(30*s)))
    # Stand.
    d.line([xy((256, 344)), xy((256, 408))], fill="white",
           width=max(2, int(30*s)))
    d.line([xy((176, 408)), xy((336, 408))], fill="white",
           width=max(2, int(30*s)))
    # Bidirectional remote-control arrows.
    orange = (245, 158, 11, 255)
    d.line([xy((160, 224)), xy((352, 224))], fill=orange,
           width=max(2, int(34*s)))
    d.line([xy((207, 176)), xy((159, 224)), xy((207, 272))],
           fill=orange, width=max(2, int(28*s)), joint="curve")
    d.line([xy((305, 176)), xy((353, 224)), xy((305, 272))],
           fill=orange, width=max(2, int(28*s)), joint="curve")
    return im


def wordmark(width: int, height: int, text_color):
    im = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    logo = icon(height - 40)
    im.alpha_composite(logo, (20, 20))
    d = ImageDraw.Draw(im)
    f = font(70, bold=True)
    d.text((height + 30, height // 2), "AMAZON ZL REMOTE",
           font=f, fill=text_color, anchor="lm")
    return im


def main():
    root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    assets = root / "flutter" / "assets"
    resources = root / "flutter" / "windows" / "runner" / "resources"
    assets.mkdir(parents=True, exist_ok=True)
    resources.mkdir(parents=True, exist_ok=True)

    icon(512).save(assets / "icon.png")
    icon(256).save(resources / "app_icon.ico",
                   sizes=[(256,256), (128,128), (64,64), (48,48), (32,32), (16,16)])

    wordmark(1200, 240, (20, 90, 56, 255)).save(assets / "logo.png")
    wordmark(1200, 240, (20, 90, 56, 255)).save(assets / "logo_light.png")
    wordmark(1200, 240, (255, 255, 255, 255)).save(assets / "logo_dark.png")

    print("Amazon ZL Remote brand assets generated.")


if __name__ == "__main__":
    main()
