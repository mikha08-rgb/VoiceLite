# 🚀 Professional Deployment Guide for voicelite.app

## Your Premium Domain Setup

### Domain: voicelite.app
- ✅ Perfect for software products
- ✅ Memorable and brandable
- ✅ .app forces HTTPS (more secure)
- ✅ Professional appearance

## Quick Start - Choose Your Path:

### Option A: DigitalOcean (Recommended for Beginners)

#### 1. Create DigitalOcean Account
1. Go to https://www.digitalocean.com
2. Sign up (get $200 free credit with new account)
3. Add payment method

#### 2. Create Droplet (Server)
1. Click "Create" → "Droplets"
2. Choose:
   - Region: New York or San Francisco
   - Image: Ubuntu 22.04 LTS
   - Plan: Basic → Regular → $6/month
   - Authentication: Password (easier) or SSH
   - Hostname: `voicelite-api`
3. Create Droplet
4. Copy your server IP: _______________

#### 3. Quick Server Setup Script
Once you have your server IP, I'll give you a single script to run that sets everything up!

### Option B: Hetzner (Best Value - Advanced)

#### 1. Create Hetzner Account
1. Go to https://www.hetzner.com/cloud
2. Sign up (German company, very reliable)
3. Add payment method

#### 2. Create Server
1. New Project → Add Server
2. Location: Ashburn (USA) or Falkenstein (EU)
3. Image: Ubuntu 22.04
4. Type: CX11 (€4.51/month)
5. Create & get IP

## DNS Configuration

### Step 1: Add DNS Records

Go to your domain registrar and add these records:

| Type | Host | Value | TTL | Purpose |
|------|------|-------|-----|---------|
| A | @ | YOUR_SERVER_IP | 3600 | Main domain |
| A | api | YOUR_SERVER_IP | 3600 | License server |
| A | www | YOUR_SERVER_IP | 3600 | WWW redirect |
| CNAME | app | voicelite.app | 3600 | Downloads |

### Step 2: Cloudflare Setup (Optional but Recommended)

Using Cloudflare provides:
- Free SSL certificates
- DDoS protection
- CDN for faster loading
- Hide your server's real IP

1. Sign up at https://cloudflare.com (free)
2. Add site: voicelite.app
3. Update nameservers at your registrar
4. Configure DNS records in Cloudflare
5. Enable proxy (orange cloud) for all records

## Automated Deployment Script

Once you have your server, run this single script to set up everything:

```bash
#!/bin/bash
# VoiceLite.app Professional Setup Script

# Update system
apt update && apt upgrade -y

# Install Node.js 18
curl -fsSL https://deb.nodesource.com/setup_18.x | bash -
apt-get install -y nodejs

# Install required packages
apt install -y git nginx certbot python3-certbot-nginx ufw

# Install PM2
npm install -g pm2

# Setup firewall
ufw allow OpenSSH
ufw allow 'Nginx Full'
ufw --force enable

# Clone license server
cd /var/www
git clone https://github.com/mikha08-rgb/voicelite-license-server.git
cd voicelite-license-server
npm install

# Create environment file
cat > .env << 'EOL'
API_KEY=CE7038B50A2FC2F91C52D042EAADAA77
ADMIN_KEY=F50BB8D40F0262CFFE40D254B789C317
PORT=3000
DATABASE_PATH=./data/licenses.db
NODE_ENV=production
EOL

# Create data directory
mkdir -p data

# Start with PM2
pm2 start server.js --name voicelite-api
pm2 save
pm2 startup systemd -u root --hp /root

# Configure Nginx for API
cat > /etc/nginx/sites-available/api.voicelite.app << 'EOL'
server {
    listen 80;
    server_name api.voicelite.app;

    location / {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOL

# Configure Nginx for main site
cat > /etc/nginx/sites-available/voicelite.app << 'EOL'
server {
    listen 80;
    server_name voicelite.app www.voicelite.app;
    root /var/www/html;
    index index.html;

    location / {
        try_files $uri $uri/ =404;
    }
}
EOL

# Enable sites
ln -sf /etc/nginx/sites-available/api.voicelite.app /etc/nginx/sites-enabled/
ln -sf /etc/nginx/sites-available/voicelite.app /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default

# Test and reload Nginx
nginx -t && systemctl reload nginx

echo "✅ Server setup complete!"
echo "Next: Run certbot to get SSL certificates"
```

## SSL Setup (After DNS Propagates)

Wait 5-10 minutes for DNS to propagate, then:

```bash
# Get SSL for API
certbot --nginx -d api.voicelite.app --non-interactive --agree-tos -m your-email@example.com

# Get SSL for main domain
certbot --nginx -d voicelite.app -d www.voicelite.app --non-interactive --agree-tos -m your-email@example.com
```

## Upload Landing Page

```bash
# Create web directory
mkdir -p /var/www/html

# Upload your landing page (from local machine)
scp docs/index.html root@YOUR_SERVER_IP:/var/www/html/
scp docs/success.html root@YOUR_SERVER_IP:/var/www/html/
```

## Update VoiceLite Application

### In LicenseManager.cs:
```csharp
private const string LICENSE_SERVER_URL = "https://api.voicelite.app";
private const string API_KEY = "CE7038B50A2FC2F91C52D042EAADAA77";
```

### In PaymentProcessor.cs:
```csharp
private const string LICENSE_SERVER_URL = "https://api.voicelite.app";
```

### In docs/index.html:
Update download links to use voicelite.app

## Testing Your Setup

```bash
# Test API
curl https://api.voicelite.app/api/check

# Test website
curl https://voicelite.app
```

## Professional Email Setup (Optional)

Since you have voicelite.app, you can set up professional email:

### Option 1: Zoho Mail (Free for 5 users)
1. Sign up at https://www.zoho.com/mail/
2. Verify domain ownership
3. Add MX records
4. Create support@voicelite.app

### Option 2: Google Workspace ($6/user/month)
1. More professional
2. Better integration
3. More storage

## Monitoring & Maintenance

### Set Up Monitoring:
```bash
# Install monitoring
pm2 install pm2-logrotate
pm2 set pm2-logrotate:max_size 10M
pm2 set pm2-logrotate:retain 7
```

### UptimeRobot (Free):
1. Sign up at https://uptimerobot.com
2. Add monitor: https://api.voicelite.app/api/check
3. Get alerts if server goes down

### Backup Script:
```bash
# Create backup script
cat > /root/backup.sh << 'EOL'
#!/bin/bash
BACKUP_DIR="/root/backups"
mkdir -p $BACKUP_DIR
tar -czf $BACKUP_DIR/voicelite-backup-$(date +%Y%m%d).tar.gz /var/www/voicelite-license-server/data/
find $BACKUP_DIR -name "*.tar.gz" -mtime +7 -delete
EOL

chmod +x /root/backup.sh

# Add to crontab (daily at 2 AM)
(crontab -l 2>/dev/null; echo "0 2 * * * /root/backup.sh") | crontab -
```

## Your Professional Setup:

### Live URLs:
- Website: https://voicelite.app
- API: https://api.voicelite.app
- Support: support@voicelite.app (if configured)

### Monthly Costs:
- Server: $6/month (DigitalOcean) or €4.51/month (Hetzner)
- Domain: ~$20/year ($1.67/month)
- **Total: ~$8/month**

### Benefits:
- ✅ Professional appearance
- ✅ Full control
- ✅ Scalable
- ✅ Secure
- ✅ Fast loading
- ✅ 99.9% uptime

## You're Ready for Business! 🎉

With voicelite.app properly configured:
- Customers trust your professional domain
- API is secure and fast
- Ready for real sales
- Can scale to thousands of users

This is a production-grade setup!