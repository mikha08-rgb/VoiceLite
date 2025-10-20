# Admin API Endpoints

## ⚠️ SECURITY WARNING

These endpoints are **DEVELOPMENT ONLY** and are automatically disabled in production.

## Generate Test License

**Endpoint:** `POST /api/admin/generate-test-license`

**Purpose:** Generate test licenses for local development and testing without going through Stripe.

### Requirements

1. **Environment must be development:**
   ```bash
   NODE_ENV=development
   ```

2. **Admin secret must be configured in `.env.local`:**
   ```bash
   ADMIN_SECRET=your-strong-secret-here
   ```

### Usage

**Local Development:**
```bash
curl -X POST http://localhost:3000/api/admin/generate-test-license \
  -H "x-admin-secret: your-strong-secret-here" \
  -H "Content-Type: application/json" \
  -d '{"email": "test@example.com"}'
```

**Response:**
```json
{
  "success": true,
  "message": "Test license generated successfully",
  "license": {
    "key": "VL-A1B2-C3D4-E5F6-G7H8",
    "email": "test@example.com",
    "type": "LIFETIME",
    "status": "ACTIVE"
  }
}
```

### Error Responses

**Production Environment (403):**
```json
{
  "error": "This endpoint is disabled in production. Use Stripe webhooks to generate licenses."
}
```

**Invalid Admin Secret (401):**
```json
{
  "error": "Unauthorized - Invalid admin secret"
}
```

**Invalid Email (400):**
```json
{
  "error": "Invalid email format"
}
```

### Security Features

1. ✅ **Disabled in production** - Returns 403 if `NODE_ENV=production`
2. ✅ **Requires admin secret** - Must provide valid `ADMIN_SECRET` via header
3. ✅ **Email validation** - Validates email format
4. ✅ **Duplicate detection** - Checks for key collisions
5. ✅ **Audit logging** - Logs all license generations
6. ✅ **Rate limited** - Via Vercel's built-in rate limiting

### Testing Flow

1. Generate a test license using this endpoint
2. Copy the license key from the response
3. Launch VoiceLite desktop app
4. Click "Activate License" when prompted
5. Paste the license key
6. App will call `/api/licenses/activate` to activate it
7. License is now bound to your machine

### Production License Generation

In production, licenses are **ONLY** generated via:
- Stripe webhook (`/api/webhook`)
- When a customer purchases Pro for $20

**Never use this endpoint in production!**
