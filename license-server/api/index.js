// Vercel Serverless Function for License Server
const sqlite3 = require('sqlite3').verbose();
const crypto = require('crypto');
const path = require('path');

// Database in /tmp for Vercel
const db = new sqlite3.Database('/tmp/licenses.db');

// Initialize database
db.serialize(() => {
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
});

const API_KEY = process.env.API_KEY || 'CE7038B50A2FC2F91C52D042EAADAA77';
const ADMIN_KEY = process.env.ADMIN_KEY || 'F50BB8D40F0262CFFE40D254B789C317';

// Helper to generate license key
function generateLicenseKey(type) {
    const prefix = type === 'Personal' ? 'VLP' : type === 'Professional' ? 'VLR' : 'VLB';
    const random = crypto.randomBytes(12).toString('hex').toUpperCase();
    return `${prefix}-${random.substr(0, 4)}-${random.substr(4, 4)}-${random.substr(8, 4)}-${random.substr(12, 4)}`;
}

module.exports = async (req, res) => {
    // CORS headers
    res.setHeader('Access-Control-Allow-Credentials', true);
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET,OPTIONS,POST');
    res.setHeader('Access-Control-Allow-Headers', 'X-API-Key, X-Admin-Key, Content-Type');

    if (req.method === 'OPTIONS') {
        res.status(200).end();
        return;
    }

    const { url, method, headers, body } = req;
    const apiKey = headers['x-api-key'];
    const adminKey = headers['x-admin-key'];

    // Parse URL path
    const urlPath = url.split('?')[0];

    // Health check
    if (urlPath === '/api/check' && method === 'GET') {
        res.status(200).json({
            status: 'ok',
            timestamp: new Date().toISOString(),
            server: 'VoiceLite License Server v1.0'
        });
        return;
    }

    // API key validation for other endpoints
    if (!apiKey || (apiKey !== API_KEY && adminKey !== ADMIN_KEY)) {
        res.status(401).json({ error: 'Unauthorized' });
        return;
    }

    // Activate license
    if (urlPath === '/api/activate' && method === 'POST') {
        const { license_key, machine_id } = body;

        if (!license_key || !machine_id) {
            res.status(400).json({ error: 'Missing required fields' });
            return;
        }

        return new Promise((resolve) => {
            db.get(
                'SELECT * FROM licenses WHERE license_key = ?',
                [license_key],
                (err, license) => {
                    if (err || !license) {
                        res.status(404).json({ error: 'Invalid license key' });
                        resolve();
                        return;
                    }

                    if (!license.is_active) {
                        res.status(403).json({ error: 'License has been deactivated' });
                        resolve();
                        return;
                    }

                    // Check existing activations
                    db.all(
                        'SELECT DISTINCT machine_id FROM activations WHERE license_key = ?',
                        [license_key],
                        (err, activations) => {
                            const activationCount = activations ? activations.length : 0;
                            const isAlreadyActivated = activations?.some(a => a.machine_id === machine_id);

                            if (!isAlreadyActivated && activationCount >= license.max_devices) {
                                res.status(403).json({
                                    error: 'Maximum activations reached',
                                    max_devices: license.max_devices,
                                    current_devices: activationCount
                                });
                                resolve();
                                return;
                            }

                            // Add or update activation
                            db.run(
                                `INSERT OR REPLACE INTO activations (license_key, machine_id, activated_at, last_check)
                                 VALUES (?, ?, datetime('now'), datetime('now'))`,
                                [license_key, machine_id],
                                (err) => {
                                    if (err) {
                                        res.status(500).json({ error: 'Activation failed' });
                                        resolve();
                                        return;
                                    }

                                    // Update license if first activation
                                    if (!license.machine_id) {
                                        db.run(
                                            'UPDATE licenses SET machine_id = ?, activation_date = datetime("now") WHERE license_key = ?',
                                            [machine_id, license_key]
                                        );
                                    }

                                    res.status(200).json({
                                        success: true,
                                        license_type: license.license_type,
                                        email: license.email,
                                        activations: activationCount + (isAlreadyActivated ? 0 : 1),
                                        max_devices: license.max_devices
                                    });
                                    resolve();
                                }
                            );
                        }
                    );
                }
            );
        });
    }

    // Validate license
    if (urlPath === '/api/validate' && method === 'POST') {
        const { license_key, machine_id } = body;

        if (!license_key || !machine_id) {
            res.status(400).json({ error: 'Missing required fields' });
            return;
        }

        return new Promise((resolve) => {
            db.get(
                `SELECT l.*, a.activated_at FROM licenses l
                 LEFT JOIN activations a ON l.license_key = a.license_key AND a.machine_id = ?
                 WHERE l.license_key = ?`,
                [machine_id, license_key],
                (err, result) => {
                    if (err || !result) {
                        res.status(404).json({ error: 'Invalid license' });
                        resolve();
                        return;
                    }

                    if (!result.is_active) {
                        res.status(403).json({ error: 'License deactivated' });
                        resolve();
                        return;
                    }

                    if (!result.activated_at) {
                        res.status(403).json({ error: 'License not activated for this machine' });
                        resolve();
                        return;
                    }

                    // Update last check
                    db.run(
                        'UPDATE activations SET last_check = datetime("now") WHERE license_key = ? AND machine_id = ?',
                        [license_key, machine_id]
                    );

                    res.status(200).json({
                        valid: true,
                        license_type: result.license_type,
                        email: result.email,
                        activated_at: result.activated_at
                    });
                    resolve();
                }
            );
        });
    }

    // Generate license (admin only)
    if (urlPath === '/api/generate' && method === 'POST') {
        if (adminKey !== ADMIN_KEY) {
            res.status(401).json({ error: 'Admin access required' });
            return;
        }

        const { email, license_type } = body;

        if (!email || !license_type) {
            res.status(400).json({ error: 'Missing required fields' });
            return;
        }

        const licenseKey = generateLicenseKey(license_type);
        const maxDevices = license_type === 'Personal' ? 1 : license_type === 'Professional' ? 3 : 999;

        return new Promise((resolve) => {
            db.run(
                `INSERT INTO licenses (license_key, email, license_type, max_devices)
                 VALUES (?, ?, ?, ?)`,
                [licenseKey, email, license_type, maxDevices],
                (err) => {
                    if (err) {
                        res.status(500).json({ error: 'Failed to generate license' });
                        resolve();
                        return;
                    }

                    res.status(200).json({
                        success: true,
                        license_key: licenseKey,
                        email: email,
                        license_type: license_type,
                        max_devices: maxDevices
                    });
                    resolve();
                }
            );
        });
    }

    // Default response
    res.status(404).json({ error: 'Endpoint not found' });
};