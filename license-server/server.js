// Simple VoiceLite License Server
// Deploy this on Railway.app or Heroku for free

const express = require('express');
const sqlite3 = require('sqlite3').verbose();
const crypto = require('crypto');
const cors = require('cors');
const { sendLicenseEmail } = require('./emailService');

const app = express();
const PORT = process.env.PORT || 3000;

// Middleware
app.use(express.json());
app.use(cors());

// Initialize SQLite database
const DATABASE_PATH = process.env.DATABASE_PATH || './licenses.db';
const db = new sqlite3.Database(DATABASE_PATH);

// Create tables if they don't exist
db.run(`
    CREATE TABLE IF NOT EXISTS licenses (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        license_key TEXT UNIQUE NOT NULL,
        email TEXT NOT NULL,
        license_type TEXT NOT NULL,
        machine_id TEXT,
        activation_date TEXT,
        device_count INTEGER DEFAULT 1,
        max_devices INTEGER DEFAULT 1,
        is_active BOOLEAN DEFAULT 1,
        subscription_id TEXT,
        stripe_customer_id TEXT,
        subscription_status TEXT,
        current_period_end TEXT,
        created_at TEXT DEFAULT CURRENT_TIMESTAMP
    )
`);

db.run(`
    CREATE TABLE IF NOT EXISTS activations (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        license_key TEXT NOT NULL,
        machine_id TEXT NOT NULL,
        activated_at TEXT DEFAULT CURRENT_TIMESTAMP,
        last_check TEXT DEFAULT CURRENT_TIMESTAMP,
        UNIQUE(license_key, machine_id)
    )
`);

// API key authentication - MUST be set as environment variables in production
const API_KEY = process.env.API_KEY;
const ADMIN_KEY = process.env.ADMIN_KEY;

if (!API_KEY || !ADMIN_KEY) {
    console.error('CRITICAL: API_KEY and ADMIN_KEY environment variables must be set!');
    console.error('Generate secure keys using: openssl rand -hex 32');
    process.exit(1);
}

// Middleware for API authentication
function requireApiKey(req, res, next) {
    const apiKey = req.headers['x-api-key'];
    if (apiKey !== API_KEY) {
        return res.status(401).json({ error: 'Invalid API key' });
    }
    next();
}

function requireAdminKey(req, res, next) {
    const adminKey = req.headers['x-admin-key'];
    if (adminKey !== ADMIN_KEY) {
        return res.status(401).json({ error: 'Admin access required' });
    }
    next();
}

// Health check endpoint
app.get('/api/check', (req, res) => {
    res.json({
        status: 'ok',
        service: 'VoiceLite License Server',
        version: '1.0.0'
    });
});

// Generate a new license (admin only)
app.post('/api/generate', requireAdminKey, (req, res) => {
    const { email, license_type = 'Personal' } = req.body;

    if (!email) {
        return res.status(400).json({ error: 'Email required' });
    }

    // Generate license key based on type
    const prefix = {
        'Free': 'FREE',
        'Pro': 'PRO',
        'Subscription': 'SUB'
    }[license_type] || 'FREE';

    const licenseKey = `${prefix}-${crypto.randomBytes(4).toString('hex').toUpperCase()}-${crypto.randomBytes(4).toString('hex').toUpperCase()}-${crypto.randomBytes(4).toString('hex').toUpperCase()}`;

    const maxDevices = {
        'Free': 1,
        'Pro': 3,
        'Subscription': 3
    }[license_type] || 1;

    db.run(
        `INSERT INTO licenses (license_key, email, license_type, max_devices) VALUES (?, ?, ?, ?)`,
        [licenseKey, email, license_type, maxDevices],
        function(err) {
            if (err) {
                return res.status(500).json({ error: 'Failed to generate license' });
            }
            res.json({
                license_key: licenseKey,
                email: email,
                license_type: license_type,
                max_devices: maxDevices
            });
        }
    );
});

// Activate a license
app.post('/api/activate', requireApiKey, (req, res) => {
    const { license_key, email, machine_id } = req.body;

    if (!license_key || !email || !machine_id) {
        return res.status(400).json({ error: 'Missing required fields' });
    }

    // Check if license exists and matches email
    db.get(
        `SELECT * FROM licenses WHERE license_key = ? AND email = ? AND is_active = 1`,
        [license_key, email],
        (err, license) => {
            if (err) {
                return res.status(500).json({ error: 'Database error' });
            }
            if (!license) {
                return res.status(404).json({ error: 'Invalid license or email' });
            }

            // Count existing activations
            db.get(
                `SELECT COUNT(*) as count FROM activations WHERE license_key = ?`,
                [license_key],
                (err, result) => {
                    if (err) {
                        return res.status(500).json({ error: 'Database error' });
                    }

                    // Check if already activated on this machine
                    db.get(
                        `SELECT * FROM activations WHERE license_key = ? AND machine_id = ?`,
                        [license_key, machine_id],
                        (err, existing) => {
                            if (existing) {
                                // Already activated, just update last check
                                db.run(
                                    `UPDATE activations SET last_check = CURRENT_TIMESTAMP WHERE license_key = ? AND machine_id = ?`,
                                    [license_key, machine_id]
                                );
                                return res.json({
                                    success: true,
                                    license_type: license.license_type,
                                    message: 'License already activated on this device'
                                });
                            }

                            // Check device limit
                            if (result.count >= license.max_devices) {
                                return res.status(403).json({
                                    error: `Device limit reached (${license.max_devices} devices maximum)`
                                });
                            }

                            // Activate on new device
                            db.run(
                                `INSERT INTO activations (license_key, machine_id) VALUES (?, ?)`,
                                [license_key, machine_id],
                                function(err) {
                                    if (err) {
                                        return res.status(500).json({ error: 'Activation failed' });
                                    }

                                    // Update license activation date if first activation
                                    if (result.count === 0) {
                                        db.run(
                                            `UPDATE licenses SET machine_id = ?, activation_date = CURRENT_TIMESTAMP WHERE license_key = ?`,
                                            [machine_id, license_key]
                                        );
                                    }

                                    res.json({
                                        success: true,
                                        license_type: license.license_type,
                                        devices_used: result.count + 1,
                                        max_devices: license.max_devices
                                    });
                                }
                            );
                        }
                    );
                }
            );
        }
    );
});

// Validate a license
app.post('/api/validate', requireApiKey, (req, res) => {
    const { license_key, machine_id } = req.body;

    if (!license_key || !machine_id) {
        return res.status(400).json({ error: 'Missing required fields' });
    }

    db.get(
        `SELECT l.*, a.activated_at
         FROM licenses l
         JOIN activations a ON l.license_key = a.license_key
         WHERE l.license_key = ? AND a.machine_id = ? AND l.is_active = 1`,
        [license_key, machine_id],
        (err, result) => {
            if (err) {
                return res.status(500).json({ error: 'Database error' });
            }
            if (!result) {
                return res.status(404).json({ valid: false, error: 'Invalid license or not activated on this device' });
            }

            // Update last check time
            db.run(
                `UPDATE activations SET last_check = CURRENT_TIMESTAMP WHERE license_key = ? AND machine_id = ?`,
                [license_key, machine_id]
            );

            res.json({
                valid: true,
                license_type: result.license_type,
                email: result.email,
                activated_at: result.activated_at
            });
        }
    );
});

// Revoke a license (admin only)
app.post('/api/revoke', requireAdminKey, (req, res) => {
    const { license_key } = req.body;

    if (!license_key) {
        return res.status(400).json({ error: 'License key required' });
    }

    db.run(
        `UPDATE licenses SET is_active = 0 WHERE license_key = ?`,
        [license_key],
        function(err) {
            if (err) {
                return res.status(500).json({ error: 'Failed to revoke license' });
            }
            if (this.changes === 0) {
                return res.status(404).json({ error: 'License not found' });
            }
            res.json({ success: true, message: 'License revoked' });
        }
    );
});

// Get license stats (admin only)
app.get('/api/stats', requireAdminKey, (req, res) => {
    db.all(
        `SELECT
            COUNT(DISTINCT l.id) as total_licenses,
            COUNT(DISTINCT CASE WHEN l.is_active = 1 THEN l.id END) as active_licenses,
            COUNT(DISTINCT a.machine_id) as total_devices,
            COUNT(DISTINCT CASE WHEN l.license_type = 'Trial' THEN l.id END) as trial_count,
            COUNT(DISTINCT CASE WHEN l.license_type = 'Personal' THEN l.id END) as personal_count,
            COUNT(DISTINCT CASE WHEN l.license_type = 'Pro' THEN l.id END) as pro_count,
            COUNT(DISTINCT CASE WHEN l.license_type = 'Business' THEN l.id END) as business_count
         FROM licenses l
         LEFT JOIN activations a ON l.license_key = a.license_key`,
        [],
        (err, stats) => {
            if (err) {
                return res.status(500).json({ error: 'Database error' });
            }
            res.json(stats[0]);
        }
    );
});

// Stripe Webhook Handler
app.post('/api/webhook/stripe', express.raw({type: 'application/json'}), async (req, res) => {
    const sig = req.headers['stripe-signature'];
    const webhookSecret = process.env.STRIPE_WEBHOOK_SECRET;

    if (!webhookSecret) {
        console.error('Stripe webhook secret not configured');
        return res.status(500).send('Webhook secret not configured');
    }

    try {
        // For now, just log the webhook (Stripe library would be added in production)
        const event = JSON.parse(req.body.toString());

        console.log('Stripe webhook received:', event.type);

        // Handle different event types
        switch (event.type) {
            case 'checkout.session.completed':
                // Payment successful - generate and email license
                const session = event.data.object;
                const email = session.customer_email || session.customer_details?.email;
                const subscriptionId = session.subscription;
                const customerId = session.customer;
                const licenseType = 'Pro'; // All subscriptions are Pro

                // Generate license
                const prefix = 'SUB'; // Subscription prefix

                const licenseKey = `${prefix}-${crypto.randomBytes(4).toString('hex').toUpperCase()}-${crypto.randomBytes(4).toString('hex').toUpperCase()}-${crypto.randomBytes(4).toString('hex').toUpperCase()}`;

                // Save to database with subscription info
                db.run(
                    `INSERT INTO licenses (license_key, email, license_type, max_devices, subscription_id, stripe_customer_id, subscription_status) VALUES (?, ?, ?, ?, ?, ?, ?)`,
                    [licenseKey, email, licenseType, 3, subscriptionId, customerId, 'active'],
                    async (err) => {
                        if (err) {
                            console.error('Failed to save license:', err);
                        } else {
                            console.log(`License created for ${email}: ${licenseKey}`);
                            // Send email with license key
                            await sendLicenseEmail(email, licenseKey, licenseType);
                        }
                    }
                );
                break;

            case 'customer.subscription.deleted':
                // Handle subscription cancellation
                const cancelledSub = event.data.object;
                db.run(
                    `UPDATE licenses SET subscription_status = 'cancelled', is_active = 0 WHERE subscription_id = ?`,
                    [cancelledSub.id],
                    (err) => {
                        if (err) console.error('Failed to cancel subscription:', err);
                        else console.log('Subscription cancelled:', cancelledSub.id);
                    }
                );
                break;

            case 'customer.subscription.updated':
                // Handle subscription updates (renewal, payment method change, etc)
                const updatedSub = event.data.object;
                db.run(
                    `UPDATE licenses SET subscription_status = ?, current_period_end = ? WHERE subscription_id = ?`,
                    [updatedSub.status, new Date(updatedSub.current_period_end * 1000).toISOString(), updatedSub.id],
                    (err) => {
                        if (err) console.error('Failed to update subscription:', err);
                        else console.log('Subscription updated:', updatedSub.id);
                    }
                );
                break;

            case 'invoice.payment_failed':
                // Handle failed payments
                const failedInvoice = event.data.object;
                if (failedInvoice.subscription) {
                    db.run(
                        `UPDATE licenses SET subscription_status = 'past_due' WHERE subscription_id = ?`,
                        [failedInvoice.subscription],
                        (err) => {
                            if (err) console.error('Failed to update payment status:', err);
                            else console.log('Payment failed for subscription:', failedInvoice.subscription);
                        }
                    );
                }
                break;
        }

        res.json({received: true});
    } catch (err) {
        console.error('Webhook error:', err);
        res.status(400).send(`Webhook Error: ${err.message}`);
    }
});

// Start server
app.listen(PORT, () => {
    console.log(`VoiceLite License Server running on port ${PORT}`);
    console.log(`Health check: http://localhost:${PORT}/api/check`);
    console.log('\nIMPORTANT: Set these environment variables in production:');
    console.log('- API_KEY: For app authentication');
    console.log('- ADMIN_KEY: For admin operations');
    console.log('- STRIPE_WEBHOOK_SECRET: For Stripe webhooks');
    console.log('- PORT: Server port (optional)');
});

// Graceful shutdown
process.on('SIGINT', () => {
    console.log('\nShutting down server...');
    db.close((err) => {
        if (err) {
            console.error(err.message);
        }
        console.log('Database connection closed.');
        process.exit(0);
    });
});