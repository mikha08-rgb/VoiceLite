# ⚡ Ultra-Quick Deployment (Copy-Paste)

**Time:** 15 minutes total
**Status:** Desktop ready ✅ | Vercel pending ⏳

---

## 🎯 What You Need to Do

You need to update 5 secrets in Vercel dashboard, then deploy.

---

## Step 1: Open Vercel Dashboard (30 seconds)

**Click this link:**

👉 **https://vercel.com/mishas-projects-0509f3dc/voicelite/settings/environment-variables**

You should see a page with environment variables.

---

## Step 2: Update 5 Variables (10 minutes)

For each variable below:
1. Find it in the list
2. Click the "•••" button on the right
3. Click "Edit"
4. **Select all the old value and delete it**
5. **Copy the new value from below**
6. **Paste it**
7. Click "Save"

---

### Variable 1: LICENSE_SIGNING_PRIVATE_B64

**New value (copy this):**
```
***REMOVED***
```

---

### Variable 2: LICENSE_SIGNING_PUBLIC_B64

**New value (copy this):**
```
***REMOVED***
```

---

### Variable 3: CRL_SIGNING_PRIVATE_B64

**New value (copy this):**
```
***REMOVED***
```

---

### Variable 4: CRL_SIGNING_PUBLIC_B64

**New value (copy this):**
```
***REMOVED***
```

---

### Variable 5: MIGRATION_SECRET

**New value (copy this):**
```
***REMOVED***
```

---

## Step 3: Deploy (3 minutes)

After you've updated all 5 variables, open your terminal:

```bash
cd voicelite-web
vercel deploy --prod
```

**Wait 2-3 minutes.** You'll see output like:
```
✅  Production: https://voicelite.app [2m]
```

---

## Step 4: Verify (30 seconds)

```bash
curl -I https://voicelite.app
```

**Expected:** First line shows `HTTP/1.1 200 OK` or `HTTP/2 200`

---

## ✅ You're Done!

Production is now fully updated with:
- ✅ New license signing keys
- ✅ New CRL signing keys
- ✅ New migration secret
- ✅ Desktop app updated to match

---

## 🔴 Optional: Check Resend

If email isn't working, you may need to configure Resend API:

```bash
cd voicelite-web
vercel env ls production | grep RESEND_API_KEY
```

**If it shows "Encrypted":** ✅ You're good!

**If it's missing:** You need to sign up at https://resend.com and add the API key to Vercel.

---

## 📋 Checklist

- [ ] Opened Vercel environment variables page
- [ ] Updated LICENSE_SIGNING_PRIVATE_B64
- [ ] Updated LICENSE_SIGNING_PUBLIC_B64
- [ ] Updated CRL_SIGNING_PRIVATE_B64
- [ ] Updated CRL_SIGNING_PUBLIC_B64
- [ ] Updated MIGRATION_SECRET
- [ ] Ran `vercel deploy --prod`
- [ ] Verified website responds (200 OK)
- [ ] (Optional) Checked RESEND_API_KEY exists

---

## 💡 Tips

- **Copy carefully:** Make sure you copy the entire value (select all, Ctrl+C)
- **Paste carefully:** Delete old value completely before pasting
- **One at a time:** Update one variable at a time to avoid mistakes
- **Double-check:** After pasting, verify the value looks correct before clicking Save

---

## 🚀 That's It!

**Need more details?** See [START_HERE_DEPLOYMENT.md](START_HERE_DEPLOYMENT.md)

**Questions?** Check [DEPLOYMENT_STATUS.md](DEPLOYMENT_STATUS.md)
