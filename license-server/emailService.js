const nodemailer = require('nodemailer');

// Email configuration - supports Gmail, SendGrid, or any SMTP
const createTransporter = () => {
    // Option 1: SendGrid (recommended for production)
    if (process.env.SENDGRID_API_KEY) {
        return nodemailer.createTransporter({
            host: 'smtp.sendgrid.net',
            port: 587,
            auth: {
                user: 'apikey',
                pass: process.env.SENDGRID_API_KEY
            }
        });
    }

    // Option 2: Gmail (for testing - requires app password)
    if (process.env.GMAIL_USER && process.env.GMAIL_APP_PASSWORD) {
        return nodemailer.createTransporter({
            service: 'gmail',
            auth: {
                user: process.env.GMAIL_USER,
                pass: process.env.GMAIL_APP_PASSWORD
            }
        });
    }

    // Option 3: Custom SMTP
    if (process.env.SMTP_HOST) {
        return nodemailer.createTransporter({
            host: process.env.SMTP_HOST,
            port: process.env.SMTP_PORT || 587,
            secure: process.env.SMTP_SECURE === 'true',
            auth: {
                user: process.env.SMTP_USER,
                pass: process.env.SMTP_PASS
            }
        });
    }

    console.warn('No email service configured. Emails will not be sent.');
    return null;
};

const transporter = createTransporter();

const sendLicenseEmail = async (to, licenseKey, licenseType) => {
    if (!transporter) {
        console.log(`Would send license ${licenseKey} to ${to} (email not configured)`);
        return false;
    }

    const mailOptions = {
        from: process.env.FROM_EMAIL || 'noreply@voicelite.app',
        to: to,
        subject: 'Welcome to VoiceLite Pro!',
        html: `
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Arial, sans-serif; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; border-radius: 10px 10px 0 0; text-align: center; }
        .content { background: #f7f7f7; padding: 30px; border-radius: 0 0 10px 10px; }
        .license-box { background: white; border: 2px solid #667eea; border-radius: 8px; padding: 20px; margin: 20px 0; text-align: center; }
        .license-key { font-family: 'Courier New', monospace; font-size: 20px; color: #667eea; font-weight: bold; letter-spacing: 1px; }
        .button { display: inline-block; background: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 25px; margin-top: 20px; }
        .steps { background: white; padding: 20px; border-radius: 8px; margin-top: 20px; }
        .steps li { margin: 10px 0; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>Welcome to VoiceLite Pro!</h1>
            <p>Your subscription is now active</p>
        </div>

        <div class="content">
            <p>Thank you for subscribing to VoiceLite Pro! Your activation key is:</p>

            <div class="license-box">
                <div class="license-key">${licenseKey}</div>
            </div>

            <div class="steps">
                <h3>How to Activate:</h3>
                <ol>
                    <li>Download and install VoiceLite if you haven't already</li>
                    <li>Right-click the VoiceLite icon in your system tray</li>
                    <li>Select "License" from the menu</li>
                    <li>Enter your license key above</li>
                    <li>Click "Activate" - you're all set!</li>
                </ol>
            </div>

            <center>
                <a href="https://voicelite.app/downloads/VoiceLite-latest.zip" class="button">Download VoiceLite</a>
            </center>

            <p style="margin-top: 30px; color: #666; font-size: 14px;">
                <strong>Need help?</strong> Contact us at support@voicelite.app<br>
                Please keep this email for your records.
            </p>
        </div>
    </div>
</body>
</html>
        `,
        text: `
Welcome to VoiceLite!

Your ${licenseType} License Key: ${licenseKey}

How to Activate:
1. Download VoiceLite from https://voicelite.app
2. Right-click the system tray icon
3. Select "License"
4. Enter your license key
5. Click "Activate"

Need help? Contact support@voicelite.app

Please keep this email for your records.
        `
    };

    try {
        await transporter.sendMail(mailOptions);
        console.log(`License email sent to ${to}`);
        return true;
    } catch (error) {
        console.error('Failed to send email:', error);
        return false;
    }
};

module.exports = {
    sendLicenseEmail
};