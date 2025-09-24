#!/usr/bin/env python3
"""
Download Whisper.cpp and models for VoiceLite
"""

import os
import sys
import urllib.request
import zipfile
import shutil
from pathlib import Path

def download_file(url, dest_path, description):
    """Download a file with progress indication"""
    print(f"\nDownloading {description}...")
    print(f"From: {url}")
    print(f"To: {dest_path}")

    try:
        # Download with progress
        def download_progress(block_num, block_size, total_size):
            downloaded = block_num * block_size
            percent = min(100, (downloaded / total_size) * 100) if total_size > 0 else 0
            bar_length = 40
            filled = int(bar_length * percent / 100)
            bar = '#' * filled + '-' * (bar_length - filled)
            sys.stdout.write(f'\r[{bar}] {percent:.1f}%')
            sys.stdout.flush()

        urllib.request.urlretrieve(url, dest_path, reporthook=download_progress)
        print(f"\n[OK] Successfully downloaded {description}")
        return True
    except Exception as e:
        print(f"\n[MISSING] Failed to download {description}: {e}")
        return False

def setup_whisper():
    """Main setup function"""
    print("=" * 50)
    print("    VoiceLite - Whisper Setup")
    print("=" * 50)

    # Create whisper directory
    whisper_dir = Path("VoiceLite/whisper")
    whisper_dir.mkdir(parents=True, exist_ok=True)
    print(f"[OK] Whisper directory ready: {whisper_dir.absolute()}")

    # Download options
    print("\nSetup Options:")
    print("1. Download Whisper binary (from GitHub)")
    print("2. Download small model (466 MB)")
    print("3. Download both (recommended)")
    print("4. Just show download links")

    choice = input("\nSelect option (1-4): ").strip()

    if choice in ["1", "3"]:
        print("\n" + "="*50)
        print("Downloading Whisper.cpp Binary")
        print("="*50)

        # Latest known working release
        whisper_url = "https://github.com/ggerganov/whisper.cpp/releases/download/v1.5.4/whisper-blas-bin-x64.zip"
        zip_path = whisper_dir / "whisper-bin.zip"

        if download_file(whisper_url, str(zip_path), "Whisper.cpp binary"):
            try:
                print("Extracting archive...")
                with zipfile.ZipFile(zip_path, 'r') as zip_ref:
                    zip_ref.extractall(whisper_dir)

                # Find and rename main.exe to whisper.exe
                for root, dirs, files in os.walk(whisper_dir):
                    for file in files:
                        if file == "main.exe":
                            src = Path(root) / file
                            dest = whisper_dir / "whisper.exe"
                            shutil.move(str(src), str(dest))
                            print(f"[OK] Renamed main.exe to whisper.exe")
                            break

                # Cleanup
                zip_path.unlink()
                print("[OK] Cleanup complete")

            except Exception as e:
                print(f"Error extracting: {e}")

    if choice in ["2", "3"]:
        print("\n" + "="*50)
        print("Downloading Whisper Model")
        print("="*50)

        model_url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin"
        model_path = whisper_dir / "ggml-small.bin"

        if model_path.exists():
            print(f"Model already exists at {model_path}")
            overwrite = input("Overwrite? (y/N): ").lower() == 'y'
            if not overwrite:
                print("Skipping model download")
            else:
                download_file(model_url, str(model_path), "ggml-small.bin (466 MB)")
        else:
            download_file(model_url, str(model_path), "ggml-small.bin (466 MB)")

    if choice == "4":
        print("\n" + "="*50)
        print("Manual Download Links")
        print("="*50)
        print("\nWhisper.cpp Binary:")
        print("https://github.com/ggerganov/whisper.cpp/releases")
        print("-> Download whisper-bin-x64.zip")
        print("-> Extract and rename main.exe to whisper.exe")
        print(f"-> Place in: {(whisper_dir / 'whisper.exe').absolute()}")

        print("\nWhisper Model (ggml-small.bin):")
        print("https://huggingface.co/ggerganov/whisper.cpp/blob/main/ggml-small.bin")
        print("-> Click the download button")
        print(f"-> Place in: {(whisper_dir / 'ggml-small.bin').absolute()}")

    # Verify installation
    print("\n" + "="*50)
    print("Verification")
    print("="*50)

    whisper_exe = whisper_dir / "whisper.exe"
    model_file = whisper_dir / "ggml-small.bin"

    if whisper_exe.exists():
        print(f"[OK] whisper.exe found ({whisper_exe.stat().st_size / 1024 / 1024:.1f} MB)")
    else:
        print("[MISSING] whisper.exe NOT found")

    if model_file.exists():
        print(f"[OK] ggml-small.bin found ({model_file.stat().st_size / 1024 / 1024:.1f} MB)")
    else:
        print("[MISSING] ggml-small.bin NOT found")

    print("\n" + "="*50)
    if whisper_exe.exists() and model_file.exists():
        print("[OK] Setup complete! You can now run the tests.")
        print("\nRun: dotnet test VoiceLite.Tests")
    else:
        print("[WARNING] Setup incomplete. Please download missing files.")
    print("="*50)

if __name__ == "__main__":
    try:
        setup_whisper()
    except KeyboardInterrupt:
        print("\n\nSetup cancelled by user")
        sys.exit(1)
    except Exception as e:
        print(f"\nError: {e}")
        sys.exit(1)