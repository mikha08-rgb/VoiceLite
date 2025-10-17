# Stripe Compliance Fixes - Action Required

Based on legal team feedback, here are the critical changes needed before Stripe submission:

---

## ✅ COMPLETED (by me)

### 1. USD Currency
- ✅ Added "USD" to pricing ($20 USD)
- ✅ Visible on homepage pricing section

### 2. Stripe Security Mention
- ✅ Added "Secure checkout via Stripe"
- ✅ Located in pricing card

### 3. Business Entity Visibility
- ✅ Added "Basement Hustle LLC" to footer
- ✅ Created `/business-info` page

---

## ⚠️ CRITICAL - NEEDS YOUR ACTION

### 1. **Email Address** (URGENT)
**Current:** `support@voicelite.app`, `contact@voicelite.app` (NOT REAL)
**Correct:** `basementhustleLLC@gmail.com`

**Files to update:**
1. `voicelite-web/app/page.tsx` (line ~522)
2. `voicelite-web/app/business-info/page.tsx` (lines ~48, ~53)
3. `voicelite-web/app/legal/refunds/page.tsx` (mentions support email)
4. `voicelite-web/app/terms/page.tsx` (line ~100)
5. `voicelite-web/app/privacy/page.tsx` (Contact section)
6. `voicelite-web/app/basement-hustle-llc/page.tsx` (if keeping this)

**Find & Replace:**
```
Find: support@voicelite.app
Replace: basementhustleLLC@gmail.com

Find: contact@voicelite.app
Replace: basementhustleLLC@gmail.com
```

---

### 2. **Phone Number** (REQUIRED by Stripe)
**Current:** None
**Need:** Real phone number or clear statement

**Add to:**
- Homepage footer (after email)
- `/business-info` page

**Format:**
```
Phone: +1-XXX-XXX-XXXX
```

Or if no business phone:
```
Email support preferred. Phone available upon request: [number]
```

---

### 3. **Business Mailing Address** (REQUIRED)
**Current:** "TODO: Your Street Address"
**Need:** Real, verifiable address

**Must match:**
- ✅ Business registration documents
- ✅ EIN application
- ✅ What you tell Stripe

**Options:**
- Physical office
- Home office (if LLC registered there)
- Registered agent address
- Virtual office (with mail forwarding)

**Update in 2 places:**
1. `voicelite-web/app/page.tsx` (footer, lines 516-518)
2. `voicelite-web/app/business-info/page.tsx` (line ~49)

---

### 4. **Link Business Info Page** (Stripe Crawler Requirement)
**Issue:** Page exists but not linked - Stripe's crawler may not find it

**Fix:** Add to footer Legal section

**In `voicelite-web/app/page.tsx`** (around line 610):
```tsx
<li>
  <a href="/business-info" className="transition-colors hover:text-blue-400">
    Business Information
  </a>
</li>
```

---

### 5. **Governing Law Placeholder** (Legal Requirement)
**Current:** "[Your State/Country]" in Terms
**Need:** Specific jurisdiction

**File:** `voicelite-web/app/terms/page.tsx`

**Find:**
```
Governing Law (placeholders: [Your State/Country])
```

**Replace with (example):**
```
Governing Law: Delaware, United States
```

(Use whatever state your LLC is registered in)

---

## 📋 STRIPE SUBMISSION CHECKLIST

Before submitting to Stripe, verify:

### Website Content
- [ ] All email addresses = `basementhustleLLC@gmail.com`
- [ ] Phone number added (or clear policy stated)
- [ ] Real mailing address (matches business registration)
- [ ] `/business-info` linked in footer
- [ ] Governing law specified in Terms

### Stripe Dashboard
- [ ] Business name: **Basement Hustle LLC** (exact match to LLC docs)
- [ ] Business website: **https://voicelite.app**
- [ ] Address: **Same as on website** (exact match)
- [ ] Bank account info ready
- [ ] Have LLC formation documents ready (PDF)

### Documentation Ready
- [ ] Articles of Organization (LLC formation doc)
- [ ] EIN Letter (from IRS)
- [ ] ID for beneficial owner (passport/driver license)
- [ ] Proof of address (utility bill, bank statement)

---

## 🔧 QUICK FIX COMMANDS

### Option A: Manual Search & Replace (Recommended)

1. Open VS Code
2. Press `Ctrl+Shift+H` (Find in Files)
3. Search: `support@voicelite.app`
4. Replace: `basementhustleLLC@gmail.com`
5. Click "Replace All" in `voicelite-web/app/`
6. Repeat for `contact@voicelite.app`

### Option B: Command Line (Advanced)

```bash
cd voicelite-web/app

# Update emails (Windows PowerShell)
(Get-ChildItem -Recurse -Include *.tsx,*.ts) | ForEach-Object {
  (Get-Content $_.FullName) -replace 'support@voicelite\.app','basementhustleLLC@gmail.com' -replace 'contact@voicelite\.app','basementhustleLLC@gmail.com' | Set-Content $_.FullName
}
```

---

## 📝 AFTER FIXING

### 1. Test Locally
```bash
cd voicelite-web
npm run dev
```

Visit:
- http://localhost:3000 (check footer email)
- http://localhost:3000/business-info (check all contact info)
- http://localhost:3000/terms (check governing law)

### 2. Deploy
```bash
git add voicelite-web/
git commit -m "fix: update contact info and Stripe compliance

- Change all emails to basementhustleLLC@gmail.com
- Add phone number
- Add real business address
- Link business-info page in footer
- Specify governing law in Terms"
git push
```

### 3. Verify Production
Wait 2-3 minutes, then check:
- https://voicelite.app (footer)
- https://voicelite.app/business-info
- https://voicelite.app/terms

---

## ❓ QUESTIONS FOR LEGAL TEAM

1. **What phone number should we use?**
   - Business line?
   - Personal (if sole proprietor)?
   - Google Voice?
   - "Email only" policy?

2. **What's the business mailing address?**
   - Where LLC is registered?
   - Home office?
   - Virtual office?

3. **What state/jurisdiction for governing law?**
   - Where LLC is formed?
   - Where you operate?

4. **Do we have these docs ready for Stripe?**
   - [ ] LLC Articles of Organization
   - [ ] EIN Letter
   - [ ] Beneficial owner ID
   - [ ] Bank account for payouts

---

## 🚨 BLOCKERS

Cannot submit to Stripe until:
1. Email = basementhustleLLC@gmail.com (real email)
2. Phone number added
3. Real address filled in
4. Governing law specified

**Estimated time to fix:** 30 minutes (once you have phone/address)

---

**Last updated:** January 16, 2025
**Status:** Awaiting info from legal team
**Next step:** Get phone number + address → Update files → Deploy → Submit to Stripe