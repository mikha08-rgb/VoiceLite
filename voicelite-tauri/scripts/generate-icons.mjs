#!/usr/bin/env node
/**
 * Generate VoiceLite app icons from SVG template.
 * Requires: sharp (available via sharp-cli or npm install sharp)
 *
 * Usage: node scripts/generate-icons.mjs
 */
import sharp from "sharp";
import { mkdirSync, writeFileSync } from "fs";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, "..");
const ICONS_DIR = join(ROOT, "src-tauri", "icons");
const RESOURCES_DIR = join(ROOT, "src-tauri", "resources");

// VoiceLite icon: microphone on rounded pink background
function makeSvg(size, { rounded = true, padding = 0.15 } = {}) {
  const r = rounded ? size * 0.22 : 0;
  const pad = size * padding;
  const micW = (size - pad * 2) * 0.35;
  const micH = (size - pad * 2) * 0.45;
  const cx = size / 2;
  const micTop = size * 0.18;
  const micRx = micW / 2;
  const micRy = micH / 2;
  const micCy = micTop + micRy;

  // Arc below mic
  const arcY = micTop + micH + size * 0.04;
  const arcR = micW * 0.75;
  const arcBottom = arcY + size * 0.12;

  // Stem
  const stemTop = arcBottom;
  const stemBottom = stemTop + size * 0.1;

  // Base
  const baseW = micW * 0.7;

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <rect width="${size}" height="${size}" rx="${r}" fill="#3B82F6"/>
  <rect x="${cx - micRx}" y="${micTop}" width="${micW}" height="${micH}" rx="${micRx}" fill="white"/>
  <path d="M${cx - arcR} ${arcY} A${arcR} ${arcR * 0.9} 0 0 0 ${cx + arcR} ${arcY}" stroke="white" stroke-width="${size * 0.045}" fill="none" stroke-linecap="round"/>
  <line x1="${cx}" y1="${arcBottom - size * 0.02}" x2="${cx}" y2="${stemBottom}" stroke="white" stroke-width="${size * 0.045}" stroke-linecap="round"/>
  <line x1="${cx - baseW}" y1="${stemBottom}" x2="${cx + baseW}" y2="${stemBottom}" stroke="white" stroke-width="${size * 0.045}" stroke-linecap="round"/>
</svg>`;
}

// Simple mic outline for tray icons (monochrome)
function makeTraySvg(size, color = "white") {
  const cx = size / 2;
  const micW = size * 0.3;
  const micH = size * 0.4;
  const micTop = size * 0.12;
  const micRx = micW / 2;
  const arcY = micTop + micH + size * 0.04;
  const arcR = micW * 0.7;
  const arcBottom = arcY + size * 0.13;
  const stemBottom = arcBottom + size * 0.1;
  const baseW = micW * 0.55;
  const sw = Math.max(size * 0.06, 1.5);

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <rect x="${cx - micRx}" y="${micTop}" width="${micW}" height="${micH}" rx="${micRx}" stroke="${color}" stroke-width="${sw}" fill="none"/>
  <path d="M${cx - arcR} ${arcY} A${arcR} ${arcR * 0.9} 0 0 0 ${cx + arcR} ${arcY}" stroke="${color}" stroke-width="${sw}" fill="none" stroke-linecap="round"/>
  <line x1="${cx}" y1="${arcBottom - size * 0.02}" x2="${cx}" y2="${stemBottom}" stroke="${color}" stroke-width="${sw}" stroke-linecap="round"/>
  <line x1="${cx - baseW}" y1="${stemBottom}" x2="${cx + baseW}" y2="${stemBottom}" stroke="${color}" stroke-width="${sw}" stroke-linecap="round"/>
</svg>`;
}

// Small pink mic for colored tray / resources
function makeSmallColorSvg(size) {
  const cx = size / 2;
  const micW = size * 0.35;
  const micH = size * 0.42;
  const micTop = size * 0.14;
  const micRx = micW / 2;
  const arcY = micTop + micH + size * 0.03;
  const arcR = micW * 0.7;
  const arcBottom = arcY + size * 0.12;
  const stemBottom = arcBottom + size * 0.08;
  const baseW = micW * 0.55;
  const sw = Math.max(size * 0.07, 2);
  const color = "#3B82F6";

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <rect x="${cx - micRx}" y="${micTop}" width="${micW}" height="${micH}" rx="${micRx}" stroke="${color}" stroke-width="${sw}" fill="${color}" fill-opacity="0.2"/>
  <path d="M${cx - arcR} ${arcY} A${arcR} ${arcR * 0.9} 0 0 0 ${cx + arcR} ${arcY}" stroke="${color}" stroke-width="${sw}" fill="none" stroke-linecap="round"/>
  <line x1="${cx}" y1="${arcBottom - size * 0.02}" x2="${cx}" y2="${stemBottom}" stroke="${color}" stroke-width="${sw}" stroke-linecap="round"/>
  <line x1="${cx - baseW}" y1="${stemBottom}" x2="${cx + baseW}" y2="${stemBottom}" stroke="${color}" stroke-width="${sw}" stroke-linecap="round"/>
</svg>`;
}

// Recording indicator (red dot + mic)
function makeRecordingSvg(size, darkBg = false) {
  const color = darkBg ? "white" : "#333";
  const cx = size / 2;
  const dotR = size * 0.12;

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <circle cx="${size * 0.75}" cy="${size * 0.25}" r="${dotR}" fill="#FF4444"/>
  ${makeTrayMicPath(size, color)}
</svg>`;
}

function makeTrayMicPath(size, color) {
  const cx = size * 0.45;
  const micW = size * 0.25;
  const micH = size * 0.35;
  const micTop = size * 0.18;
  const micRx = micW / 2;
  const arcY = micTop + micH + size * 0.03;
  const arcR = micW * 0.65;
  const arcBottom = arcY + size * 0.1;
  const stemBottom = arcBottom + size * 0.08;
  const baseW = micW * 0.5;
  const sw = Math.max(size * 0.055, 1.5);

  return `<rect x="${cx - micRx}" y="${micTop}" width="${micW}" height="${micH}" rx="${micRx}" stroke="${color}" stroke-width="${sw}" fill="none"/>
  <path d="M${cx - arcR} ${arcY} A${arcR} ${arcR * 0.9} 0 0 0 ${cx + arcR} ${arcY}" stroke="${color}" stroke-width="${sw}" fill="none" stroke-linecap="round"/>
  <line x1="${cx}" y1="${arcBottom - size * 0.02}" x2="${cx}" y2="${stemBottom}" stroke="${color}" stroke-width="${sw}" stroke-linecap="round"/>
  <line x1="${cx - baseW}" y1="${stemBottom}" x2="${cx + baseW}" y2="${stemBottom}" stroke="${color}" stroke-width="${sw}" stroke-linecap="round"/>`;
}

async function svgToPng(svg, outPath, size) {
  await sharp(Buffer.from(svg)).resize(size, size).png().toFile(outPath);
  console.log(`  ${outPath}`);
}

async function main() {
  console.log("Generating VoiceLite icons...\n");

  // --- Main app icons ---
  const mainSizes = [
    { name: "32x32.png", size: 32 },
    { name: "64x64.png", size: 64 },
    { name: "128x128.png", size: 128 },
    { name: "128x128@2x.png", size: 256 },
    { name: "icon.png", size: 512 },
    { name: "logo.png", size: 1024 },
  ];

  for (const { name, size } of mainSizes) {
    const svg = makeSvg(size);
    await svgToPng(svg, join(ICONS_DIR, name), size);
  }

  // --- Windows Store logos ---
  const storeSizes = [
    { name: "Square30x30Logo.png", size: 30 },
    { name: "Square44x44Logo.png", size: 44 },
    { name: "Square71x71Logo.png", size: 71 },
    { name: "Square89x89Logo.png", size: 89 },
    { name: "Square107x107Logo.png", size: 107 },
    { name: "Square142x142Logo.png", size: 142 },
    { name: "Square150x150Logo.png", size: 150 },
    { name: "Square284x284Logo.png", size: 284 },
    { name: "Square310x310Logo.png", size: 310 },
    { name: "StoreLogo.png", size: 50 },
  ];

  for (const { name, size } of storeSizes) {
    const svg = makeSvg(size);
    await svgToPng(svg, join(ICONS_DIR, name), size);
  }

  // --- ICO (Windows) - use 256x256 source ---
  const icoSvg = makeSvg(256);
  const icoPng = await sharp(Buffer.from(icoSvg)).resize(256, 256).png().toBuffer();
  // Create multi-size ICO by embedding 256, 48, 32, 16 px PNGs
  const icoSizes = [256, 48, 32, 16];
  const icoBuffers = [];
  for (const s of icoSizes) {
    icoBuffers.push(await sharp(icoPng).resize(s, s).png().toBuffer());
  }
  writeFileSync(join(ICONS_DIR, "icon.ico"), createIco(icoBuffers, icoSizes));
  console.log(`  ${join(ICONS_DIR, "icon.ico")}`);

  // --- ICNS (macOS) - just use a large PNG; tauri build regenerates it ---
  // We'll just copy the 512 to icon.icns position (tauri handles conversion)
  const icnsSvg = makeSvg(1024);
  await svgToPng(icnsSvg, join(ICONS_DIR, "icon.icns.png"), 1024);
  console.log("  (icon.icns: use `tauri icon` command to generate from icon.png)");

  // --- Android icons ---
  const androidSizes = [
    { dir: "mipmap-mdpi", size: 48 },
    { dir: "mipmap-hdpi", size: 72 },
    { dir: "mipmap-xhdpi", size: 96 },
    { dir: "mipmap-xxhdpi", size: 144 },
    { dir: "mipmap-xxxhdpi", size: 192 },
  ];

  for (const { dir, size } of androidSizes) {
    const d = join(ICONS_DIR, "android", dir);
    mkdirSync(d, { recursive: true });
    const svg = makeSvg(size);
    await svgToPng(svg, join(d, "ic_launcher.png"), size);
    await svgToPng(svg, join(d, "ic_launcher_round.png"), size);
    // Foreground (icon without background for adaptive icons)
    const fgSvg = makeSvg(size, { rounded: false, padding: 0.25 });
    await svgToPng(fgSvg, join(d, "ic_launcher_foreground.png"), size);
  }

  // --- iOS icons ---
  const iosSizes = [
    { name: "AppIcon-20x20@1x.png", size: 20 },
    { name: "AppIcon-20x20@2x.png", size: 40 },
    { name: "AppIcon-20x20@2x-1.png", size: 40 },
    { name: "AppIcon-20x20@3x.png", size: 60 },
    { name: "AppIcon-29x29@1x.png", size: 29 },
    { name: "AppIcon-29x29@2x.png", size: 58 },
    { name: "AppIcon-29x29@2x-1.png", size: 58 },
    { name: "AppIcon-29x29@3x.png", size: 87 },
    { name: "AppIcon-40x40@1x.png", size: 40 },
    { name: "AppIcon-40x40@2x.png", size: 80 },
    { name: "AppIcon-40x40@2x-1.png", size: 80 },
    { name: "AppIcon-40x40@3x.png", size: 120 },
    { name: "AppIcon-60x60@2x.png", size: 120 },
    { name: "AppIcon-60x60@3x.png", size: 180 },
    { name: "AppIcon-76x76@1x.png", size: 76 },
    { name: "AppIcon-76x76@2x.png", size: 152 },
    { name: "AppIcon-83.5x83.5@2x.png", size: 167 },
    { name: "AppIcon-512@2x.png", size: 1024 },
  ];

  const iosDir = join(ICONS_DIR, "ios");
  mkdirSync(iosDir, { recursive: true });
  for (const { name, size } of iosSizes) {
    const svg = makeSvg(size);
    await svgToPng(svg, join(iosDir, name), size);
  }

  // --- Tray icons (resources/) ---
  const traySize = 32;

  // Idle - light outline mic
  await svgToPng(makeTraySvg(traySize, "white"), join(RESOURCES_DIR, "tray_idle.png"), traySize);
  await svgToPng(makeTraySvg(traySize, "#333"), join(RESOURCES_DIR, "tray_idle_dark.png"), traySize);

  // Recording - mic with red dot
  await svgToPng(makeRecordingSvg(traySize, true), join(RESOURCES_DIR, "tray_recording.png"), traySize);
  await svgToPng(makeRecordingSvg(traySize, false), join(RESOURCES_DIR, "tray_recording_dark.png"), traySize);

  // Transcribing - use a blue-ish tint to differentiate
  const transcribeSvg = (dark) => {
    const color = dark ? "#333" : "white";
    const dotColor = "#4488FF";
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${traySize}" height="${traySize}" viewBox="0 0 ${traySize} ${traySize}">
  <circle cx="${traySize * 0.75}" cy="${traySize * 0.25}" r="${traySize * 0.1}" fill="${dotColor}"/>
  ${makeTrayMicPath(traySize, color)}
</svg>`;
  };
  await svgToPng(transcribeSvg(false), join(RESOURCES_DIR, "tray_transcribing.png"), traySize);
  await svgToPng(transcribeSvg(true), join(RESOURCES_DIR, "tray_transcribing_dark.png"), traySize);

  // Colored tray (voicelite.png) - small pink mic
  await svgToPng(makeSmallColorSvg(traySize), join(RESOURCES_DIR, "voicelite.png"), traySize);

  // Recording and transcribing resources for colored theme
  await svgToPng(makeSmallColorSvg(traySize), join(RESOURCES_DIR, "recording.png"), traySize);
  await svgToPng(makeSmallColorSvg(traySize), join(RESOURCES_DIR, "transcribing.png"), traySize);

  console.log("\nDone! All icons generated.");
  console.log("\nNote: Run `bunx tauri icon src-tauri/icons/icon.png` to regenerate icon.icns for macOS.");
}

/**
 * Create a minimal ICO file from PNG buffers.
 * ICO format: header + directory entries + PNG data
 */
function createIco(pngBuffers, sizes) {
  const numImages = pngBuffers.length;
  const headerSize = 6;
  const dirEntrySize = 16;
  const dirSize = dirEntrySize * numImages;
  let dataOffset = headerSize + dirSize;

  // ICO header
  const header = Buffer.alloc(headerSize);
  header.writeUInt16LE(0, 0); // reserved
  header.writeUInt16LE(1, 2); // type: ICO
  header.writeUInt16LE(numImages, 4);

  // Directory entries
  const dir = Buffer.alloc(dirSize);
  for (let i = 0; i < numImages; i++) {
    const s = sizes[i] >= 256 ? 0 : sizes[i]; // 0 means 256
    const off = i * dirEntrySize;
    dir.writeUInt8(s, off); // width
    dir.writeUInt8(s, off + 1); // height
    dir.writeUInt8(0, off + 2); // color palette
    dir.writeUInt8(0, off + 3); // reserved
    dir.writeUInt16LE(1, off + 4); // color planes
    dir.writeUInt16LE(32, off + 6); // bits per pixel
    dir.writeUInt32LE(pngBuffers[i].length, off + 8); // size
    dir.writeUInt32LE(dataOffset, off + 12); // offset
    dataOffset += pngBuffers[i].length;
  }

  return Buffer.concat([header, dir, ...pngBuffers]);
}

main().catch(console.error);
