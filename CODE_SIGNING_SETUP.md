# Code Signing Setup for VoiceLite

## Why Code Signing is Critical
- **Microsoft SmartScreen**: Unsigned apps show "Unknown Publisher" warning (80%+ users abandon)
- **Microsoft Defender**: Unsigned apps with text injection = instant malware flag
- **Google Safe Browsing**: Blocks unsigned executables from downloads
- **User Trust**: Code signing is industry standard for Windows apps

## Certificate Options

### Option 1: EV Code Signing (RECOMMENDED)
**Cost**: $300-400/year | **Reputation**: Immediate

**Providers:**
1. **DigiCert** ($469/year)
   - Instant Microsoft SmartScreen reputation
   - USB token-based (FIPS 140-2 compliant)
   - 3-day turnaround
   - https://www.digicert.com/signing/code-signing-certificates

2. **Sectigo (Comodo)** ($329/year)
   - Good reputation
   - USB token included
   - 5-day verification
   - https://sectigo.com/ssl-certificates-tls/code-signing

3. **SSL.com** ($199/year - BUDGET OPTION)
   - Cloud-based signing (no USB token required)
   - 7-day verification
   - https://www.ssl.com/code-signing/

**Requirements:**
- Business registration (LLC or sole proprietorship)
- Phone + address verification
- Dun & Bradstreet number (DigiCert only)

### Option 2: Standard Code Signing (Not Recommended)
**Cost**: $80-200/year | **Reputation**: 3-6 months + 3000+ downloads

**Why NOT recommended:**
- No immediate SmartScreen reputation
- Still triggers "Unknown Publisher" warnings
- Takes months to build trust
- Antivirus still suspicious

## Setup Instructions (DigiCert Example)

### Step 1: Purchase Certificate
1. Go to https://www.digicert.com/signing/code-signing-certificates
2. Choose "EV Code Signing Certificate"
3. Complete business verification (3-5 days)
4. Receive USB token in mail

### Step 2: Install Certificate
```powershell
# Insert USB token, install DigiCert client
# Certificate auto-loads into Windows Certificate Store

# Verify installation
certutil -store My
```

### Step 3: Configure Inno Setup Signing
Edit `VoiceLite.iss` line 36:

**Before:**
```iss
SignTool=signtool
```

**After:**
```iss
SignTool=signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a $f
SignedUninstaller=yes
```

**Explanation:**
- `/tr`: RFC 3161 timestamp server (keeps signature valid after cert expires)
- `/td sha256`: Timestamp digest algorithm
- `/fd sha256`: File digest algorithm (SHA-256, required for modern Windows)
- `/a`: Auto-select best certificate from store
- `$f`: Inno Setup placeholder for filename

### Step 4: Sign Executable Before Installer Build
Add to `.github/workflows/release.yml` or local build script:

```yaml
# Sign main executable
- name: Sign VoiceLite.exe
  run: |
    & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" sign `
      /tr http://timestamp.digicert.com `
      /td sha256 `
      /fd sha256 `
      /a `
      "VoiceLite/VoiceLite/bin/Release/net8.0-windows/VoiceLite.exe"
```

### Step 5: Verify Signature
```powershell
# Check signature
Get-AuthenticodeSignature "VoiceLite-Setup-1.0.96.exe"

# Should show:
# Status: Valid
# SignerCertificate: CN=VoiceLite Software, ...
# TimeStamperCertificate: CN=DigiCert Timestamp...
```

## Timeline
1. **Purchase**: 15 minutes
2. **Verification**: 3-5 business days
3. **USB token delivery**: 3-7 business days (or instant for cloud-based SSL.com)
4. **Setup**: 1 hour
5. **SmartScreen reputation**: Immediate (EV) or 3-6 months (Standard)

## Alternative: Temporary Cloud Signing (SSL.com)
If you need instant signing without USB token:

**SSL.com eSigner** ($199/year):
- Cloud-based HSM (no hardware token)
- API-based signing for CI/CD
- 2FA via mobile app
- Setup time: 7 days verification + 1 hour config

**GitHub Actions integration:**
```yaml
- name: Sign with SSL.com eSigner
  uses: sslcom/esigner-codesign@develop
  with:
    username: ${{ secrets.ESIGNER_USERNAME }}
    password: ${{ secrets.ESIGNER_PASSWORD }}
    totp-secret: ${{ secrets.ESIGNER_TOTP }}
    file-path: VoiceLite-Setup-1.0.96.exe
```

## Cost-Benefit Analysis
| Certificate | Cost/Year | SmartScreen | Antivirus | Setup Time |
|------------|-----------|-------------|-----------|------------|
| None | $0 | ❌ Blocks | ❌ Flags | 0 days |
| Standard | $80-200 | ⚠️ 3-6 months | ⚠️ Still flags | 5 days |
| EV (DigiCert) | $469 | ✅ Immediate | ✅ Trusted | 10 days |
| EV (SSL.com) | $199 | ✅ Immediate | ✅ Trusted | 8 days |

**Recommendation**: SSL.com EV ($199/year) - Best value for indie developers

## What to Expect After Signing
- **Microsoft Defender**: False positives drop from 90% to <5%
- **SmartScreen**: "Unknown Publisher" warning removed immediately
- **Google Safe Browsing**: No more download blocks
- **User trust**: Professional appearance
- **Download conversion**: Increases 60-80%

## References
- Microsoft Code Signing Guide: https://learn.microsoft.com/en-us/windows/win32/seccrypto/cryptography-tools
- DigiCert Setup: https://docs.digicert.com/en/software-trust-manager/ci-cd-integrations/plugins/github-custom-action.html
- SSL.com eSigner: https://www.ssl.com/esigner/
