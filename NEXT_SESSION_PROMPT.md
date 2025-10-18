# 🚀 Quick Start Prompt for Next Session

**Copy/paste this into Claude Code when you return:**

---

Hi Claude! I just restarted my PC and set up Supabase MCP in another instance. We're ready to connect the database and finish the VoiceLite production deployment.

## Current Status

**What's Done ✅:**
- Desktop app v1.0.68 builds successfully (0 errors)
- Website deployed at voicelite.app
- All critical security fixes committed (5 commits ahead of origin):
  - Race condition fix in license activation (Prisma transaction)
  - Email failure recovery logging (customers never lose licenses)
  - Client-side model gating (anti-piracy - server-side validation)
  - License encryption with DPAPI (Windows Data Protection API)
  - Rate limiting on validation endpoint (prevents brute force)
  - Graceful Stripe error handling (can deploy without Stripe)
- Resend email configured: `re_Vn4JijC8_KJGGmrQYBe5QXa9ohEHiGjZn`
- Upstash Redis configured: `https://golden-ibex-26450.upstash.io`
- Git workspace clean (no uncommitted changes)

**What's Blocked ⏳:**
- Database connection (Supabase connection blocked by network/firewall)
- Schema migration not applied yet (needs database connection)

**What's Not Done ❌:**
- Stripe configuration (optional - skipped for now)

## Immediate Next Steps

### 1. Connect to Supabase Database (15 minutes)

The Supabase MCP is now set up in another Claude instance. Let's use it to:

**Database Details:**
- Provider: Supabase PostgreSQL
- Project: `lvocjzqjqllouzyggpqm`
- Connection string: `postgresql://postgres:jpcyCSh80D5sG$iq@db.lvocjzqjqllouzyggpqm.supabase.co:5432/postgres`

**Tasks:**
1. Verify Supabase MCP can connect
2. Run Prisma migration to create tables:
   ```bash
   cd voicelite-web
   npx prisma db push
   ```
3. Verify tables created:
   - `License` table (with `emailSent` field + 5 indexes)
   - `LicenseActivation` table (with unique constraint on licenseId + machineId)
   - `WebhookEvent` table (for idempotency)

### 2. Test the License System (30 minutes)

**Generate Test License:**
```bash
cd voicelite-web
# Use the license generation script or API endpoint
# Should create a test license: VL-XXXXXX-XXXXXX-XXXXXX
```

**Test Desktop App Activation:**
1. Build desktop app: `cd VoiceLite && dotnet build -c Release`
2. Run: `VoiceLite/bin/Release/net8.0-windows/VoiceLite.exe`
3. Enter test license key
4. Verify:
   - License activates successfully
   - Database shows activation record
   - License file encrypted with DPAPI (`license.dat` is binary, not JSON)

**Test API Endpoints:**
```bash
# Test validation endpoint
curl -X POST http://localhost:3000/api/licenses/validate \
  -H "Content-Type: application/json" \
  -d '{"licenseKey":"VL-TEST-TEST-TEST"}'
# Should return: {"valid":false,"error":"License key not found"}

# Test rate limiting (run 101 times)
for i in {1..101}; do
  curl -X POST http://localhost:3000/api/licenses/validate \
    -H "Content-Type: application/json" \
    -d '{"licenseKey":"VL-TEST-TEST-TEST"}'
done
# Requests 101+ should return 429 Too Many Requests
```

### 3. Optional: Additional Quick Fixes (1-2 hours)

If you want to knock out more critical issues, check `START_HERE_FIXES.md`:
- **Fix #2**: Remove Ed25519 from env-validation.ts (5 min) - Prevents build failures
- **Fix #3**: Add try-catch to async void methods (20 min) - Prevents silent crashes
- **Fix #4**: Fix UI thread violations (10 min) - Prevents InvalidOperationException
- **Fix #5**: Delete backup page files (5 min) - Cleanup
- **Fix #7**: Add webhook timestamp validation (15 min) - Security

Total time: ~1 hour for all 5 fixes

## Files You'll Be Working With

**Database Setup:**
- `voicelite-web/prisma/schema.prisma` (schema with indexes)
- `voicelite-web/.env` (Supabase connection string)
- `voicelite-web/prisma/migrations/` (migration files)

**Testing:**
- `voicelite-web/lib/licensing.ts` (license generation)
- `voicelite-web/app/api/licenses/activate/route.ts` (activation endpoint)
- `voicelite-web/app/api/licenses/validate/route.ts` (validation endpoint)
- `VoiceLite/VoiceLite/Services/SimpleLicenseStorage.cs` (DPAPI encryption)

**Additional Fixes (if doing optional tasks):**
- `voicelite-web/lib/env-validation.ts` (Fix #2)
- `VoiceLite/VoiceLite/MainWindow.xaml.cs` (Fix #3, #4)
- `voicelite-web/app/` (Fix #5 - delete backups)
- `voicelite-web/app/api/webhook/route.ts` (Fix #7)

## Important Context

**Architecture:**
- VoiceLite = Desktop speech-to-text app (C# WPF .NET 8)
- Web platform = Next.js 15 + Prisma + PostgreSQL
- Licensing model: Free tier (Base model) vs Pro tier ($20 - Small/Medium/Large models)

**Security Fixes Already Applied:**
1. **Race condition** - Prisma transaction prevents concurrent activations from bypassing device limits
2. **Email failure** - Try-catch with recovery logging (customer never loses license)
3. **Model gating** - Server-side validation in PersistentWhisperService.cs (can't bypass by editing Settings.json)
4. **License encryption** - DPAPI encrypts license.dat on disk
5. **Rate limiting** - 100 req/hour on validate endpoint, 10 req/hour on activate endpoint
6. **Privacy** - Email removed from API responses

**Recent Changes:**
- Removed Ed25519 cryptographic signing (wasn't being used)
- Updated all user-facing messages to new architecture (Base free, not Tiny)
- Fixed 24 critical issues from comprehensive audit

## Success Criteria

**Minimum (30 min):**
- [ ] Database connected via Supabase MCP
- [ ] Prisma migration applied successfully
- [ ] Tables created (License, LicenseActivation, WebhookEvent)

**Standard (1 hour):**
- [ ] Test license generated
- [ ] Desktop app activates successfully
- [ ] API endpoints tested (validation + rate limiting)

**Extended (2-3 hours):**
- [ ] All above + 5 additional fixes from START_HERE_FIXES.md
- [ ] Desktop app builds with 0 errors
- [ ] Web platform builds with 0 TypeScript errors
- [ ] All tests pass

## Quick Commands Reference

```bash
# Database
cd voicelite-web
npx prisma db push              # Apply schema to database
npx prisma studio               # Open database GUI
npx prisma migrate dev          # Create new migration

# Desktop App
cd VoiceLite
dotnet clean
dotnet build -c Release
dotnet test

# Web Platform
cd voicelite-web
npm install
npm run dev                     # Start dev server (localhost:3000)
npm run build                   # Build for production

# Git
git status
git log --oneline -5            # See recent commits
git push                        # Push to remote (when ready)
```

## Environment Variables (Already Configured)

```bash
# In voicelite-web/.env
DATABASE_URL="postgresql://postgres:jpcyCSh80D5sG%24iq@db.lvocjzqjqllouzyggpqm.supabase.co:5432/postgres"
RESEND_API_KEY="re_Vn4JijC8_KJGGmrQYBe5QXa9ohEHiGjZn"
UPSTASH_REDIS_REST_URL="https://golden-ibex-26450.upstash.io"
UPSTASH_REDIS_REST_TOKEN="AWdSAAIncDJjMDhkYTUwZWMxZWY0ODM2OTBjOWRmMGQwYTAwYzhiNXAyMjY0NTA"
STRIPE_SECRET_KEY="sk_test_PLACEHOLDER"  # Not configured yet (optional)
```

## Troubleshooting

**If database connection fails:**
- Verify Supabase MCP is connected in the other instance
- Check firewall/network settings
- Try connection pooler port 6543 instead of 5432

**If Prisma migration fails:**
- Run `npx prisma generate` first
- Check DATABASE_URL is correct
- Verify Supabase project is active

**If desktop app won't build:**
- Run `dotnet clean` first
- Check .NET 8 SDK is installed
- Look for missing dependencies

**If TypeScript build fails:**
- Delete `lib/auth/session.ts` (dead code)
- Run `npm install` to refresh packages
- Check `node_modules/.prisma/client` exists

## Questions to Ask Me

If you're unsure about anything, ask:
1. "Should I configure Stripe now or skip it?" (Answer: Skip for now)
2. "Should I do the optional fixes from START_HERE_FIXES.md?" (Your call - minimum is just database)
3. "Should I push to GitHub?" (Answer: Yes, once database is working)

---

**Let's get that database connected and test the license system!** 🚀

**Estimated total time: 30 minutes (database only) to 3 hours (database + optional fixes)**
