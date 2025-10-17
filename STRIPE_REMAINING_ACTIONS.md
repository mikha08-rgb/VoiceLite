# Stripe Compliance - Remaining Manual Actions

## ✅ COMPLETED
- All email addresses updated to `basementhustleLLC@gmail.com` across 6+ files
- Business information page linked in footer Legal section
- Business information page added to sitemap for Stripe crawler discoverability
- "USD" added to pricing ($20 USD)
- "Secure checkout via Stripe" added to pricing card
- "Basement Hustle LLC" added to homepage footer
- 30-day refund policy page created

---

## ⚠️ STILL REQUIRED - Your Action Needed

Before you can submit to Stripe, you **must** provide the following information and update the files listed below.

### 1. **Phone Number** (Required by Stripe)

**What you need to provide:**
- A real phone number for business contact (can be Google Voice, business line, or personal)

**Files to update:**

#### [voicelite-web/app/page.tsx](voicelite-web/app/page.tsx:516-518)
```tsx
// Current (line ~520):
<p className="text-sm">
  <strong className="text-white">Contact:</strong>{' '}
  <a href="mailto:basementhustleLLC@gmail.com" className="transition-colors hover:text-blue-400">
    basementhustleLLC@gmail.com
  </a>
</p>

// Add phone number after email:
<p className="text-sm">
  <strong className="text-white">Contact:</strong>{' '}
  <a href="mailto:basementhustleLLC@gmail.com" className="transition-colors hover:text-blue-400">
    basementhustleLLC@gmail.com
  </a>
  {' '} | Phone: +1-XXX-XXX-XXXX
</p>
```

#### [voicelite-web/app/business-info/page.tsx](voicelite-web/app/business-info/page.tsx:59-74)
```tsx
// Add a new row after Business Email (line ~74):
<div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
  <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Phone:</dt>
  <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
    +1-XXX-XXX-XXXX
  </dd>
</div>
```

#### [voicelite-web/app/basement-hustle-llc/page.tsx](voicelite-web/app/basement-hustle-llc/page.tsx:130)
```tsx
// Current (line 130):
{ label: 'Support Phone', value: 'TODO: +1-XXX-XXX-XXXX' },

// Replace with your real number:
{ label: 'Support Phone', value: '+1-XXX-XXX-XXXX' },
```

---

### 2. **Business Mailing Address** (Required by Stripe)

**What you need to provide:**
- Street address
- City, State, ZIP
- Country

**IMPORTANT:** This address **must exactly match**:
- Your LLC registration documents
- Your EIN application
- What you enter in Stripe dashboard

**Options:**
- Physical office address
- Home office (if LLC registered there)
- Registered agent address
- Virtual office with mail forwarding

**Files to update:**

#### [voicelite-web/app/page.tsx](voicelite-web/app/page.tsx:514-518)
```tsx
// Current (lines 516-518):
<div className="text-xs text-stone-500 space-y-1">
  <p><strong className="text-stone-400">Basement Hustle LLC</strong></p>
  <p>TODO: Your Street Address</p>
  <p>TODO: City, State ZIP</p>
  <p>TODO: Country</p>
</div>

// Replace with:
<div className="text-xs text-stone-500 space-y-1">
  <p><strong className="text-stone-400">Basement Hustle LLC</strong></p>
  <p>123 Main Street</p>
  <p>City, ST 12345</p>
  <p>United States</p>
</div>
```

#### [voicelite-web/app/business-info/page.tsx](voicelite-web/app/business-info/page.tsx:52-57)
```tsx
// Current (lines 53-56):
<dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
  <p>TODO: Street Address</p>
  <p>TODO: City, State ZIP</p>
  <p>TODO: Country</p>
</dd>

// Replace with:
<dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
  <p>123 Main Street</p>
  <p>City, ST 12345</p>
  <p>United States</p>
</dd>
```

#### [voicelite-web/app/basement-hustle-llc/page.tsx](voicelite-web/app/basement-hustle-llc/page.tsx:84)
```tsx
// Current (line 84):
{
  label: 'Registered Business Address',
  value: 'TODO: Street Address, City, State ZIP, Country'
},

// Replace with:
{
  label: 'Registered Business Address',
  value: '123 Main Street, City, ST 12345, United States'
},
```

---

### 3. **Governing Law Jurisdiction** (Required in Terms of Service)

**What you need to provide:**
- The state where your LLC is registered (e.g., "Delaware", "California", "New York")

**File to update:**

#### [voicelite-web/app/terms/page.tsx](voicelite-web/app/terms/page.tsx:220-221)
```tsx
// Current (lines 219-222):
<p className="text-gray-700">
  These Terms are governed by the laws of [Your State/Country], without regard to conflict of law
  principles. Any disputes shall be resolved in the courts of [Your Jurisdiction].
</p>

// Replace with (example):
<p className="text-gray-700">
  These Terms are governed by the laws of Delaware, United States, without regard to conflict of law
  principles. Any disputes shall be resolved in the courts of Delaware.
</p>
```

---

## 📋 STRIPE SUBMISSION CHECKLIST

After making the above updates, verify the following before submitting to Stripe:

### Website Content Ready
- [ ] All email addresses = `basementhustleLLC@gmail.com` ✅ DONE
- [ ] Phone number added to homepage footer
- [ ] Phone number added to /business-info page
- [ ] Real mailing address in homepage footer (matches LLC docs)
- [ ] Real mailing address in /business-info page (matches LLC docs)
- [ ] `/business-info` linked in footer Legal section ✅ DONE
- [ ] Governing law specified in Terms (matches LLC state)
- [ ] USD currency shown on pricing ($20 USD) ✅ DONE
- [ ] "Secure checkout via Stripe" visible ✅ DONE

### Stripe Dashboard Info (Must Match Website Exactly)
- [ ] Business name: **Basement Hustle LLC**
- [ ] Business website: **https://voicelite.app**
- [ ] Contact email: **basementhustleLLC@gmail.com**
- [ ] Phone: **(your number - must match website)**
- [ ] Address: **(your address - must match website exactly)**
- [ ] Bank account info ready for payouts

### Documentation Ready for Stripe Verification
- [ ] LLC Articles of Organization (PDF)
- [ ] EIN Letter from IRS (PDF)
- [ ] Government-issued ID for beneficial owner (passport/driver license scan)
- [ ] Proof of address (utility bill, bank statement - dated within 3 months)

---

## 🚀 DEPLOYMENT STEPS

Once you've updated all the files above:

### 1. Test Locally
```bash
cd voicelite-web
npm run dev
```

Visit and verify:
- http://localhost:3000 (check footer contact info and address)
- http://localhost:3000/business-info (check all fields updated)
- http://localhost:3000/terms (check governing law)

### 2. Commit and Deploy
```bash
git add voicelite-web/
git commit -m "fix: complete Stripe compliance updates

- Update phone number across all pages
- Add real business mailing address
- Specify governing law jurisdiction in Terms
- All contact info now matches LLC registration"

git push
```

Vercel will auto-deploy in ~2-3 minutes.

### 3. Verify Production
Wait 2-3 minutes, then check:
- https://voicelite.app (footer shows phone + address)
- https://voicelite.app/business-info (all fields updated)
- https://voicelite.app/terms (governing law specified)
- https://voicelite.app/sitemap.xml (business-info page listed)

### 4. Submit to Stripe
Go to your Stripe dashboard and:
1. Enter business information **exactly as it appears on your website**
2. Upload required documents
3. Submit for review

Expected result: Automatic approval (all requirements met!)

---

## ❓ QUESTIONS?

If you're unsure about any of these:

**Phone number:**
- Can be Google Voice, business line, or personal
- Will be visible on your website, so choose accordingly

**Address:**
- **MUST** match your LLC formation documents and EIN application
- Cannot be a P.O. Box (Stripe rejects these)
- Can be a virtual office if it accepts mail and physical packages

**Governing law:**
- Use the state where your LLC was formed
- Check your LLC formation documents if unsure

---

**Status:** Awaiting phone number, address, and governing law information
**Next Step:** Provide the 3 pieces of information above → Update files → Deploy → Submit to Stripe
**Estimated Time:** 15-20 minutes once you have the information

**Last Updated:** January 16, 2025
