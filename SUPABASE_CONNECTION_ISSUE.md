# Supabase Connection Issue

The database connection is failing. This is likely because we need the **direct connection string** (not the pooled one).

## Fix This Now

1. **Go to your Supabase dashboard**: [https://supabase.com/dashboard](https://supabase.com/dashboard)
2. **Click on your project**: `voicelite`
3. **Go to**: Project Settings → Database
4. **Scroll down** to the **Connection string** section
5. **Look for two connection strings**:
   - ✅ **Transaction pooling** (port 6543) - This is what you gave me
   - ✅ **Session pooling / Direct connection** (port 5432) - **THIS is what we need**

## What to Copy

You need the **Direct connection** string that looks like:

```
postgresql://postgres.[PROJECT-REF]:[PASSWORD]@aws-0-us-east-1.pooler.supabase.com:5432/postgres
```

OR if there's a "Connection pooling" toggle, make sure it says:

```
Mode: Direct connection
Port: 5432
```

## Paste Here

Copy the direct connection string and paste it here, then I'll update your config.

**Format**: `postgresql://postgres.[something]:[PASSWORD]@[HOST]:5432/postgres`

---

**Alternative**: If you only see one connection string, try changing the port from `5432` to `6543` in what you already gave me.