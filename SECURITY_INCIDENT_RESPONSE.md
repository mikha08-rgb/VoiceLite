# Security Incident Response - Exposed Credentials

**Date**: October 19, 2025
**Severity**: CRITICAL
**Status**: IN PROGRESS

## Incident Summary

GitGuardian detected exposed Stripe webhook secret in GitHub repository. Git history audit revealed additional production credentials exposed in commit `2e9ec9abe9a3fce89b5af70fd6ff6db306c39ae7`.

## Exposed Credentials

| Credential | Type | Severity | Status |
|------------|------|----------|--------|
| `whsec_e9U0n3DBo6KcaKK1s8WRHTdXQvWeHPJu` | Stripe Webhook Secret | CRITICAL | ❌ Active |
| `o!BQ%y8Y!O8$8EB4` | Database Password | CRITICAL | ❌ Active |
| `re_Vn4JijC8_...` | Resend API Key | HIGH | ❌ Active |
| `AWdSAAInc...` | Upstash Redis Token | MEDIUM | ❌ Active |

## Attack Vectors

1. **Stripe Webhook Forgery**: Attacker can forge payment events to create unlimited Pro licenses
2. **Database Access**: Direct PostgreSQL access with full read/write permissions
3. **Email Spoofing**: Send emails from voicelite.app domain
4. **Rate Limit Bypass**: Circumvent API rate limiting

## Remediation Steps

### Phase 1: Immediate Cleanup (0-2 hours)

#### Step 1: Delete Files from Working Directory
```bash
cd "c:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
rm STRIPE_PAYMENT_COMPLETE.md
rm READY_TO_DEPLOY.md
rm STRIPE_SETUP_COMPLETE.md
git add -A
git commit -m "security: remove documentation containing production credentials"
git push
```

#### Step 2: Clean Git History with BFG
```bash
# Download BFG (if not installed)
# https://rtyley.github.io/bfg-repo-cleaner/

# Create backup
cd ..
cp -r "HereWeGoAgain v3.3 Fuck" "VoiceLite-BACKUP-2025-10-19"

# Create secrets file
cd "HereWeGoAgain v3.3 Fuck"
cat > secrets.txt << EOF
whsec_e9U0n3DBo6KcaKK1s8WRHTdXQvWeHPJu
o!BQ%y8Y!O8$8EB4
re_Vn4JijC8_KJGGmrQYBe5QXa9ohEHiGjZn
AWdSAAIncDJjMDhkYTUwZWMxZWY0ODM2OTBjOWRmMGQwYTAwYzhiNXAyMjY0NTA
aws-1-us-east-1.pooler.supabase.com
postgres.lvocjzqjqllouzyggpqm
EOF

# Run BFG (replace with actual path to bfg.jar)
java -jar bfg.jar --replace-text secrets.txt --no-blob-protection .git

# Clean up
git reflog expire --expire=now --all
git gc --prune=now --aggressive

# Force push (CAUTION: This rewrites history)
git push --force origin master

# Clean up secrets file
rm secrets.txt
```

**Alternative (Without BFG)**: Use git filter-branch
```bash
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch STRIPE_PAYMENT_COMPLETE.md READY_TO_DEPLOY.md STRIPE_SETUP_COMPLETE.md" \
  --prune-empty -- --all

git reflog expire --expire=now --all
git gc --prune=now --aggressive
git push --force origin master
```

### Phase 2: Credential Rotation (2-4 hours)

#### 1. Rotate Stripe Webhook Secret

**Stripe Dashboard**:
1. Go to: https://dashboard.stripe.com/webhooks
2. Find webhook endpoint: `https://voicelite.app/api/webhook`
3. Click "..." menu → "Roll signing secret"
4. Copy new secret (starts with `whsec_`)

**Update Vercel**:
```bash
cd voicelite-web
vercel env rm STRIPE_WEBHOOK_SECRET production
vercel env add STRIPE_WEBHOOK_SECRET production
# Paste new secret when prompted
```

**Update Local**:
```bash
# Edit .env.local and .env.production
STRIPE_WEBHOOK_SECRET="whsec_NEW_SECRET_HERE"
```

**Redeploy**:
```bash
vercel --prod
```

**Test**:
1. Stripe Dashboard → Webhooks → Your endpoint → "Send test webhook"
2. Select "checkout.session.completed"
3. Verify webhook succeeds (200 OK)

#### 2. Rotate Database Password

**Supabase Dashboard**:
1. Go to: https://supabase.com/dashboard/project/lvocjzqjqllouzyggpqm/settings/database
2. Click "Reset Database Password"
3. Copy new password
4. Update connection strings:
   - Pooler: `postgresql://postgres.lvocjzqjqllouzyggpqm:NEW_PASSWORD@aws-1-us-east-1.pooler.supabase.com:6543/postgres?pgbouncer=true&connection_limit=1`
   - Direct: `postgresql://postgres:NEW_PASSWORD@db.dzgqyytpkvjguxlhcpgl.supabase.co:5432/postgres`

**Update Vercel**:
```bash
vercel env rm DATABASE_URL production
vercel env rm DIRECT_DATABASE_URL production
vercel env add DATABASE_URL production
# Paste new pooler URL
vercel env add DIRECT_DATABASE_URL production
# Paste new direct URL
```

**Update Local**:
```bash
# Edit .env.local and .env.production
DATABASE_URL="postgresql://postgres.lvocjzqjqllouzyggpqm:NEW_PASSWORD@..."
DIRECT_DATABASE_URL="postgresql://postgres:NEW_PASSWORD@..."
```

**Test**:
```bash
cd voicelite-web
npx prisma db pull  # Should succeed
```

**Redeploy**:
```bash
vercel --prod
```

#### 3. Rotate Resend API Key

**Resend Dashboard**:
1. Go to: https://resend.com/api-keys
2. Find key named for VoiceLite
3. Click "Delete" to revoke old key
4. Click "Create API Key"
5. Name: "VoiceLite Production"
6. Copy new key (starts with `re_`)

**Update Vercel**:
```bash
vercel env rm RESEND_API_KEY production
vercel env add RESEND_API_KEY production
# Paste new key
```

**Update Local**:
```bash
# Edit .env.local and .env.production
RESEND_API_KEY="re_NEW_KEY_HERE"
```

**Test**:
```bash
# Send test email via API
curl -X POST https://voicelite.app/api/test-email \
  -H "Content-Type: application/json" \
  -d '{"to":"your-email@example.com"}'
```

**Redeploy**:
```bash
vercel --prod
```

#### 4. Rotate Upstash Redis Token

**Upstash Dashboard**:
1. Go to: https://console.upstash.com/redis/golden-ibex-26450
2. Go to "Details" tab
3. Scroll to "REST API" section
4. Click "Rotate Token"
5. Copy new token

**Update Vercel**:
```bash
vercel env rm UPSTASH_REDIS_REST_TOKEN production
vercel env add UPSTASH_REDIS_REST_TOKEN production
# Paste new token
```

**Update Local**:
```bash
# Edit .env.local and .env.production
UPSTASH_REDIS_REST_TOKEN="NEW_TOKEN_HERE"
```

**Test**:
```bash
# Make rate-limited API request
curl https://voicelite.app/api/license/validate \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{"licenseKey":"test","hardwareId":"test"}'
# Should return rate limit info in headers
```

**Redeploy**:
```bash
vercel --prod
```

### Phase 3: Monitoring (Ongoing)

#### Check for Unauthorized Access

**Stripe**:
- Review webhook logs: https://dashboard.stripe.com/webhooks
- Check for suspicious `checkout.session.completed` events
- Verify all payments have corresponding Stripe dashboard entries

**Supabase**:
- Check query logs: Project Settings → Logs
- Look for unusual queries or IP addresses
- Review license table for unexpected modifications

**Resend**:
- Check email logs: https://resend.com/logs
- Verify all sent emails are legitimate

**Upstash**:
- Monitor request patterns: https://console.upstash.com/redis/golden-ibex-26450
- Check for unusual spikes in requests

#### Verify No Fraudulent Licenses

```bash
cd voicelite-web
npx prisma studio
# Check licenses table for:
# - Unexpected Pro licenses
# - Unusual creation dates
# - Suspicious email addresses
```

### Phase 4: Prevention (1 week)

#### Install Pre-commit Secret Scanning

```bash
# Install gitleaks
# Windows: https://github.com/gitleaks/gitleaks/releases
# Download gitleaks.exe to a directory in PATH

# Create .gitleaks.toml
cat > .gitleaks.toml << EOF
title = "VoiceLite Secret Scanning"

[extend]
useDefault = true

[[rules]]
id = "stripe-webhook-secret"
description = "Stripe Webhook Secret"
regex = '''whsec_[a-zA-Z0-9]{32,}'''
tags = ["key", "stripe"]

[[rules]]
id = "resend-api-key"
description = "Resend API Key"
regex = '''re_[a-zA-Z0-9]{32,}'''
tags = ["key", "resend"]

[[rules]]
id = "upstash-token"
description = "Upstash Redis Token"
regex = '''[A-Za-z0-9]{50,}'''
tags = ["key", "upstash"]
EOF

# Add pre-commit hook
cat > .git/hooks/pre-commit << EOF
#!/bin/bash
gitleaks protect --staged --verbose
EOF

chmod +x .git/hooks/pre-commit
```

#### Update Documentation Policy

Create `CONTRIBUTING.md`:
```markdown
# Security Guidelines

## NEVER Commit:
- Real API keys, tokens, or secrets
- Production credentials
- Database passwords
- `.env` files (except `.env.example`)
- Files containing actual environment variables

## Use Instead:
- Placeholder values: `STRIPE_SECRET_KEY="sk_live_YOUR_KEY_HERE"`
- Environment variable names: `STRIPE_SECRET_KEY` (without value)
- Template files: `.env.example` with fake/placeholder values
```

## Verification Checklist

After completing all steps, verify:

- [ ] Documentation files deleted from working directory
- [ ] Git history cleaned (secrets no longer found with `git log -S "whsec_"`)
- [ ] Stripe webhook secret rotated and tested
- [ ] Database password rotated and tested
- [ ] Resend API key rotated and tested
- [ ] Upstash Redis token rotated and tested
- [ ] All services redeployed to production
- [ ] No unauthorized licenses in database
- [ ] Pre-commit hooks installed
- [ ] Team notified of new credentials

## Timeline

- **Discovery**: October 19, 2025 (GitGuardian alert)
- **Audit Complete**: October 19, 2025
- **Remediation Start**: October 19, 2025
- **Target Completion**: October 20, 2025 (24 hours)

## Estimated Downtime

- **During credential rotation**: ~5-10 minutes per service
- **Total expected downtime**: <30 minutes
- **Recommended maintenance window**: Off-peak hours

## Communication

**Internal Team**:
- Notify all developers of credential rotation
- Share new `.env.production` via secure channel (1Password, encrypted email)
- Update deployment documentation

**External**:
- No public disclosure required (no evidence of exploitation)
- Monitor support channels for user-reported issues during rotation

## Lessons Learned

1. **Never commit documentation with real credentials** - Use placeholder values or environment variable references only
2. **Implement pre-commit scanning** - Catch secrets before they reach git history
3. **Regular security audits** - Quarterly git history scans
4. **Credential rotation schedule** - Rotate production secrets every 90 days
5. **Secrets management** - Consider using Vercel Secrets, AWS Secrets Manager, or 1Password for team sharing

## References

- GitGuardian Alert: [Link to alert]
- Git History Audit Report: `GIT_HISTORY_AUDIT_REPORT.md`
- Commit with exposure: `2e9ec9abe9a3fce89b5af70fd6ff6db306c39ae7`

---

**Response Lead**: Mikhail Lev
**Last Updated**: October 19, 2025
