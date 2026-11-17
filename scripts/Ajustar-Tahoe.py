"""Gera uma cópia de trabalho da Tahoe com faixas laterais."""
from pathlib import Path
from io import BytesIO
import hashlib
import zipfile
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
STUDY = ROOT / 'Estudos Milton Fretes/tahoe'
SOURCE = STUDY / 'Milton Fretes - Tahoe - teste v2.scs'
OUTPUT = STUDY / 'Milton Fretes - Tahoe - faixas.scs'
SOURCE_HASH = '7fce7067eb447f155f7fa57aeb8f03dda2dd47461f405dfb2e4808a73e7c52b8'
PAINT = 'vehicle/truck/upgrade/milton_tahoe/pintura.dds'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    if OUTPUT.exists():
        raise FileExistsError(f'O arquivo de saída já existe: {OUTPUT}')
    if sha(SOURCE) != SOURCE_HASH:
        raise ValueError('O pacote de origem não corresponde à versão esperada')
    img = Image.open(STUDY / 'tahoe-pintura-faixas.png').convert('RGBA')
    if img.size != (2048, 2048):
        raise ValueError('Tamanho inesperado da textura')
    data = BytesIO()
    img.save(data, format='DDS')
    with zipfile.ZipFile(SOURCE) as src:
        with zipfile.ZipFile(OUTPUT, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as dst:
            for item in src.infolist():
                if item.is_dir():
                    continue
                name = item.filename
                content = src.read(item)
                if name == 'manifest.sii':
                    if content.count(b'package_version: "1.14.0"') != 1:
                        raise ValueError('Versão inesperada do manifesto')
                    content = content.replace(b'package_version: "1.14.0"', b'package_version: "1.14.2"')
                elif name == PAINT:
                    content = data.getvalue()
                dst.writestr(item, content)
    print(f'Pacote: {OUTPUT}\nSHA-256: {sha(OUTPUT)}')


if __name__ == '__main__':
    main()
