# Pre-Production Checklist for v3 Homepage

## ✅ **What's Working Great**

### **Content & Messaging**
- ✅ Honest positioning ("Be among the first" - no fake stats)
- ✅ Clear value prop ("Stop Typing. Start Speaking.")
- ✅ $20 one-time pricing (prominent, no confusion)
- ✅ Founder story (authentic, relatable)
- ✅ Trust signals (30-day guarantee badge)
- ✅ 8 FAQs (launch-specific questions)
- ✅ No fake testimonials or user counts

### **Design & UX**
- ✅ Clean, modern blue theme
- ✅ Sticky navigation with smooth backdrop blur
- ✅ Responsive design (mobile, tablet, desktop)
- ✅ Smooth hover effects (cards lift, shadows grow)
- ✅ Dark mode support
- ✅ Accessibility (focus-visible, motion-reduce)
- ✅ Professional typography and spacing

### **Technical**
- ✅ Fast compile time (466ms)
- ✅ Zero build errors
- ✅ Tailwind CSS v4 working correctly
- ✅ Next.js 15 App Router
- ✅ TypeScript (type-safe)

---

## 🔧 **Minor Polish Opportunities (5-10 min)**

### **1. Add Mobile Hamburger Menu**
**Current**: Navigation links hidden on mobile (display: none)
**Issue**: Mobile users can't access Features, Pricing, FAQ links

**Quick Fix**:
```tsx
// Add mobile menu toggle (5 min)
const [isMenuOpen, setIsMenuOpen] = useState(false);

{/* Mobile menu button */}
<button onClick={() => setIsMenuOpen(!isMenuOpen)} className="md:hidden">
  ☰
</button>

{/* Mobile menu */}
{isMenuOpen && (
  <div className="absolute top-full left-0 w-full bg-white shadow-lg md:hidden">
    <a href="#features">Features</a>
    <a href="#pricing">Pricing</a>
    <a href="#faq">FAQ</a>
  </div>
)}
```

**Impact**: Critical for mobile UX

---

### **2. Make FAQ Accordion Interactive**
**Current**: FAQ items are static (just show question, don't expand)
**Issue**: Users can't read answers

**Quick Fix** (2 options):

**Option A: Simple (2 min)** - Show all answers by default
```tsx
<div className="space-y-2">
  <h3 className="text-lg font-semibold">{item.q}</h3>
  <p className="text-stone-600">{item.a}</p>
</div>
```

**Option B: Interactive (5 min)** - Add click to expand
```tsx
const [openIndex, setOpenIndex] = useState<number | null>(null);

onClick={() => setOpenIndex(openIndex === i ? null : i)}

{openIndex === i && <p>{item.a}</p>}
```

**Impact**: Medium - Users need to read FAQ answers

---

### **3. Fix Pricing Button Link**
**Current**: "Buy Now" goes to `/api/checkout`
**Issue**: `/api/checkout` expects authenticated user (will fail for new visitors)

**Options**:

**Option A**: Direct download link (for now, no checkout)
```tsx
href="https://github.com/mikha08-rgb/VoiceLite/releases/latest"
```

**Option B**: Redirect to login/signup page
```tsx
href="/account" // Or create /buy page
```

**Option C**: Keep `/api/checkout` (implement guest checkout)
- Needs Stripe integration work
- Can do post-launch

**Recommendation**: **Option A** for launch week (free download), implement checkout later

**Impact**: Critical - Button must work

---

### **4. Add Open Graph Meta Tags**
**Current**: Missing OG tags for social sharing
**Issue**: When shared on Twitter/Discord/Slack, no preview image or description

**Quick Fix** (3 min):
```tsx
// Add to app/layout.tsx or page.tsx
export const metadata = {
  title: 'VoiceLite - Private Voice-to-Text for Windows',
  description: 'Stop typing. Start speaking. VoiceLite turns your voice into text instantly—anywhere on Windows. Private, fast, and $20 one-time.',
  openGraph: {
    title: 'VoiceLite - Private Voice-to-Text for Windows',
    description: 'Stop typing. Start speaking. $20 one-time. No subscription.',
    images: ['/og-image.png'], // Need to create this
  },
  twitter: {
    card: 'summary_large_image',
    title: 'VoiceLite - Private Voice-to-Text for Windows',
    description: 'Stop typing. Start speaking. $20 one-time.',
  },
};
```

**Impact**: Medium - Better social sharing (important for launch)

---

### **5. Add Favicon**
**Current**: Default Next.js favicon
**Issue**: Looks unprofessional in browser tabs

**Quick Fix** (2 min):
- Create `app/icon.png` or `app/favicon.ico`
- Next.js auto-detects and uses it

**Impact**: Low - Polish

---

## ⚠️ **Critical Pre-Launch Fixes (Must Do)**

### **Priority 1: Mobile Navigation** ✅ MUST FIX
- Mobile users can't navigate site
- Add hamburger menu (5 min)

### **Priority 2: FAQ Answers** ✅ MUST FIX
- Users can't read FAQ answers
- Either show all or add accordion (2-5 min)

### **Priority 3: Buy Button Link** ✅ MUST FIX
- Button goes to `/api/checkout` which will error
- Change to GitHub download link (1 min)

---

## 📊 **Pre-Production Score**

| Category | Status | Notes |
|----------|--------|-------|
| **Content** | ✅ 100% | Honest, clear, launch-ready |
| **Design** | ✅ 95% | Beautiful, just need mobile menu |
| **Functionality** | ⚠️ 70% | FAQ answers hidden, Buy button broken |
| **SEO** | ⚠️ 60% | Missing OG tags (can add later) |
| **Accessibility** | ✅ 90% | Good keyboard nav, focus states |
| **Performance** | ✅ 100% | Fast compile, optimized |

**Overall: 85% Ready** (3 critical fixes needed)

---

## 🚀 **Recommended Action Plan**

### **Option A: Quick Fixes (10 minutes) → Ship Tonight**
1. ✅ Add mobile hamburger menu (5 min)
2. ✅ Show FAQ answers by default (2 min)
3. ✅ Change "Buy Now" to GitHub download link (1 min)
4. ✅ Test on mobile (2 min)
5. 🚀 Deploy to production

**Total time: 10 minutes**
**Result: 95% ready, launch-worthy**

---

### **Option B: Full Polish (20 minutes) → Ship Tomorrow**
1. ✅ Add mobile hamburger menu (5 min)
2. ✅ Make FAQ accordion interactive (5 min)
3. ✅ Change "Buy Now" to GitHub download (1 min)
4. ✅ Add OG meta tags (3 min)
5. ✅ Add favicon (2 min)
6. ✅ Test on mobile + desktop (4 min)
7. 🚀 Deploy to production

**Total time: 20 minutes**
**Result: 100% polished, perfect launch**

---

### **Option C: Ship As-Is (Testing Phase)**
- Deploy current version
- Test with real users
- Iterate based on feedback

**Pros**: Fastest, real feedback
**Cons**: Mobile UX is poor, FAQ unusable

---

## 💡 **My Recommendation**

**Go with Option A** (10 min quick fixes):
- Mobile menu is essential
- FAQ answers must be readable
- Buy button must work
- OG tags can wait (add after launch)
- Favicon can wait

**This gets you to 95% ready in 10 minutes** and is totally launch-worthy.

---

## 🎯 **Post-Launch Improvements** (Week 2+)

After launch, add these:
1. Record and add demo video (replaces ▶️ emoji)
2. Implement Stripe checkout flow
3. Add OG image for social sharing
4. Collect first testimonials
5. Add "As Seen On" badges (Product Hunt, HN)
6. A/B test copy variations
7. Add analytics (PostHog, Plausible)

---

## ✅ **Ready to Apply Fixes?**

**Should I:**
- **Option A**: Apply the 3 critical fixes now (10 min)
- **Option B**: Ship as-is, iterate after launch
- **Option C**: You tell me which fixes to prioritize

What do you want to do?
