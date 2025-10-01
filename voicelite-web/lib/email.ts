import { Resend } from 'resend';

const FROM_ADDRESS = process.env.RESEND_FROM_EMAIL ?? 'VoiceLite <support@voicelite.app>';

function getResendClient() {
  const apiKey = process.env.RESEND_API_KEY;
  if (!apiKey || apiKey === 're_placeholder') {
    return null;
  }
  return new Resend(apiKey);
}

interface MagicLinkParams {
  email: string;
  magicLinkUrl: string;
  otpCode: string;
  deepLinkUrl: string;
  expiresInMinutes: number;
}

export async function sendMagicLinkEmail({
  email,
  magicLinkUrl,
  otpCode,
  deepLinkUrl,
  expiresInMinutes,
}: MagicLinkParams) {
  console.log('[Email] Attempting to send magic link to:', email);
  console.log('[Email] FROM_ADDRESS:', FROM_ADDRESS);
  console.log('[Email] Has RESEND_API_KEY:', !!process.env.RESEND_API_KEY);

  const resend = getResendClient();
  const html = `
    <h1>Sign in to VoiceLite</h1>
    <p>Click the button below to finish signing in. This link expires in <strong>${expiresInMinutes} minutes</strong>.</p>
    <p><a href="${magicLinkUrl}" style="display:inline-block;padding:12px 24px;background:#2563eb;color:#fff;border-radius:6px;text-decoration:none;">Sign in</a></p>
    <p>If you installed the desktop app, you can also open this link directly: <a href="${deepLinkUrl}">${deepLinkUrl}</a></p>
    <hr />
    <p><strong>Need a code instead?</strong> Enter this one-time code in the app:</p>
    <p style="font-size:24px;font-weight:bold;letter-spacing:4px;">${otpCode}</p>
    <p>The code expires in ${expiresInMinutes} minutes.</p>
  `;

  if (!resend) {
    console.error('CRITICAL: RESEND_API_KEY not configured - cannot send magic link emails');
    console.log('[Email stub] Magic link for %s', email, { magicLinkUrl, deepLinkUrl, otpCode });
    throw new Error('Email service not configured. RESEND_API_KEY is required.');
  }

  console.log('[Email] Calling Resend API...');
  const { error } = await resend.emails.send({
    from: FROM_ADDRESS,
    to: [email],
    subject: 'Your VoiceLite sign-in link',
    html,
    text: `Sign in to VoiceLite\n\nUse this link (expires in ${expiresInMinutes} minutes): ${magicLinkUrl}\nDesktop link: ${deepLinkUrl}\n\nOr enter this one-time code: ${otpCode}`,
  });

  if (error) {
    console.error('Failed to send magic link email:', error);
    throw error;
  }

  console.log('[Email] Magic link email sent successfully to:', email);
}

interface LicenseEmailParams {
  email: string;
  licenseKey: string;
  plan: 'subscription' | 'lifetime';
}

export async function sendLicenseEmail({ email, licenseKey, plan }: LicenseEmailParams) {
  const resend = getResendClient();
  const planLabel = plan === 'subscription' ? 'Quarterly Subscription' : 'Lifetime License';
  const html = `
    <h1>Welcome to VoiceLite ${plan === 'subscription' ? 'Pro' : ''}</h1>
    <p>Your ${planLabel} is now active.</p>
    <p>License key:</p>
    <p style="font-size:24px;font-weight:bold;letter-spacing:4px;">${licenseKey}</p>
    <p>Keep this email for your records. Install the desktop app and sign in to sync your license automatically.</p>
  `;

  if (!resend) {
    console.error('CRITICAL: RESEND_API_KEY not configured - cannot send license emails');
    console.log('[Email stub] License key for %s: %s (%s)', email, licenseKey, planLabel);
    throw new Error('Email service not configured. RESEND_API_KEY is required.');
  }

  const { error } = await resend.emails.send({
    from: FROM_ADDRESS,
    to: [email],
    subject: 'Your VoiceLite license',
    html,
    text: `Your ${planLabel} is active. License key: ${licenseKey}`,
  });

  if (error) {
    console.error('Failed to send license email:', error);
    throw error;
  }
}