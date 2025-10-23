import { Resend } from 'resend';

const resend = new Resend(process.env.RESEND_API_KEY);

export interface LicenseEmailData {
  email: string;
  licenseKey: string;
}

export interface LicenseEmailResult {
  success: boolean;
  messageId?: string;
  error?: unknown;
}

export async function sendLicenseEmail({ email, licenseKey }: LicenseEmailData): Promise<LicenseEmailResult> {
  const fromEmail = process.env.RESEND_FROM_EMAIL || 'noreply@voicelite.app';

  const html = `
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
</head>
<body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;">
  <div style="background: linear-gradient(135deg, #2563eb 0%, #4f46e5 100%); padding: 30px; border-radius: 10px 10px 0 0; text-align: center;">
    <h1 style="color: white; margin: 0; font-size: 28px;">🎤 VoiceLite Pro</h1>
  </div>

  <div style="background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px;">
    <h2 style="color: #1f2937; margin-top: 0;">Thank you for your purchase! 🎉</h2>

    <p style="color: #4b5563; font-size: 16px;">
      Your VoiceLite Pro license is ready. Here's your license key:
    </p>

    <div style="background: white; border: 2px solid #2563eb; border-radius: 8px; padding: 20px; margin: 25px 0; text-align: center;">
      <p style="margin: 0 0 10px 0; color: #6b7280; font-size: 14px; font-weight: 600; text-transform: uppercase; letter-spacing: 1px;">Your License Key</p>
      <p style="margin: 0; font-size: 20px; font-weight: bold; color: #2563eb; font-family: 'Courier New', monospace; word-break: break-all;">
        ${licenseKey}
      </p>
    </div>

    <div style="background: white; border-left: 4px solid #2563eb; padding: 20px; margin: 25px 0; border-radius: 4px;">
      <h3 style="margin-top: 0; color: #1f2937; font-size: 18px;">📝 How to Activate:</h3>
      <ol style="color: #4b5563; padding-left: 20px; margin: 10px 0;">
        <li style="margin: 8px 0;">Open VoiceLite app</li>
        <li style="margin: 8px 0;">Go to <strong>Settings → License</strong></li>
        <li style="margin: 8px 0;">Paste your license key</li>
        <li style="margin: 8px 0;">Click <strong>"Activate"</strong></li>
      </ol>
    </div>

    <div style="background: #f0f9ff; border: 1px solid #bae6fd; padding: 20px; margin: 25px 0; border-radius: 8px;">
      <h3 style="margin-top: 0; color: #1f2937; font-size: 18px;">✨ What's Included:</h3>
      <ul style="color: #4b5563; padding-left: 20px; margin: 10px 0; list-style: none;">
        <li style="margin: 8px 0;">✓ <strong>All 5 AI models</strong> (Tiny, Swift, Pro, Elite, Ultra)</li>
        <li style="margin: 8px 0;">✓ <strong>90-98% accuracy</strong> (vs 80-85% free tier)</li>
        <li style="margin: 8px 0;">✓ <strong>Lifetime updates</strong></li>
        <li style="margin: 8px 0;">✓ <strong>Commercial use allowed</strong></li>
        <li style="margin: 8px 0;">✓ <strong>Priority email support</strong></li>
      </ul>
    </div>

    <div style="text-align: center; margin: 30px 0;">
      <a href="https://github.com/mikha08-rgb/VoiceLite/releases/latest"
         style="display: inline-block; background: #2563eb; color: white; padding: 14px 32px; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 16px;">
        Download Latest Version
      </a>
    </div>

    <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 30px 0;">

    <p style="color: #6b7280; font-size: 14px; margin: 10px 0;">
      <strong>Need help?</strong> Just reply to this email and we'll assist you.
    </p>

    <p style="color: #6b7280; font-size: 14px; margin: 10px 0;">
      Thank you for supporting VoiceLite!
    </p>

    <p style="color: #6b7280; font-size: 14px; margin: 10px 0;">
      – The VoiceLite Team
    </p>
  </div>

  <div style="text-align: center; margin-top: 20px; padding: 20px; color: #9ca3af; font-size: 12px;">
    <p style="margin: 5px 0;">© ${new Date().getFullYear()} VoiceLite. All rights reserved.</p>
    <p style="margin: 5px 0;">
      <a href="https://voicelite.app" style="color: #2563eb; text-decoration: none;">voicelite.app</a>
    </p>
  </div>
</body>
</html>
  `.trim();

  const text = `
VoiceLite Pro - Thank You for Your Purchase!

Your License Key: ${licenseKey}

How to Activate:
1. Open VoiceLite app
2. Go to Settings → License
3. Paste your license key
4. Click "Activate"

What's Included:
✓ All 5 AI models (Tiny, Swift, Pro, Elite, Ultra)
✓ 90-98% accuracy (vs 80-85% free tier)
✓ Lifetime updates
✓ Commercial use allowed
✓ Priority email support

Download latest version:
https://github.com/mikha08-rgb/VoiceLite/releases/latest

Need help? Just reply to this email.

– The VoiceLite Team
`.trim();

  try {
    const result = await resend.emails.send({
      from: `VoiceLite <${fromEmail}>`,
      to: email,
      subject: 'Your VoiceLite Pro License Key',
      html,
      text,
    });

    return { success: true, messageId: result.data?.id };
  } catch (error) {
    console.error('Failed to send license email:', error);
    return { success: false, error };
  }
}
