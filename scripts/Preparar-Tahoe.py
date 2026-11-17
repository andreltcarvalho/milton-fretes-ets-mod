"""Monta um pacote de trabalho Milton Fretes + Tahoe sem alterar os originais."""
from pathlib import Path, PurePosixPath
from io import BytesIO
import hashlib
import re
import struct
import zipfile

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT / 'Estudos Milton Fretes/caneca/Milton Fretes - caneca - teste.scs'
TAHOE = Path.home() / 'Documents/Euro Truck Simulator 2/mod/Chevrolet_Tahoe_2007_V4.2_1.62.scs'
ART = ROOT / 'Estudos Milton Fretes/tahoe/tahoe-pintura-estudo.png'
OUTPUT = ROOT / 'Estudos Milton Fretes/tahoe/Milton Fretes - Tahoe - teste v2.scs'
HASHES = {
    BASE: 'd5c516769fc3caf73a8d4b8b1b0354acd803ffcf659460c2966880338973ccac',
    TAHOE: 'f843bd89fc6cf08edf5e73a5ee77eda3e17a602d2d8ec229acdf8b4712817cc7',
}
VEHICLE = 'def/vehicle/car/chevrolet.tahoe_2007/'
DEALERS = (
    'def/vehicle/truck_dealer/chevrolet/chevrolet.tahoe_2007.sii',
    'def/vehicle/car_dealer/chevrolet/chevrolet.tahoe_2007.sii',
)
PAINT = VEHICLE + 'paint_job/milton.sii'
TEXTURE = 'vehicle/truck/upgrade/milton_tahoe/pintura.dds'
TOBJ = 'vehicle/truck/upgrade/milton_tahoe/pintura.tobj'
SHARED = {
    'def/vehicle/f_tire/sub.sii': ('ftire', 'sub'),
    'def/vehicle/r_tire/sub.sii': ('rtire', 'sub'),
    'def/vehicle/f_disc/empty.sii': ('fdisc', 'empty'),
    'def/vehicle/r_disc/empty.sii': ('rdisc', 'empty'),
    'def/vehicle/f_hub/empty.sii': ('fhub', 'empty'),
    'def/vehicle/r_hub/empty.sii': ('rhub', 'empty'),
    'def/vehicle/f_nuts/empty.sii': ('fnuts', 'empty'),
    'def/vehicle/r_nuts/empty.sii': ('rnuts', 'empty'),
}


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def entries(archive):
    result = {}
    for entry in archive.infolist():
        if entry.is_dir():
            continue
        name = entry.filename.replace('\\', '/')
        parts = PurePosixPath(name).parts
        if name.startswith('/') or not parts or any(p in ('.', '..') for p in parts) or ':' in name:
            raise ValueError(f'Caminho inválido no pacote: {name}')
        key = name.lower()
        if key in result:
            raise ValueError(f'Arquivo duplicado no pacote: {name}')
        result[key] = entry
    return result


def price_zero(content, amount):
    text = content.decode('utf-8-sig').replace('\r\n', '\n').replace('\r', '\n')
    text, n = re.subn(r'(?m)^(\s*price\s*:\s*)\d+', rf'\g<1>{amount}', text)
    if n != 1:
        raise ValueError('A definição precisa conter exatamente um preço')
    return text.encode('utf-8')


def main():
    if OUTPUT.exists():
        raise FileExistsError(f'O arquivo de saída já existe: {OUTPUT}')
    for path, expected in HASHES.items():
        if digest(path) != expected:
            raise ValueError(f'O pacote de origem mudou: {path}')
    with zipfile.ZipFile(BASE) as base, zipfile.ZipFile(TAHOE) as tahoe:
        base_files, car_files = entries(base), entries(tahoe)
        overlaps = set(base_files) & set(car_files)
        if overlaps != {'manifest.sii', 'mod_icon.jpg', 'mod_description.txt'}:
            raise ValueError(f'Conflito inesperado entre pacotes: {sorted(overlaps)}')
        additions = {}
        replacements = {}

        # A oferta padrão tem componentes próprios da Tahoe e componentes
        # compartilhados. Os compartilhados recebem cópias com nomes exclusivos.
        dealer = tahoe.read(car_files[DEALERS[0]])
        paths = re.findall(rb'data_path\s*:\s*"/([^"]+)"', dealer)
        for raw in paths:
            name = raw.decode('ascii')
            if name == VEHICLE + 'data.sii':
                continue
            if name.startswith(VEHICLE):
                replacements[name] = price_zero(tahoe.read(car_files[name]), 1 if name.endswith('/chassis/rwd.sii') else 0)
            elif name in SHARED:
                kind, old = SHARED[name]
                alias = str(PurePosixPath(name).with_name('mftahoe.sii'))
                blob = price_zero(tahoe.read(car_files[name]), 0)
                blob, count = re.subn(rb'(:\s*)' + old.encode() + rb'\.' + kind.encode() + rb'\b', rb'\1mftahoe.' + kind.encode(), blob)
                if count != 1:
                    raise ValueError(f'Nome inesperado da peça: {name}')
                additions[alias] = blob
            else:
                raise ValueError(f'Peça da oferta não reconhecida: {name}')

        paint = tahoe.read(car_files[VEHICLE + 'paint_job/skin1.sii']).decode('utf-8-sig').replace('\r', '')
        paint = re.sub(r'accessory_paint_job_data\s*:\s*[^\s{]+', 'accessory_paint_job_data : milton.tahoe.paint_job', paint, count=1)
        paint = re.sub(r'name\s*:\s*"[^"]+"', 'name: "Milton Fretes - Escolta"', paint, count=1)
        paint = re.sub(r'price\s*:\s*\d+', 'price: 0', paint, count=1)
        paint = re.sub(r'paint_job_mask\s*:\s*"[^"]+"', f'paint_job_mask: "/{TOBJ}"', paint, count=1)
        additions[PAINT] = paint.encode('utf-8')
        for name in DEALERS:
            data = tahoe.read(car_files[name])
            for old in SHARED:
                new = str(PurePosixPath(old).with_name('mftahoe.sii'))
                data = data.replace(('/' + old).encode(), ('/' + new).encode())
            old_paint = ('/' + VEHICLE + 'paint_job/color3.sii').encode()
            if data.count(old_paint) != 1:
                raise ValueError(f'Pintura original não encontrada: {name}')
            replacements[name] = data.replace(old_paint, ('/' + PAINT).encode())

        image = Image.open(ART).convert('RGBA')
        if image.size != (2048, 2048):
            raise ValueError('Dimensões inesperadas da pintura')
        encoded = BytesIO()
        image.save(encoded, format='DDS')
        additions[TEXTURE] = encoded.getvalue()
        original = tahoe.read(car_files['vehicle/truck/upgrade/tahoe_2007_skin/skin1.tobj'])
        old_path = b'/vehicle/truck/upgrade/tahoe_2007_skin/hwy_patrol.dds'
        if len(original) != 48 + len(old_path) or original[48:] != old_path or struct.unpack_from('<I', original, 40)[0] != len(old_path):
            raise ValueError('Formato inesperado do descritor de textura')
        new_path = ('/' + TEXTURE).encode('ascii')
        tobj = bytearray(original[:48])
        struct.pack_into('<I', tobj, 40, len(new_path))
        additions[TOBJ] = bytes(tobj) + new_path

        reserved = set(base_files) | set(car_files)
        if reserved.intersection(additions):
            raise ValueError('Novo arquivo entraria em conflito com um dos pacotes')
        manifest = base.read(base_files['manifest.sii'])
        if manifest.count(b'package_version: "1.13.1"') != 1:
            raise ValueError('Versão inesperada do pacote Milton Fretes')
        manifest = manifest.replace(b'package_version: "1.13.1"', b'package_version: "1.14.0"')
        with zipfile.ZipFile(OUTPUT, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as dest:
            for name, entry in base_files.items():
                data = manifest if name == 'manifest.sii' else base.read(entry)
                if name == 'mod_description.txt':
                    data += b'\n\nChevrolet Tahoe 2007: Nimit, Elaman, obelihnio e Void.\n'
                dest.writestr(entry.filename.replace('\\', '/'), data)
            for name, entry in car_files.items():
                if name not in overlaps:
                    dest.writestr(entry.filename.replace('\\', '/'), replacements[name] if name in replacements else tahoe.read(entry))
            for name, data in additions.items():
                dest.writestr(name, data)
    print(f'Pacote: {OUTPUT}\nSHA-256: {digest(OUTPUT)}')


if __name__ == '__main__':
    main()
