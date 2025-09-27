# 🌐 Professional Hosting Setup with Your Domain

## Why Use Your Own Domain + VPS

### Professional Benefits:
- ✅ `api.yourdomain.com` looks professional
- ✅ Customers trust real domains more
- ✅ Better for SEO and marketing
- ✅ Full control over your infrastructure
- ✅ Can add email later (support@yourdomain.com)

## Recommended Setup: Hetzner Cloud

### Why Hetzner:
- **Best value**: €4.51/month for 2GB RAM (others charge $10+)
- **German engineering**: Reliable, fast, secure
- **Great network**: Low latency worldwide
- **Easy setup**: One-click apps available

## Step-by-Step Setup Guide

### 1. Create Hetzner Account
1. Go to https://www.hetzner.com/cloud
2. Sign up (immediate activation)
3. Add payment method

### 2. Create Server
1. Click "New Project"
2. Add Server:
   - Location: Choose closest to your customers (Ashburn for US)
   - Image: Ubuntu 22.04
   - Type: CX11 (€4.51/month)
   - Name: voicelite-api
3. Create & get IP address

### 3. Initial Server Setup
```bash
# SSH into server
ssh root@YOUR_SERVER_IP

# Update system
apt update && apt upgrade -y

# Install Node.js
curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
apt-get install -y nodejs

# Install PM2 (process manager)
npm install -g pm2

# Install git
apt install git -y

# Create app directory
mkdir -p /var/www
cd /var/www

# Clone your license server
git clone https://github.com/mikha08-rgb/voicelite-license-server.git
cd voicelite-license-server

# Install dependencies
npm install

# Create .env file
cat > .env << EOL
API_KEY=CE7038B50A2FC2F91C52D042EAADAA77
ADMIN_KEY=F50BB8D40F0262CFFE40D254B789C317
PORT=3000
DATABASE_PATH=./data/licenses.db
NODE_ENV=production
EOL

# Start with PM2
pm2 start server.js --name voicelite-api
pm2 save
pm2 startup
```

### 4. Install Nginx (Reverse Proxy)
```bash
# Install Nginx
apt install nginx -y

# Create config
cat > /etc/nginx/sites-available/api << EOL
server {
    listen 80;
    server_name api.yourdomain.com;

    location / {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host \$host;
        proxy_cache_bypass \$http_upgrade;
    }
}
EOL

# Enable site
ln -s /etc/nginx/sites-available/api /etc/nginx/sites-enabled/
nginx -t
systemctl restart nginx
```

### 5. Setup SSL with Let's Encrypt
```bash
# Install Certbot
apt install certbot python3-certbot-nginx -y

# Get SSL certificate
certbot --nginx -d api.yourdomain.com

# Auto-renewal is configured automatically
```

### 6. Configure Firewall
```bash
# Setup UFW firewall
ufw allow OpenSSH
ufw allow 'Nginx Full'
ufw enable
```

## Domain Configuration

### At Your Domain Provider:

Create these DNS records:

| Type | Name | Value | TTL |
|------|------|-------|-----|
| A | @ | YOUR_SERVER_IP | 3600 |
| A | api | YOUR_SERVER_IP | 3600 |
| A | www | YOUR_SERVER_IP | 3600 |
| CNAME | app | yourdomain.com | 3600 |

### If Using Cloudflare (Recommended):

1. Add site to Cloudflare (free)
2. Change nameservers at domain registrar
3. Add A records in Cloudflare
4. Enable proxy (orange cloud)
5. Set SSL/TLS to "Full (strict)"

## Final URLs

Your production setup:
- Landing Page: `https://yourdomain.com`
- API Server: `https://api.yourdomain.com`
- Downloads: `https://app.yourdomain.com`
- Support: `support@yourdomain.com` (later)

## Update VoiceLite Code

### LicenseManager.cs
```csharp
private const string LICENSE_SERVER_URL = "https://api.yourdomain.com";
private const string API_KEY = "CE7038B50A2FC2F91C52D042EAADAA77";
```

### PaymentProcessor.cs
```csharp
private const string LICENSE_SERVER_URL = "https://api.yourdomain.com";
```

## Testing Your Setup

```bash
# From your local machine
curl https://api.yourdomain.com/api/check

# Should return
{"status":"ok","timestamp":"...","version":"1.0.0"}
```

## Monitoring

### Server Monitoring
```bash
# Check PM2 status
pm2 status

# View logs
pm2 logs voicelite-api

# Monitor resources
htop
```

### Uptime Monitoring
1. Sign up for UptimeRobot (free)
2. Add monitor for https://api.yourdomain.com/api/check
3. Get alerts if server goes down

## Backup Strategy

### Automated Backups
```bash
# Create backup script
cat > /root/backup.sh << EOL
#!/bin/bash
tar -czf /root/backup-\$(date +%Y%m%d).tar.gz /var/www/voicelite-license-server/data/
# Keep only last 7 days
find /root -name "backup-*.tar.gz" -mtime +7 -delete
EOL

chmod +x /root/backup.sh

# Add to crontab (daily at 2 AM)
echo "0 2 * * * /root/backup.sh" | crontab -
```

## Monthly Costs

- Hetzner Server: €4.51 (~$5)
- Domain: ~$12/year ($1/month)
- **Total: ~$6/month**

Compare to:
- Railway: Limited free tier, then $5+/month
- Heroku: $7+/month
- AWS: $10+/month

## Security Checklist

- ✅ SSL certificate installed
- ✅ Firewall configured
- ✅ Regular updates scheduled
- ✅ Backups automated
- ✅ Monitoring active
- ✅ API keys in environment variables
- ✅ Database not exposed

## You're Professional Now! 🎉

With your own domain and VPS:
- Looks professional to customers
- Full control over everything
- Better performance
- Can scale as needed
- Ready for real business

This is how real SaaS products are deployed!