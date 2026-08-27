"""Ploofy'nin geri bildirim seslerini üretir.

Uygulama hiçbir görsel varlık taşımıyor — şekiller, yapboz resmi, avatarlar
hepsi koddan çiziliyor. Sesler de aynı yolu izliyor: burada tohumdan
sentezleniyorlar, depoda kaynak olarak bu betik duruyor. Sebebi lisans:
hazır ses bankaları çocuk uygulamalarında atıf ve ticari kullanım şartı
getiriyor, üretilmiş bir dalga getirmiyor.

Çıktı: src/Ploofy.App/Resources/Raw/sounds/*.wav
Çalıştırma: python tools/build_sounds.py
"""
import math
import struct
import wave
from pathlib import Path

SAMPLE_RATE = 44100
OUT_DIR = Path(__file__).resolve().parent.parent / "src" / "Ploofy.App" / "Resources" / "Raw" / "sounds"

# --- Tını sözlüğü -----------------------------------------------------------
# Her tını, temel frekansın katları olarak (oran, genlik, sönüm sabiti) üçlüleri.
# Sönüm sabiti saniye cinsinden: genlik exp(-t/tau) ile iniyor. Üst harmonikler
# daha hızlı sönerse vuruş "tahta", yavaş sönerse "çan" duyuluyor.
#
# 4.0 ve 9.8 katları marimbanın gerçek kip oranları; tam sayı katları
# kullanıldığında ses org gibi çıkıyor, çocuk kulağına fazla elektronik.
VOICES = {
    "marimba": [(1.0, 1.00, 0.17), (4.0, 0.30, 0.09), (9.8, 0.09, 0.05)],
    "bell":    [(1.0, 1.00, 0.34), (2.7, 0.42, 0.20), (5.4, 0.18, 0.12), (8.9, 0.07, 0.07)],
    "wood":    [(1.0, 1.00, 0.07), (3.1, 0.45, 0.04), (6.3, 0.16, 0.02)],
    "flute":   [(1.0, 1.00, 0.26), (2.0, 0.14, 0.18), (3.0, 0.05, 0.12)],
    "thud":    [(1.0, 1.00, 0.11), (2.0, 0.22, 0.06)],
}

# Vuruşun başındaki tırmanma. Sıfırdan başlamayan bir dalga hoparlörde
# "tık" diye duyuluyor; 4 ms bunu siliyor ama vuruşu yumuşatmıyor.
ATTACK = 0.004


def note(buf, start, freq, voice, gain=1.0, attack=ATTACK):
    """Tampona tek bir nota ekler (üzerine toplayarak)."""
    partials = VOICES[voice]
    longest = max(tau for _, _, tau in partials)
    # exp(-3.2) ≈ -28 dB: kuyruk duyulmaz olmuş ama kesik de değil. Uzun
    # kuyruk bu oyunda zarar veriyor — art arda gelen iki yıldız sesinden
    # ikincisi birincisini kesiyor ve kesilen ses tıkırdıyor.
    length = int(SAMPLE_RATE * (longest * 3.2 + attack))
    offset = int(start * SAMPLE_RATE)

    for i in range(length):
        t = i / SAMPLE_RATE
        env = min(1.0, t / attack) if attack > 0 else 1.0
        value = 0.0
        for ratio, amp, tau in partials:
            value += amp * math.exp(-t / tau) * math.sin(2 * math.pi * freq * ratio * t)
        index = offset + i
        while index >= len(buf):
            buf.append(0.0)
        buf[index] += value * env * gain


def write(name, buf, peak=0.72):
    """Tepe değerine göre normalleyip 16 bit mono WAV olarak yazar."""
    # Son 5 ms'de sıfıra in: kuyruğu ortadan kesmek de "tık" yapıyor.
    tail = int(SAMPLE_RATE * 0.005)
    for i in range(min(tail, len(buf))):
        buf[len(buf) - 1 - i] *= i / tail

    top = max(abs(v) for v in buf) or 1.0
    scale = peak / top

    frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, v * scale)) * 32767)) for v in buf)
    path = OUT_DIR / name
    with wave.open(str(path), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SAMPLE_RATE)
        f.writeframes(frames)
    print(f"{path.name}: {len(buf) / SAMPLE_RATE:.2f} sn, {len(frames) // 1024} KB")


# --- Nota isimleri ----------------------------------------------------------
# Bütün sesler do majör pentatonik içinde kalıyor: hangi ikisi üst üste
# binerse binsin uyumsuz bir aralık çıkmıyor. Bir çocuk oyununda sesler
# sık sık çakışıyor (yıldız + tur sonu, dokunuş + doğru).
C5, D5, E5, G5, A5 = 523.25, 587.33, 659.25, 783.99, 880.00
C6, D6, E6, G6, A6 = 1046.50, 1174.66, 1318.51, 1567.98, 1760.00
C7, E7 = 2093.00, 2637.02
G3, C4, E4 = 196.00, 261.63, 329.63


def build():
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    # Dokunuş: kartın çevrilmesi, parçanın tutulması. Saniyede birkaç kez
    # çalabiliyor, o yüzden kısa ve alçak — yoksa oyun takırdıyor.
    buf = []
    note(buf, 0.0, C6, "wood", 0.9)
    write("tap.wav", buf, peak=0.45)

    # Doğru: iki nota yukarı. Yükselen aralık bu yaşta "oldu" diye okunuyor.
    buf = []
    note(buf, 0.00, G5, "marimba", 1.0)
    note(buf, 0.09, D6, "marimba", 0.95)
    write("correct.wav", buf)

    # Tekrar dene: iki nota aşağı ama yumuşak bir tınıyla ve alçaktan.
    # "Kaybettin" değil "bir daha" tonu; Filiz ve Fidan bantlarında yanlışın
    # zaten cezası yok, ses de ceza gibi duyulmamalı.
    buf = []
    note(buf, 0.00, A5, "flute", 0.9, attack=0.02)
    note(buf, 0.13, E5, "flute", 0.8, attack=0.02)
    write("retry.wav", buf, peak=0.40)

    # Tur tamamlandı: dört notalık çıkan arpej, sonuncusu daha tok.
    buf = []
    for i, f in enumerate((C5, E5, G5, C6)):
        note(buf, i * 0.10, f, "marimba", 1.0 if i < 3 else 1.15)
    write("round_complete.wav", buf)

    # Yıldız: üç hızlı yüksek çan — parıltı. Sonuç ekranında yıldızlar
    # arka arkaya düşüyor, sesin de art arda binmesi gerekiyor.
    buf = []
    for i, f in enumerate((G6, C7, E7)):
        note(buf, i * 0.06, f, "bell", 0.9 - i * 0.12)
    write("star.wav", buf, peak=0.55)

    # Devir: nötr, "sıra sende". Ne kutlama ne uyarı; yumuşak iki nota.
    buf = []
    note(buf, 0.00, E6, "flute", 1.0, attack=0.03)
    note(buf, 0.16, A6, "flute", 0.9, attack=0.03)
    write("handoff.wav", buf, peak=0.50)

    # Kilitli: kısa, alçak, tok bir tık. Çocuk kilidi kendi açamıyor;
    # ses "olmadı" demeli, azarlamamalı.
    buf = []
    note(buf, 0.0, G3, "thud", 1.0)
    write("locked.wav", buf, peak=0.42)

    # Sırayı Tekrarla'nın altı tuşu. Klasik oyunun asıl işi burada: her tuşun
    # kendi notası olunca dizi kulakla da hatırlanıyor ve gösterim bir ezgiye
    # dönüşüyor. Pentatonik olduğu için dizi hangi sırada çıkarsa çıksın
    # kulağa melodi gibi geliyor — sıralı seçilmiş yedi ses arasında bunu
    # sağlayan tek dizi bu.
    for index, freq in enumerate((C5, D5, E5, G5, A5, C6), start=1):
        buf = []
        note(buf, 0.0, freq, "marimba", 1.0)
        write(f"pad{index}.wav", buf, peak=0.60)


if __name__ == "__main__":
    build()
