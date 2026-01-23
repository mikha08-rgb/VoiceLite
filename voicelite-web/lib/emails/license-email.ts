import { Resend } from 'resend';

// Singleton Resend client - lazy initialized for serverless compatibility
let resendClient: Resend | null = null;

function getResendClient(): Resend {
  // Return cached client if already initialized
  if (resendClient) {
    return resendClient;
  }

  const key = process.env.RESEND_API_KEY;
  if (!key || key === 're_placeholder') {
    // Log detailed error for debugging in production
    console.error('RESEND_API_KEY missing or placeholder. Email sending will fail.');
    throw new Error('RESEND_API_KEY must be configured for email sending');
  }

  // Create and cache client
  resendClient = new Resend(key);
  console.log('📧 Resend client initialized');
  return resendClient;
}

export interface LicenseEmailData {
  email: string;
  licenseKey: string;
}

export interface LicenseEmailResult {
  success: boolean;
  messageId?: string;
  error?: unknown;
  attempts?: number;
}

/**
 * Retry configuration for email sending
 * Uses exponential backoff: 1s, 2s, 4s delays between attempts
 */
const RETRY_CONFIG = {
  maxAttempts: 3,
  baseDelayMs: 1000,
  maxDelayMs: 10000,
};

/**
 * Delay helper with exponential backoff
 */
async function delay(attempt: number): Promise<void> {
  const delayMs = Math.min(
    RETRY_CONFIG.baseDelayMs * Math.pow(2, attempt),
    RETRY_CONFIG.maxDelayMs
  );
  return new Promise(resolve => setTimeout(resolve, delayMs));
}

export async function sendLicenseEmail({ email, licenseKey }: LicenseEmailData): Promise<LicenseEmailResult> {
  const fromEmail = process.env.RESEND_FROM_EMAIL || 'VoiceLite <basementhustlellc@gmail.com>';

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

  // HIGH-1 FIX: Retry with exponential backoff (3 attempts)
  // Previously relied on Stripe webhook retries which is unreliable
  let lastError: unknown;

  for (let attempt = 0; attempt < RETRY_CONFIG.maxAttempts; attempt++) {
    try {
      if (attempt > 0) {
        console.log(`📧 Resend API: Retry attempt ${attempt + 1}/${RETRY_CONFIG.maxAttempts} for ${email}`);
        await delay(attempt - 1); // Exponential backoff before retry
      } else {
        console.log(`📧 Resend API: Sending email to ${email} from ${fromEmail}`);
      }

      const resend = getResendClient();
      const result = await resend.emails.send({
        from: fromEmail,
        to: email,
        subject: 'Your VoiceLite Pro License Key',
        html,
        text,
      });

      console.log(`✅ Resend API response:`, {
        success: true,
        messageId: result.data?.id,
        email: email,
        attempt: attempt + 1,
      });

      return { success: true, messageId: result.data?.id, attempts: attempt + 1 };
    } catch (error) {
      lastError = error;
      console.error(`❌ Email attempt ${attempt + 1}/${RETRY_CONFIG.maxAttempts} failed:`, {
        email,
        error: error instanceof Error ? error.message : String(error),
      });

      // Don't retry on configuration errors (API key issues)
      if (error instanceof Error && error.message.includes('RESEND_API_KEY')) {
        console.error('Configuration error - not retrying');
        break;
      }
    }
  }

  // All retries exhausted
  console.error('❌ Failed to send license email after all retries:', {
    email,
    attempts: RETRY_CONFIG.maxAttempts,
    error: lastError instanceof Error ? lastError.message : String(lastError),
    errorDetails: lastError,
  });
  return { success: false, error: lastError, attempts: RETRY_CONFIG.maxAttempts };
}
