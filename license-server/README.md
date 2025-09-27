# VoiceLite License Server

## Quick Start (Local Testing)

1. Install dependencies:
```bash
npm install
```

2. Run server:
```bash
npm start
```

3. Test endpoints:
```bash
# Health check
curl http://localhost:3000/api/check

# Generate license (admin)
curl -X POST http://localhost:3000/api/generate \
  -H "x-admin-key: admin-secret-key-change-this" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","license_type":"Personal"}'
```

## Deploy to Railway.app (FREE)

1. Create account at https://railway.app
2. Connect GitHub repository
3. Click "New Project" → "Deploy from GitHub repo"
4. Select this repository
5. Add environment variables:
   - `API_KEY` = generate-random-string
   - `ADMIN_KEY` = generate-different-random-string
6. Click "Deploy"
7. Get your URL from Railway dashboard

## Deploy to Heroku (FREE tier ending)

1. Install Heroku CLI
2. Run:
```bash
heroku create voicelite-licenses
heroku config:set API_KEY=your-api-key
heroku config:set ADMIN_KEY=your-admin-key
git push heroku main
```

## API Endpoints

### Public Endpoints

#### GET /api/check
Health check - no authentication required

### App Endpoints (requires x-api-key header)

#### POST /api/activate
Activate a license for a device
```json
{
  "license_key": "PERS-XXXX-XXXX-XXXX",
  "email": "user@example.com",
  "machine_id": "ABC123"
}
```

#### POST /api/validate
Check if license is valid for device
```json
{
  "license_key": "PERS-XXXX-XXXX-XXXX",
  "machine_id": "ABC123"
}
```

### Admin Endpoints (requires x-admin-key header)

#### POST /api/generate
Generate new license key
```json
{
  "email": "customer@example.com",
  "license_type": "Personal"  // Trial, Personal, Pro, Business
}
```

#### POST /api/revoke
Revoke a license
```json
{
  "license_key": "PERS-XXXX-XXXX-XXXX"
}
```

#### GET /api/stats
Get licensing statistics

## Environment Variables

- `PORT` - Server port (default: 3000)
- `API_KEY` - Required for app endpoints
- `ADMIN_KEY` - Required for admin endpoints

## Security Notes

1. **IMPORTANT**: Change default API keys before deploying!
2. Use HTTPS in production (Railway/Heroku provide this)
3. Consider rate limiting for production
4. Add IP whitelisting for admin endpoints if needed

## Database

Uses SQLite for simplicity. The database file (`licenses.db`) is created automatically.

For production, consider migrating to PostgreSQL:
1. Railway provides free PostgreSQL
2. Update connection string in code
3. Use `pg` package instead of `sqlite3`

## Testing with VoiceLite App

Update `PaymentProcessor.cs` in VoiceLite:
```csharp
private const string LICENSE_SERVER_URL = "https://your-railway-app.up.railway.app";
```

Add the API key to requests:
```csharp
httpClient.DefaultRequestHeaders.Add("x-api-key", "your-api-key");
```

## Monitoring

1. Use UptimeRobot (free) to monitor uptime
2. Check Railway/Heroku logs for errors
3. Set up email alerts for failures

## Backup

Railway automatically backs up your database. For extra safety:
1. Download SQLite database weekly
2. Export to CSV for records
3. Keep customer email list separate