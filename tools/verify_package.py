"""Üretilmiş Android paketinin içinde gerçekten ne olduğuna bakar.

    python tools/verify_package.py <apk> <canary> <aranan> [<aranan> ...]

Örnek:

    python tools/verify_package.py \\
        src/Ploofy.App/bin/Release/net10.0-android/publish/io.ploofy.app-Signed.apk \\
        GameCatalog AdaptiveDifficulty "↑ Zorlu" "↑ Schwerer" "↑ Harder"

Neden var
---------
Paketin "güncel kodu taşıdığı" tarih damgasından anlaşılmıyor: artımlı bir
Release publish, eski ara çıktıları yeniden kullanabiliyor ve kimse fark
etmiyor. Doğrudan pakete bakmak gerekiyor.

Ama pakete bakmanın da iki tuzağı var ve ikisi de sessiz:

1. **Derlemeler sıkıştırılmış.** `libassembly-store.so` içindeki yönetilen
   derlemelerin çoğu LZ4 blok sıkıştırmalı (`XALZ` başlığı). Ham arama
   onları göremiyor ve cevap "bulunamadı" çıkıyor — yani *arama başarısız
   olduğu hâlde sonuç 'yok' gibi görünüyor.* Bu betik blokları açıyor.
2. **.NET dizeleri UTF-16.** ASCII tarayan bir arama metin sabitlerini
   kaçırıyor. Burada iki kodlama da deneniyor.

Bu yüzden **canary zorunlu**: bulunması kesin olan bir ad veriliyor
(ör. `GameCatalog`). Canary bulunamazsa betik arananları hiç raporlamadan
hata veriyor — çünkü o durumda arızalı olan arama, aranan değil.

Çıkış kodu: hepsi bulunduysa 0, biri bile eksikse 1.
"""
import struct
import sys
import zipfile


def lz4_block(src: bytes, want: int) -> bytes:
    """LZ4 blok (frame değil) açıcı — `want` bayta ulaşınca duruyor."""
    out = bytearray()
    i, n = 0, len(src)

    while i < n and len(out) < want:
        token = src[i]
        i += 1

        literals = token >> 4
        if literals == 15:
            while True:
                b = src[i]
                i += 1
                literals += b
                if b != 255:
                    break

        out += src[i:i + literals]
        i += literals

        # Son sözcük yalnızca sabit taşır; eşleşme yok.
        if i + 2 > n or len(out) >= want:
            break

        offset = src[i] | (src[i + 1] << 8)
        i += 2
        if offset == 0 or offset > len(out):
            break

        match = token & 0xF
        if match == 15:
            while True:
                b = src[i]
                i += 1
                match += b
                if b != 255:
                    break
        match += 4

        # Örtüşen kopya: kaynak yazarken uzuyor, dilim alınamaz.
        start = len(out) - offset
        for k in range(match):
            out.append(out[start + k])

    return bytes(out)


def unpacked(store: bytes) -> bytes:
    """Store'un aranabilir hâli: açılmış bloklar + ham gövde."""
    marks = []
    p = store.find(b"XALZ")
    while p >= 0:
        marks.append(p)
        p = store.find(b"XALZ", p + 4)

    parts = []
    for j, p in enumerate(marks):
        _, _, want = struct.unpack_from("<4sII", store, p)
        end = marks[j + 1] if j + 1 < len(marks) else len(store)
        parts.append(lz4_block(store[p + 12:end], want))

    # Sıkıştırılmamış derlemeler zaten burada duruyor.
    parts.append(store)
    return b"\x00".join(parts)


def stores(apk_path: str):
    """APK içindeki her ABI'nin assembly store'u."""
    with zipfile.ZipFile(apk_path) as apk:
        names = [n for n in apk.namelist() if n.endswith("libassembly-store.so")]
        if not names:
            sys.exit(f"{apk_path}: libassembly-store.so yok — bu bir .NET Android paketi mi?")
        for name in sorted(names):
            yield name.split("/")[1], apk.read(name)


def main(argv):
    if len(argv) < 4:
        sys.exit(__doc__)

    apk_path, canary, needles = argv[1], argv[2], argv[3:]
    missing = 0

    for abi, store in stores(apk_path):
        hay = unpacked(store)
        print(f"--- {abi}: {len(store):,} bayt sıkışık, {len(hay):,} bayt açık ---")

        def found(text):
            return (hay.find(text.encode("utf-8")) >= 0
                    or hay.find(text.encode("utf-16-le")) >= 0)

        if not found(canary):
            sys.exit(f"ARAMA BOZUK: canary '{canary}' bulunamadı — sonuçlara güvenme.")

        for needle in needles:
            ok = found(needle)
            missing += not ok
            print(f"  {needle:<28} {'VAR' if ok else 'YOK'}")

    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
