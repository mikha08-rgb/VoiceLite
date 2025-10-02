@echo off
REM Set environment variables from .env.local
set DATABASE_URL=postgresql://postgres.kkjfmnwjchlugzxlqipw:jY%%26%%23DvbBo2a%%25Oo%%2Az@aws-1-us-east-2.pooler.supabase.com:6543/postgres?pgbouncer=true
set DIRECT_DATABASE_URL=postgresql://postgres:jY%%26%%23DvbBo2a%%25Oo%%2Az@db.kkjfmnwjchlugzxlqipw.supabase.co:5432/postgres

REM Run Prisma db push (doesn't require direct URL)
npx prisma db push
