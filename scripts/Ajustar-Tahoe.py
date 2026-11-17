"""Gera uma cópia de trabalho com faixas laterais e sinalizador na Tahoe."""
from pathlib import Path
from io import BytesIO
import hashlib
import re
import zipfile
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
STUDY = ROOT / 'Estudos Milton Fretes/tahoe'
SOURCE = STUDY / 'Milton Fretes - Tahoe - teste v2.scs'
OUTPUT = STUDY / 'Milton Fretes - Tahoe - faixas e sinalizador.scs'
CACHE = Path(r'C:\Users\Andre\.codex\visualizations\2026\11\17\01a1e9aa-4db1-7be1-934f-b879f4bcdf55\estudo-caneca\conversion\rsrc\base\@cache')
SOURCE_HASH = '7fce7067eb447f155f7fa57aeb8f03dda2dd47461f405dfb2e4808a73e7c52b8'
PAINT = 'vehicle/truck/upgrade/milton_tahoe/pintura.dds'
BASE = 'vehicle/truck/upgrade/milton_tahoe/sinalizador/'
BEACON_DEF = 'def/vehicle/car/chevrolet.tahoe_2007/accessory/far_beacon/milton.sii'
CAR = 'def/vehicle/car/chevrolet.tahoe_2007/data.sii'
DEALERS = ('def/vehicle/truck_dealer/chevrolet/chevrolet.tahoe_2007.sii',
           'def/vehicle/car_dealer/chevrolet/chevrolet.tahoe_2007.sii')


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
    beacon_pmd = (CACHE / BASE / 'sinalizador.pmd').read_bytes()
    material = re.findall(rb'/automat/[a-z0-9/]+\.mat', beacon_pmd)
    if len(material) != 1:
        raise ValueError('Material do sinalizador não identificado')
    material_path = material[0].decode('ascii').lstrip('/')
    extras = {BEACON_DEF: (STUDY / 'sinalizador-milton.sii').read_bytes(),
              material_path: (CACHE / material_path).read_bytes()}
    for suffix in ('pmd', 'pmg', 'tobj', 'dds'):
        name = BASE + 'sinalizador.' + suffix
        extras[name] = (CACHE / name).read_bytes()
    with zipfile.ZipFile(SOURCE) as src:
        names = {i.filename for i in src.infolist() if not i.is_dir()}
        if set(extras) & names:
            raise ValueError('Os arquivos novos colidem com o pacote anterior')
        with zipfile.ZipFile(OUTPUT, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as dst:
            for item in src.infolist():
                if item.is_dir():
                    continue
                name = item.filename
                content = src.read(item)
                if name == 'manifest.sii':
                    if content.count(b'package_version: "1.14.0"') != 1:
                        raise ValueError('Versão inesperada do manifesto')
                    content = content.replace(b'package_version: "1.14.0"', b'package_version: "1.14.1"')
                elif name == PAINT:
                    content = data.getvalue()
                elif name == CAR:
                    anchor = b'\trequire[]: logo'
                    if content.count(anchor) != 1:
                        raise ValueError('Definição principal da Tahoe mudou')
                    content = content.replace(anchor, anchor + b'\n\trequire[]: far_beacon\n\tdefaults[]: "/' + BEACON_DEF.encode() + b'"')
                elif name in DEALERS:
                    line = b' accessories[]: .win'
                    if content.count(line) != 1:
                        raise ValueError(f'Definição da concessionária mudou: {name}')
                    content = content.replace(line, line + b'\n accessories[]: .milton_beacon')
                    end = content.rfind(b'}')
                    if end < 0:
                        raise ValueError('Arquivo de concessionária inválido')
                    add = b'\nvehicle_addon_accessory : .milton_beacon {\n data_path: "/' + BEACON_DEF.encode() + b'"\n}\n'
                    content = content[:end] + add + content[end:]
                dst.writestr(item, content)
            for name, value in extras.items():
                dst.writestr(name, value)
    print(f'Pacote: {OUTPUT}\nSHA-256: {sha(OUTPUT)}')


if __name__ == '__main__':
    main()
