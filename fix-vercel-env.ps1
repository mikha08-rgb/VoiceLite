# Fix Vercel Environment Variables
# This script removes the broken variables and adds the correct ones

Write-Host "🔧 Fixing Vercel Environment Variables..." -ForegroundColor Cyan
Write-Host ""

Set-Location "c:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck\voicelite-web"

# Remove the empty CRL_SIGNING_PUBLIC
Write-Host "1. Removing empty CRL_SIGNING_PUBLIC..." -ForegroundColor Yellow
vercel env rm CRL_SIGNING_PUBLIC production

# Remove old MIGRATION_SECRET
Write-Host "2. Removing old MIGRATION_SECRET..." -ForegroundColor Yellow
vercel env rm MIGRATION_SECRET production

Write-Host ""
Write-Host "✅ Removed broken variables" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Now adding correct values..." -ForegroundColor Cyan
Write-Host ""

# Add CRL_SIGNING_PUBLIC with correct value
Write-Host "3. Adding CRL_SIGNING_PUBLIC..." -ForegroundColor Yellow
Write-Host "   Paste this value when prompted:" -ForegroundColor White
Write-Host "   MCowBQYDK2VwAyEA/CC+LhhLoFN+/Z+8bcUaCp5xYQT/gfwyl3v8SsLHr5w=" -ForegroundColor Cyan
Write-Host ""
vercel env add CRL_SIGNING_PUBLIC production

Write-Host ""

# Add MIGRATION_SECRET with correct value
Write-Host "4. Adding MIGRATION_SECRET..." -ForegroundColor Yellow
Write-Host "   Paste this value when prompted:" -ForegroundColor White
Write-Host "   d58d0bec226f20c6d17853891dbb61f0704c66c811062f3b57c89614ce50bbf5" -ForegroundColor Cyan
Write-Host ""
vercel env add MIGRATION_SECRET production

Write-Host ""
Write-Host "✅ All environment variables updated!" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 Now deploying with correct credentials..." -ForegroundColor Cyan
Write-Host ""

# Redeploy to production
vercel --prod

Write-Host ""
Write-Host "🎉 Done! Your app is now deployed with the correct credentials." -ForegroundColor Green
