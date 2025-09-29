import { Resend } from 'resend';

const getResendClient = () => {
  return new Resend(process.env.RESEND_API_KEY || 're_placeholder');
};

export async function sendLicenseEmail(email: string, licenseKey: string) {
  try {
    const resend = getResendClient();
    const { data, error } = await resend.emails.send({
      from: 'VoiceLite <support@voicelite.app>',
      to: [email],
      subject: 'Welcome to VoiceLite Pro - Your License Key',
      html: `
        <!DOCTYPE html>
        <html>
        <head>
          <style>
            body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
            .container { max-width: 600px; margin: 0 auto; padding: 20px; }
            .header { background: linear-gradient(to right, #3B82F6, #6366F1); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }
            .content { background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }
            .license-box { background: white; border: 2px solid #3B82F6; padding: 20px; margin: 20px 0; border-radius: 8px; text-align: center; }
            .license-key { font-size: 24px; font-weight: bold; color: #3B82F6; letter-spacing: 2px; margin: 10px 0; }
            .button { display: inline-block; background: #3B82F6; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px; margin: 10px 0; }
            .footer { text-align: center; padding: 20px; color: #666; font-size: 14px; }
            .warning { background: #FEF3C7; border-left: 4px solid #F59E0B; padding: 15px; margin: 20px 0; }
          </style>
        </head>
        <body>
          <div class="container">
            <div class="header">
              <h1>🎉 Welcome to VoiceLite Pro!</h1>
            </div>
            <div class="content">
              <h2>Thank you for your purchase!</h2>
              <p>Your VoiceLite Pro subscription is now active. Here's everything you need to get started:</p>

              <div class="license-box">
                <p style="margin: 0;">Your License Key:</p>
                <div class="license-key">${licenseKey}</div>
                <p style="margin: 0; color: #666; font-size: 14px;">Save this key - you'll need it to activate the app</p>
              </div>

              <h3>🚀 Quick Start Guide:</h3>
              <ol>
                <li><strong>Download VoiceLite Pro</strong><br>
                   <a href="https://voicelite.app/VoiceLite-Pro.exe" class="button">Download VoiceLite Pro</a>
                </li>
                <li><strong>Install the app</strong><br>
                   Run the installer and follow the prompts
                </li>
                <li><strong>Enter your license key</strong><br>
                   Open VoiceLite, go to Settings, and enter your license key
                </li>
                <li><strong>Start using voice typing!</strong><br>
                   Hold Alt (or your custom hotkey) and speak
                </li>
              </ol>

              <div class="warning">
                <strong>⚠️ Important:</strong> Save this email! Your license key is unique and cannot be recovered if lost.
              </div>

              <h3>What's included in Pro:</h3>
              <ul>
                <li>✅ All 5 AI models (tiny, base, small, medium, large)</li>
                <li>✅ Custom hotkey configuration</li>
                <li>✅ Smart text injection with formatting</li>
                <li>✅ Automatic updates</li>
                <li>✅ Priority email support</li>
                <li>✅ 7-day free trial</li>
              </ul>

              <h3>Need help?</h3>
              <p>
                Check our <a href="https://voicelite.app/help">help center</a> or
                reply to this email for priority support.
              </p>

              <p style="margin-top: 30px;">
                <strong>Manage your subscription:</strong><br>
                You can update payment details or cancel anytime through your
                <a href="https://billing.stripe.com/p/login/test">Stripe customer portal</a>.
              </p>
            </div>
            <div class="footer">
              <p>
                VoiceLite - Instant Voice Typing for Windows<br>
                100% Offline • 100% Private<br>
                <a href="https://voicelite.app">voicelite.app</a>
              </p>
            </div>
          </div>
        </body>
        </html>
      `,
      text: `
Welcome to VoiceLite Pro!

Thank you for your purchase! Your VoiceLite Pro subscription is now active.

YOUR LICENSE KEY: ${licenseKey}
(Save this key - you'll need it to activate the app)

Quick Start Guide:
1. Download VoiceLite Pro: https://voicelite.app/VoiceLite-Pro.exe
2. Install the app
3. Enter your license key in Settings
4. Start using voice typing!

What's included in Pro:
- All 5 AI models (tiny, base, small, medium, large)
- Custom hotkey configuration
- Smart text injection with formatting
- Automatic updates
- Priority email support
- 7-day free trial

Need help? Visit https://voicelite.app/help or reply to this email.

Manage your subscription: https://billing.stripe.com/p/login/test

Best regards,
The VoiceLite Team
      `.trim(),
    });

    if (error) {
      console.error('Failed to send license email:', error);
      throw error;
    }

    console.log('License email sent successfully:', data);
    return data;
  } catch (error) {
    console.error('Error sending license email:', error);

    // Fallback: log the license key so it's not lost
    console.log(`FALLBACK: License key for ${email}: ${licenseKey}`);

    // Don't throw error to prevent webhook failure
    // Customer can still be helped manually if email fails
  }
}

export async function sendWelcomeEmail(email: string) {
  try {
    const resend = getResendClient();
    const { data, error } = await resend.emails.send({
      from: 'VoiceLite <support@voicelite.app>',
      to: [email],
      subject: 'Welcome to VoiceLite Free!',
      html: `
        <h1>Welcome to VoiceLite!</h1>
        <p>Thanks for downloading VoiceLite Free. You can now use voice typing in any Windows application!</p>
        <p>Want more features? <a href="https://voicelite.app/#pricing">Upgrade to Pro</a></p>
      `,
    });

    if (error) {
      console.error('Failed to send welcome email:', error);
    }

    return data;
  } catch (error) {
    console.error('Error sending welcome email:', error);
  }
}