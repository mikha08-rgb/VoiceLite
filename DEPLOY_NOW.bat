@echo off
echo ========================================
echo VoiceLite Deployment Script
echo ========================================
echo.
echo This will deploy your website and API to Vercel.
echo.
echo STEP 1: Login to Vercel
echo ------------------------
echo Opening Vercel login in browser...
echo.
npx vercel login
echo.
echo STEP 2: Deploy Landing Page
echo ----------------------------
echo Deploying landing page to voicelite.app...
echo.
cd landing-page
echo When prompted:
echo - Project name: voicelite
echo - Link to existing project: No
echo - Production deployment: Yes
echo.
npx vercel --prod
echo.
echo STEP 3: Deploy API Server
echo -------------------------
echo Deploying API to api.voicelite.app...
echo.
cd ../license-server
echo When prompted:
echo - Project name: voicelite-api
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
echo 2. Add custom domains:
echo    - voicelite.app to 'voicelite' project
echo    - api.voicelite.app to 'voicelite-api' project
echo 3. Update your domain DNS:
echo    A     @     76.76.21.21
echo    CNAME api   cname.vercel-dns.com
echo.
pause