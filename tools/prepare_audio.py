"""Build only Assets/Audio/*.wav and catalog.json from CC0 source WAV files.

Usage: python tools/prepare_audio.py --sources <download-directory>
Requires numpy. Downloads are deliberate and separate from builds; the app never
downloads audio. Source syllable boundaries are editable in art/audio/sources.json.
"""
import argparse
import hashlib
import json
from pathlib import Path
import wave
import numpy as np

ROOT = Path(__file__).resolve().parent.parent

def read_wave(path):
    with wave.open(str(path), "rb") as stream:
        rate, channels, width = stream.getframerate(), stream.getnchannels(), stream.getsampwidth()
        raw = stream.readframes(stream.getnframes())
    if width == 3:
        b = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        x = b[:, 0] + (b[:, 1] << 8) + (b[:, 2] << 16)
        x = np.where(x >= 8388608, x-16777216, x) / 8388608.0
    elif width == 2:
        x = np.frombuffer(raw, dtype="<i2") / 32768.0
    else:
        raise ValueError("Source must be 16/24 bit PCM")
    if rate != 48000:
        raise ValueError("Expected original 48 kHz recordings")
    return x.reshape(-1, channels).mean(axis=1), rate

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--sources", type=Path, required=True)
    args = parser.parse_args()
    spec = json.loads((ROOT / "art/audio/sources.json").read_text(encoding="utf-8"))
    output = ROOT / "src/DesktopCock/Assets/Audio"
    output.mkdir(parents=True, exist_ok=True)
    catalog = {"schemaVersion": 1, "clips": []}
    for clip in spec["clips"]:
        source = args.sources / f'{clip["recording"]}.wav'
        audio, rate = read_wave(source)
        audio = audio[round(clip["start"]*rate):round(clip["end"]*rate)].copy()
        audio -= audio.mean()
        # Gentle high-pass removes low room rumble; no pitch/time manipulation.
        spectrum = np.fft.rfft(audio)
        frequencies = np.fft.rfftfreq(len(audio), 1/rate)
        spectrum *= frequencies / np.sqrt(frequencies**2 + 180**2)
        audio = np.fft.irfft(spectrum, n=len(audio))
        rms = np.sqrt(np.mean(audio**2))
        audio *= min(.11/max(rms, 1e-9), .8/max(abs(audio).max(), 1e-9))
        fade = min(round(rate*.008), len(audio)//2)
        audio[:fade] *= np.linspace(0, 1, fade)
        audio[-fade:] *= np.linspace(1, 0, fade)
        path = output / f'{clip["id"]}.wav'
        with wave.open(str(path), "wb") as stream:
            stream.setparams((1, 2, rate, len(audio), "NONE", "not compressed"))
            stream.writeframes((audio*32767).round().astype("<i2").tobytes())
        duration = len(audio)/rate
        cues = [{"seconds": 0, "pose": "Closed"}]
        for start, end in clip["syllables"]:
            start, end = start-clip["start"], end-clip["start"]
            if not 0 <= start < end <= duration:
                raise ValueError(f'Bad syllable in {clip["id"]}')
            # Art-directed three-pose envelope; not runtime amplitude following.
            for second, pose in [(start,"Small"),(start+.025,"Open"),(end-.02,"Small"),(end,"Closed")]:
                cues.append({"seconds": round(second, 5), "pose": pose})
        catalog["clips"].append({
            "id": clip["id"], "kind": clip["kind"], "file": path.name,
            "duration": duration, "mouth": cues,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            "source": f'https://bigsoundbank.com/sound-{clip["recording"]}-perruche-calopsitte-{clip["number"]}.html',
            "author": spec["author"], "license": spec["license"],
            "sourceStart": clip["start"], "sourceEnd": clip["end"]})
        print(f'{clip["id"]}: {duration:.2f}s, {len(cues)} mouth cues')
    (output / "catalog.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")

if __name__ == "__main__":
    main()
