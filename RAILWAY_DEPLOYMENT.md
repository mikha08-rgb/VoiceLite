# 🚂 Railway Deployment Guide for VoiceLite License Server

## Quick Start (10 minutes)

### Step 1: Create GitHub Repository
```bash
1. Go to https://github.com/new
2. Name: "voicelite-license-server" (PRIVATE repository)
3. Click "Create repository"
4. Copy the repository URL
```

### Step 2: Push License Server to GitHub
```bash
cd license-server
git remote add origin https://github.com/YOUR_USERNAME/voicelite-license-server.git
git branch -M main
git push -u origin main
```

### Step 3: Deploy to Railway
1. Go to https://railway.app
2. Sign up with GitHub (FREE)
3. Click "New Project"
4. Select "Deploy from GitHub repo"
5. Choose "voicelite-license-server"
6. Railway will auto-detect Node.js and start deployment

### Step 4: Set Environment Variables
In Railway dashboard:
1. Click on your project
2. Go to "Variables" tab
3. Add these variables:

```env
API_KEY=GENERATE_A_STRONG_32_CHAR_KEY_HERE
ADMIN_KEY=DIFFERENT_STRONG_32_CHAR_KEY_HERE
PORT=3000
DATABASE_PATH=./data/licenses.db
NODE_ENV=production
```

**Generate Strong Keys:**
Use this tool: https://passwordsgenerator.net/
- Length: 32 characters
- Include: Letters, Numbers
- Exclude: Similar characters

Example keys (DO NOT USE THESE):
- API_KEY: `kP9xT2mN5qR8vB3wY6zC1fG4hJ7sL0aE`
- ADMIN_KEY: `uD2eF5gH8jK1mN4pQ7rT0vW3xY6zB9cA`

### Step 5: Get Your Server URL
1. In Railway dashboard, click on your project
2. Go to "Settings" tab
3. Under "Domains", click "Generate Domain"
4. You'll get a URL like: `voicelite-server.up.railway.app`
5. Test it: `https://voicelite-server.up.railway.app/api/check`

## Update VoiceLite App

### Files to Update:
1. **VoiceLite\VoiceLite\Services\LicenseManager.cs**
   - Line 21: Change `http://localhost:3000` to `https://YOUR-RAILWAY-URL.railway.app`
   - Line 22: Change `your-secret-api-key-change-this` to your API_KEY

2. **VoiceLite\VoiceLite\Services\PaymentProcessor.cs**
   - Line 20: Change `http://localhost:3000` to `https://YOUR-RAILWAY-URL.railway.app`

### Rebuild Application:
```bash
cd VoiceLite
dotnet build VoiceLite.sln -c Release
```

## Test Production Setup

### 1. Check Server Health:
```bash
curl https://YOUR-RAILWAY-URL.railway.app/api/check
```
Should return: `{"status":"ok","timestamp":"..."}`

### 2. Generate Test License:
```bash
# You'll need to do this from your local admin tool with the production server
node admin.js generate test@example.com Personal
```

### 3. Test License Activation:
1. Run VoiceLite.exe
2. Help → Enter License
3. Enter the test license
4. Should activate successfully

## Railway Free Tier Limits
- **Monthly:** 500 hours (enough for 24/7 operation)
- **Memory:** 512MB (plenty for license server)
- **Bandwidth:** 100GB (handles thousands of activations)
- **Cost:** $0 (FREE)

## Production Checklist

✅ **Before Going Live:**
- [ ] Strong API_KEY generated (32+ characters)
- [ ] Different ADMIN_KEY generated
- [ ] Railway deployment successful
- [ ] Health check endpoint working
- [ ] VoiceLite app updated with production URLs
- [ ] Test license activation works
- [ ] Database persists between deployments

✅ **Security:**
- [ ] GitHub repo is PRIVATE
- [ ] Environment variables set in Railway (not in code)
- [ ] API keys are unique and strong
- [ ] No sensitive data in git history

## Troubleshooting

**"Cannot connect to license server"**
- Check Railway deployment logs
- Verify URL in VoiceLite code
- Ensure https:// not http://

**"Unauthorized" error**
- Check API_KEY matches in both Railway and VoiceLite
- Ensure no extra spaces in keys

**Database resets on deploy**
- Use Railway's persistent volume for database
- Or use Railway's PostgreSQL addon (also free)

## Next Steps

Once deployed:
1. ✅ Server running 24/7
2. ✅ Ready for customer activations
3. → Set up Stripe payments
4. → Deploy landing page
5. → Start marketing!

## Support

Railway Documentation: https://docs.railway.app
Status Page: https://railway.app/status
Discord Support: https://discord.gg/railway