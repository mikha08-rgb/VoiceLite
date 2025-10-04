# VoiceLite Branding Assets

## Completed ✅

1. **Logo SVG** - `logo-final.svg` (Version 3 - approved)
   - Minimalist microphone design
   - Apple-style aesthetic
   - Purple color (#7c3aed)

2. **Desktop App Icon** - Updated files:
   - `../VoiceLite/VoiceLite/icon.svg` - Source SVG
   - `../VoiceLite/VoiceLite/VoiceLite.ico` - Multi-size .ico file (16, 32, 48, 64, 128, 256px)
   - `../VoiceLite/VoiceLite/create_icon.py` - Updated to generate new design

3. **Web Assets** - Updated files:
   - `../voicelite-web/app/icon.svg` - PWA icon
   - `../voicelite-web/app/favicon.ico` - Browser favicon

## To Do Manually 📋

1. **Social Preview Image** (`og-image.png`):
   - Source SVG created at: `../voicelite-web/public/og-image.svg`
   - **Action needed**: Convert to PNG (1200x630)
   - Tools: Use https://cloudconvert.com/svg-to-png or Figma
   - Save as: `../voicelite-web/public/og-image.png`
   - Then delete the .svg version

2. **Optional Enhancements**:
   - Consider adding logo to website header
   - Consider replacing Mic icon in footer with actual logo SVG
   - Update system tray icon to use new design (already references VoiceLite.ico)

## Design Specifications

- **Primary Color**: #7c3aed (Purple)
- **Style**: Minimalist, Apple-inspired
- **Key Elements**: 
  - Rounded mic capsule
  - U-shaped bracket
  - Clean lines, no gradients
  - Works at any size (16px to 1200px)

## File Locations

```
branding/
├── logo-final.svg          ← Main logo (approved)
├── logo-v1-minimal.svg     ← Archive
├── logo-v2.svg            ← Archive
├── logo-v3.svg            ← Archive (same as final)
└── comparison.html        ← Preview page

VoiceLite/VoiceLite/
├── icon.svg               ← Updated
├── VoiceLite.ico          ← Updated
└── create_icon.py         ← Updated script

voicelite-web/
├── app/icon.svg           ← Updated
├── app/favicon.ico        ← Updated
└── public/
    ├── og-image.svg       ← Needs conversion to PNG
    └── og-image.png       ← TO CREATE
```
