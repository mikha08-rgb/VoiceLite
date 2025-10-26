# VirusTotal Analysis Guide for VoiceLite

## What is VirusTotal?
Aggregator of 70+ antivirus engines. Shows how many engines flag your file as malicious.

**URL**: https://www.virustotal.com

## Normal Detection Rates

| App Status | Detection Rate | User Experience |
|------------|---------------|-----------------|
| **Signed (EV)** | 0-2/70 (3%) | ✅ No warnings, downloads work |
| **Signed (Standard)** | 2-8/70 (10%) | ⚠️ Some warnings for 3-6 months |
| **Unsigned + Metadata** | 10-20/70 (25%) | ⚠️ Most users see warnings |
| **Unsigned + No Metadata** | 25-40/70 (50%) | ❌ Blocked by most systems |
| **Actually Malware** | 60-70/70 (90%) | ❌ Universal block |

**Current VoiceLite Status**: Likely 25-35/70 (unsigned, has metadata after fixes)

## Expected Progress

### Before Fixes (Estimated)
```
Detection: 30-40/70 engines
Common flaggers:
- Microsoft Defender: Trojan:Win32/Wacatac
- Avast/AVG: Win32:Malware-gen
- McAfee: Artemis!
- BitDefender: Gen:Variant.Graftor
```

### After Metadata Improvements (Current)
```
Detection: 20-30/70 engines
Improvement: More engines see publisher info
Still flagged by: Behavioral detection (text injection)
```

### After False Positive Reports (1 week)
```
Detection: 15-25/70 engines
Improvement: Microsoft, Google unblock
Still flagged by: Smaller AV vendors
```

### After EV Code Signing (2 weeks)
```
Detection: 2-8/70 engines
Improvement: Signature trusted by most engines
Remaining flaggers: Overly aggressive heuristics
```

### After 3+ Months (Reputation Built)
```
Detection: 0-3/70 engines
Improvement: Download reputation established
Remaining flaggers: Ultra-conservative engines (acceptable)
```

## How to Check Your Build

### Step 1: Upload to VirusTotal
```bash
# Build installer
dotnet build VoiceLite/VoiceLite.sln -c Release
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/VoiceLite.iss

# Upload to VirusTotal
# Navigate to: https://www.virustotal.com
# Click "Choose file" and select: VoiceLite/Installer/VoiceLite-Setup-1.0.0.exe
```

### Step 2: Analyze Results
Look for these sections:

**Detection Tab:**
- **Red number (X/70)**: How many engines flagged it
- **Goal**: <10/70 for unsigned, <3/70 for signed

**Details Tab:**
- **File size**: Should match your build (~100-150MB)
- **MD5/SHA-256**: Verify it's your actual file
- **Signature**: "Unsigned" (will show publisher after code signing)

**Behavior Tab:**
- Shows what the installer does (creates files, registry entries)
- Should NOT show: network connections (except optional update checks)

### Step 3: Report False Positives
For each engine that flags it:
1. Click engine name (e.g., "Microsoft")
2. Look for "Report false positive" link
3. Submit report with explanation (see `ANTIVIRUS_FALSE_POSITIVE_SUBMISSION.md`)

## Common False Positive Triggers

### Why VoiceLite Gets Flagged
1. **H.InputSimulator (text injection)**
   - Detection: "Keylogger", "Trojan:Win32/Wacatac"
   - Why: Uses SendInput API (same as keyloggers)
   - Solution: Code signing + metadata

2. **RegisterHotKey (global hotkeys)**
   - Detection: "HackTool", "PUA (Potentially Unwanted App)"
   - Why: Monitors all keyboard input
   - Solution: Code signing + clear description

3. **Unsigned executable + admin privileges**
   - Detection: "Artemis!", "Malware-gen"
   - Why: Installer requests admin access
   - Solution: Code signing (proves identity)

4. **Bundled whisper.exe (native binary)**
   - Detection: "Gen:Variant.Graftor"
   - Why: Unsigned third-party executable
   - Solution: Sign whisper.exe separately OR include checksum verification

### Engines to Ignore (Overly Aggressive)
These flag **everything** unsigned:
- Cyren
- Jiangmin
- MaxSecure
- ClamAV (sometimes)
- VBA32

**If only these flag it (2-5 engines)**: You're fine. These have 50%+ false positive rates.

## Tracking Progress

### Create a Baseline (Today)
```bash
# Upload current build to VirusTotal
# Save results as screenshot or text

Example baseline:
- Date: 2025-10-25
- Version: 1.0.96
- Detection: 28/70 engines
- Top flaggers: Microsoft Defender, Avast, McAfee, BitDefender
```

### Monitor Weekly
| Date | Version | Detection | Changes Made | Notes |
|------|---------|-----------|--------------|-------|
| 2025-10-25 | 1.0.96 | 28/70 | Baseline (unsigned, no metadata) | - |
| 2025-10-26 | 1.0.97 | 22/70 | Added metadata | 21% improvement |
| 2025-11-01 | 1.0.97 | 18/70 | False positive reports processed | Microsoft unblocked |
| 2025-11-08 | 1.0.98 | 4/70 | EV code signing added | 86% improvement |
| 2026-02-01 | 1.1.0 | 1/70 | 3 months reputation | Goal achieved |

## Red Flags (What's Abnormal)

**You should be concerned if:**
- Detection rate >50/70 (might be actual malware contamination)
- Engines flag it as "ransomware" or "backdoor" (serious false positive)
- VirusTotal Community Score is negative (users reporting issues)
- Detection rate INCREASES after code signing (cert might be compromised)

**VoiceLite's 25-35/70 is NORMAL for:**
- Unsigned app
- Uses text injection
- Uses global hotkeys
- Bundled native executables

## Alternative: OPSWAT MetaDefender
Secondary scanning service (different engines than VirusTotal)

**URL**: https://metadefender.opswat.com

**Use case**: Cross-reference VirusTotal results
- If VirusTotal: 25/70 flagging
- If MetaDefender: 3/40 flagging
- **Conclusion**: VirusTotal's engines are overly aggressive (normal for unsigned)

## Sample VirusTotal Report Format

Save this info for tracking:

```
VoiceLite VirusTotal Report
Date: 2025-10-25
Version: 1.0.97
File: VoiceLite-Setup-1.0.97.exe
SHA-256: [hash from VirusTotal]

Detection: 22/70 (31%)

Flagging engines:
1. Microsoft - Trojan:Win32/Wacatac.B!ml
2. Avast - Win32:Malware-gen
3. AVG - Win32:Malware-gen
4. McAfee - Artemis!D4F2A1B3C4E5
5. BitDefender - Gen:Variant.Graftor.12345
... (list all)

Status:
- Submitted false positive to Microsoft (2025-10-25)
- Submitted false positive to Google (2025-10-25)
- Reported to VirusTotal community

Next check: 2025-11-01 (after 1 week)
Target: <15/70
```

## When to Panic (and When Not To)

### ❌ DON'T PANIC IF:
- Detection is 20-35/70 (normal for unsigned productivity app)
- Only behavioral detections (no specific malware names)
- All flaggers are using heuristics (no signature-based)
- VirusTotal Community Score is 0 (neutral)

### ⚠️ INVESTIGATE IF:
- Detection is >40/70 (something might be wrong)
- Engines detect specific malware families (Emotet, WannaCry, etc.)
- Community Score is negative (users reporting issues)

### 🚨 IMMEDIATE ACTION IF:
- Detection is >55/70 (build might be compromised)
- Major engines (Microsoft, Kaspersky, ESET) flag as "ransomware"
- You didn't build the file yourself (supply chain attack)

**VoiceLite's Current Status**: ❌ DON'T PANIC - This is expected for unsigned apps

## Resources
- VirusTotal: https://www.virustotal.com
- OPSWAT MetaDefender: https://metadefender.opswat.com
- Hybrid Analysis (behavior): https://www.hybrid-analysis.com
- ANY.RUN (sandbox): https://app.any.run

---

**Next Step**: Upload your current build to VirusTotal and establish a baseline.
Then follow the improvements in `ANTIVIRUS_ACTION_PLAN.md`.
