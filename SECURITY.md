# Security Policy

## Reporting Security Vulnerabilities

**Please do not report security vulnerabilities through public GitHub issues.**

If you discover a security vulnerability in VoiceLite, please report it responsibly:

### How to Report

1. **Email**: Send details to `support@voicelite.app` with subject line: "SECURITY: [Brief Description]"
2. **Include**:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Any suggested fixes (optional)

### What to Expect

- **Response Time**: We'll acknowledge your report within 48 hours
- **Updates**: We'll keep you informed of progress
- **Credit**: With your permission, we'll credit you in release notes
- **Fix Timeline**: Critical issues will be addressed within 7 days

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.2.0.x | ✅ Yes (Latest)    |
| 1.1.x   | ✅ Yes             |
| 1.0.x   | ⚠️ Limited         |
| < 1.0   | ❌ No (Please upgrade) |

## Security Measures

VoiceLite takes security seriously:

### Privacy
- ✅ **100% Offline**: Your voice never leaves your computer
- ✅ **No Telemetry**: We don't collect usage data
- ✅ **No Cloud**: All processing happens locally

### Data Storage
- Settings stored in `%LOCALAPPDATA%\VoiceLite\settings.json` (NOT %APPDATA% - privacy fix, no cloud sync)
- License stored encrypted in `%LOCALAPPDATA%\VoiceLite\license.dat` (DPAPI, `key|email` format)
- Machine ID stored in `%LOCALAPPDATA%\VoiceLite\machine_id.dat` (DPAPI encrypted fallback for VMs)
- Pro license validated against server with 3-device limit

### Known Security Considerations

1. **Text Injection**: VoiceLite simulates keyboard input, which may trigger antivirus warnings (false positives)
2. **Global Hotkeys**: Uses Win32 API for system-wide hotkey registration
3. **License Validation**: Pro tier requires one-time internet connection for validation

## Best Practices for Users

- ✅ Download only from official sources (GitHub releases or voicelite.app)
- ✅ Verify file signatures if provided
- ✅ Keep Windows and VoiceLite updated
- ✅ Use Windows Defender or trusted antivirus
- ⚠️ Be cautious of unofficial builds or modified versions

## Disclosure Policy

When we receive a security report:
1. We'll investigate and develop a fix
2. We'll release a patched version
3. We'll publish a security advisory after the fix is available
4. We'll credit the reporter (unless they prefer anonymity)

---

**Thank you for helping keep VoiceLite secure!** 🔒