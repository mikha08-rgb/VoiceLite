# Basement Hustle LLC Corporate Information Page

## Overview

A secure, unlinked page at `/basement-hustle-llc` for Stripe and payment processor verification. This page contains corporate entity information required for payment processor onboarding and compliance reviews.

## Location

**URL:** `https://voicelite.app/basement-hustle-llc`

**File:** `voicelite-web/app/basement-hustle-llc/page.tsx`

---

## Features

### 1. **Not Publicly Listed**
- Not linked from navigation, footer, or any public page
- Excluded from sitemap.xml
- Excluded from robots.txt (Disallow directive)
- Contains noindex,nofollow meta tags

### 2. **Optional Secret Key Protection (Disabled by Default)**
- Access control via query parameter: `?k=YOUR_SECRET_KEY`
- Shows 404 if wrong/missing key when enabled
- Easy to toggle on/off via environment variable

### 3. **Comprehensive Stripe-Friendly Content**
Includes all sections required by payment processors:

- Legal Entity Information (name, EIN, business type)
- Business Address (registered + mailing)
- Incorporation Details (country, state, formation date)
- Online Presence (website URLs)
- Business Description (products, services, business model)
- Contact Information (support email, phone)
- Legal Policies (Terms, Privacy, Refunds)
- Ownership & Control (beneficial owners, authorized rep)
- Banking & Settlement (bank name, masked account)
- Customer Geography (countries served)
- Fulfillment & Delivery (how customers receive product)
- Refund & Return Policy
- Risk & Disputes (fraud prevention, chargeback history)
- Data & Privacy (GDPR compliance, data handling)

### 4. **Professional Design**
- Uses existing VoiceLite layout and styling
- Responsive (mobile-friendly)
- Dark mode support
- Clear section organization with definition lists
- Verification banner at top
- Footer notice for payment processors
- Last updated timestamp

---

## Configuration

### Enable Secret Key Protection (Optional)

By default, the page is **publicly accessible** (but not linked/indexed). To add authentication:

1. Add to `.env.local` or Vercel environment variables:
   ```env
   PRIVATE_PAGE_KEY=your-secret-key-here
   ```

2. Access the page with:
   ```
   https://voicelite.app/basement-hustle-llc?k=your-secret-key-here
   ```

3. Without the correct key, visitors see a 404 page.

To **disable** protection, simply remove `PRIVATE_PAGE_KEY` from environment variables.

---

## Updating Content

### Replace TODO Placeholders

The page contains TODO placeholders for sensitive information. Replace these with real values:

1. Open `voicelite-web/app/basement-hustle-llc/page.tsx`
2. Find all instances of `TODO:` in the `entityData` array
3. Replace with actual information (mask PII as needed)
4. Update the `LAST_UPDATED` constant at the top of the file

**Example:**

```typescript
// BEFORE
{ label: 'EIN / Tax ID', value: 'XX-XXXXXXX', note: 'Full EIN available to Stripe upon request' }

// AFTER (example)
{ label: 'EIN / Tax ID', value: '12-3456789', note: 'Full EIN available to Stripe upon request' }
```

### Masking Sensitive Information

Follow these patterns for PII:

```typescript
// Beneficial owner
{ label: 'Beneficial Owner(s)', value: '[Name Redacted]', note: 'Full name, DOB, and address available to Stripe upon request' }

// Bank account
{ label: 'Account Information', value: 'Account ending in 1234', note: 'Full account details available to Stripe upon request' }

// EIN
{ label: 'EIN / Tax ID', value: 'XX-XXXXXXX', note: 'Full EIN available to Stripe upon request' }
```

### Update Timestamp

When making changes, update the `LAST_UPDATED` constant:

```typescript
const LAST_UPDATED = '2025-01-16T00:00:00Z'; // ISO 8601 format
```

---

## Verification Checklist

### After Deployment

Run these checks to ensure proper configuration:

#### 1. Check noindex meta tag
```bash
curl -I https://voicelite.app/basement-hustle-llc
# Look for: <meta name="robots" content="noindex,nofollow">
```

Or view page source in browser and search for `noindex`.

#### 2. Verify sitemap exclusion
Visit: `https://voicelite.app/sitemap.xml`

Confirm `/basement-hustle-llc` is **NOT** listed.

#### 3. Verify robots.txt
Visit: `https://voicelite.app/robots.txt`

Confirm it includes:
```
Disallow: /basement-hustle-llc
```

#### 4. Test secret key (if enabled)
- Without key: `https://voicelite.app/basement-hustle-llc` → 404
- With correct key: `https://voicelite.app/basement-hustle-llc?k=YOUR_KEY` → Page loads
- With wrong key: `https://voicelite.app/basement-hustle-llc?k=wrong` → 404

#### 5. Check page rendering
- Visit the page directly
- Verify all sections render correctly
- Check dark mode toggle works
- Test on mobile device

---

## Related Files Created

1. **Main Page**
   - `voicelite-web/app/basement-hustle-llc/page.tsx` - Corporate info page

2. **Sitemap Configuration**
   - `voicelite-web/app/sitemap.ts` - XML sitemap (excludes /basement-hustle-llc)

3. **Robots Configuration**
   - `voicelite-web/app/robots.ts` - robots.txt (disallows /basement-hustle-llc)

4. **Legal Page (New)**
   - `voicelite-web/app/legal/refunds/page.tsx` - Refund policy page

5. **Environment Validation**
   - `voicelite-web/lib/env-validation.ts` - Updated with PRIVATE_PAGE_KEY

---

## Security Best Practices

### DO:
- ✅ Keep the page URL confidential (only share with Stripe/payment processors)
- ✅ Use environment variables for the secret key (never commit to git)
- ✅ Mask PII in the page content
- ✅ Provide full details to Stripe through official verification channels
- ✅ Update the page when business information changes

### DON'T:
- ❌ Link to this page from public pages
- ❌ Include full SSN, bank account numbers, or addresses in the page
- ❌ Commit real PII to git
- ❌ Share the secret key publicly
- ❌ Remove noindex/nofollow meta tags

---

## Sharing with Stripe

When Stripe requests corporate information:

1. **Email Method:**
   ```
   Subject: Basement Hustle LLC - Corporate Information

   Hello,

   Our corporate entity information is available at:
   https://voicelite.app/basement-hustle-llc

   [If secret key enabled: Access with ?k=YOUR_SECRET_KEY]

   Please let me know if you need any additional documentation.

   Best regards,
   [Your Name]
   Basement Hustle LLC
   ```

2. **Support Portal:**
   - Upload a PDF export of the page
   - Reference the URL in your message

3. **Phone Verification:**
   - Have the URL ready to read aloud
   - Reference specific sections by title

---

## Troubleshooting

### Page shows 404
- **If secret key enabled:** Ensure you're using the correct query parameter `?k=YOUR_KEY`
- **Check environment variable:** Verify `PRIVATE_PAGE_KEY` is set correctly in Vercel
- **Clear browser cache:** Try incognito/private mode

### Page appears in search results
- **Wait 1-2 weeks:** Google needs time to re-crawl
- **Force re-index:** Submit sitemap.xml to Google Search Console
- **Verify noindex tag:** View page source and check for `<meta name="robots" content="noindex,nofollow">`

### Need to update content
1. Edit `voicelite-web/app/basement-hustle-llc/page.tsx`
2. Find the `entityData` array
3. Update values in the relevant section
4. Update `LAST_UPDATED` constant
5. Commit and deploy

### Want to add a new section
```typescript
// Add to entityData array
{
  title: 'New Section Title',
  items: [
    { label: 'Field Name', value: 'Field Value' },
    { label: 'Another Field', value: 'Another Value', note: 'Optional note' },
  ],
}
```

---

## Development

### Local Testing

```bash
cd voicelite-web

# Start dev server
npm run dev

# Visit
http://localhost:3000/basement-hustle-llc

# Test with secret key
http://localhost:3000/basement-hustle-llc?k=YOUR_KEY
```

### Environment Variables

```env
# .env.local (for local development)
PRIVATE_PAGE_KEY=test-secret-key-local
```

```env
# Vercel (for production)
PRIVATE_PAGE_KEY=production-secret-key-here
```

### Build & Deploy

```bash
# Build
npm run build

# Preview production build
npm start

# Deploy to Vercel (automatic on push)
git push origin main
```

---

## Support

For questions or issues:

- **Email:** contact@voicelite.app
- **Documentation:** This file
- **Code Location:** `voicelite-web/app/basement-hustle-llc/page.tsx`

---

## Changelog

### 2025-01-16 - Initial Implementation
- Created `/basement-hustle-llc` page with comprehensive Stripe verification content
- Added optional secret key authentication (disabled by default)
- Created `/legal/refunds` page
- Configured sitemap.xml to exclude page
- Configured robots.txt to disallow page
- Added PRIVATE_PAGE_KEY to environment validation
- Added noindex/nofollow meta tags

---

## License

Closed source - Basement Hustle LLC proprietary.
