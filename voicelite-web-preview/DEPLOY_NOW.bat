@echo off
echo ========================================
echo VoiceLite Deployment Script
echo ========================================
echo.
echo This will deploy the Next.js web platform to Vercel.
echo.
echo STEP 1: Login to Vercel
echo ------------------------
echo Opening Vercel login in browser...
echo.
npx vercel login
echo.
echo STEP 2: Deploy Web Platform
echo ----------------------------
echo Deploying Next.js platform to voicelite.app...
echo.
cd voicelite-web
echo When prompted:
echo - Project name: voicelite
echo - Link to existing project: No
echo - Production deployment: Yes
echo.
npx vercel --prod
echo.
echo ========================================
echo DEPLOYMENT COMPLETE!
echo ========================================
echo.
echo Next steps:
echo 1. Go to https://vercel.com/dashboard
echo 2. Add custom domain voicelite.app to your project
echo 3. Configure environment variables in Vercel dashboard:
echo    - DATABASE_URL (PostgreSQL connection string)
echo    - STRIPE_SECRET_KEY
echo    - LICENSE_SIGNING_PRIVATE_B64
echo    - RESEND_API_KEY
echo    - UPSTASH_REDIS_REST_URL
echo 4. Run database migrations: npm run db:migrate:deploy
echo.
pause