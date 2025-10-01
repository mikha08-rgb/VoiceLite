import crypto from 'crypto';

export function generateToken(bytes = 32) {
  return crypto.randomBytes(bytes).toString('hex');
}

export function hashToken(token: string) {
  return crypto.createHash('sha256').update(token).digest('hex');
}

// Increased from 6 to 8 digits for better security (10^8 = 100M combinations)
// This makes brute-force attacks significantly harder
export function generateOtp(length = 8) {
  const digits = '0123456789';
  let otp = '';
  for (let i = 0; i < length; i += 1) {
    const idx = crypto.randomInt(0, digits.length);
    otp += digits[idx];
  }
  return otp;
}

export function hashOtp(otp: string) {
  return hashToken(otp);
}