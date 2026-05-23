# Draft reply to Grand Pacific Health

**Status:** DRAFT — review before sending. Replace `[NAME]` placeholders. The bracketed `[OPTIONAL: ...]` sections are decision points for the owner.

---

## Subject: Re: VoiceLite pilot for Grand Pacific Health

Hi [NAME],

Thanks for reaching out — really glad VoiceLite is a fit for what your team is doing, and I'd love to set up the pilot.

To make sure I get you set up properly, a few quick questions:

1. **Use case** — Will the four pilot seats be used for clinical dictation (notes, reports going into an EMR) or more general admin/productivity work? This affects which accuracy tier I'd recommend and a couple of deployment defaults.
2. **Deployment environment** — Are the four workstations standalone Windows machines, or are they part of a managed Active Directory / Intune environment with IT-controlled installs? If the latter, I can produce an MSI that's friendly to silent install via Intune/SCCM.
3. **Australian Privacy Principles / data handling** — VoiceLite runs entirely on-device (no audio or transcribed text leaves the user's machine, ever), but I want to confirm what level of privacy documentation your compliance/legal team would like to see. I've put together a deployment doc covering log retention, what data the license-validation call sends, machine-naming guidance, and APP relevance — happy to share that as a starting point.

On my side, I'll:

- Issue four pilot licence keys for the workstations (3-device-per-licence limit, so you have some headroom to move people around).
- Send a signed installer for the latest pilot build — this includes recent privacy-focused changes specifically aimed at clinical use:
  - No transcribed text is written to log files (verified, healthcare-grade)
  - Clipboard auto-clears two seconds after each paste, so dictation doesn't linger on shared workstations
  - In-process Whisper.net transcription — no audio ever leaves the device
- Provide a one-pager deployment guide for your IT person (or do a 20-minute screen-share if that's faster).

[OPTIONAL: pricing/billing paragraph — adjust to your pilot terms. Something like: "For the pilot, the four licences are on the house — I'd just appreciate honest feedback on what works, what doesn't, and what would need to change for broader rollout. Happy to discuss longer-term pricing once you've had a chance to put it through its paces."]

Easy next step: if you can answer those three questions above (even rough answers are fine), I can have the licence keys and installer ready for you within [TIMEFRAME — e.g. "a couple of days"]. If video/voice is easier than email, my calendar is at [CAL LINK or "happy to find a time that suits"].

Looking forward to it.

[YOUR NAME]
VoiceLite

---

## Notes for the owner

- **Don't oversell APP compliance.** You can honestly say "no audio or transcribed text leaves the device" because it's true (verified in the audit). You CANNOT say "HIPAA compliant" (US framework, doesn't apply) or "APP certified" (not a thing — there's no APP certification body; it's principles, not a cert).
- **The "no transcribed text in logs" claim is true *as of the pilot build*** — older installs had transcription content in logs (PersistentWhisperService:230). Make sure the pilot users get the build that includes the Phase 1 fixes.
- **Licence count math**: 4 users × 3 devices = 12 activations. Plenty of headroom.
- **What you don't yet know**: Whether the clinic has BYOD or managed-fleet, whether they want their data on Australian-only infra (your license server is on Vercel — check if that matters for them). The questions above tease this out.
- **Suggested send window**: Monday morning AU time. Grand Pacific is in NSW (AEST/AEDT), so aim for ~9-10am their local time = late Sunday/early Monday US time. Don't send Saturday night their time — looks desperate.
