#!/usr/bin/env node

// Simple CLI admin tool for VoiceLite licenses
// Usage: node admin.js [command] [options]

const readline = require('readline');
const https = require('https');
const http = require('http');

// Configuration - update these for your deployment
const SERVER_URL = process.env.LICENSE_SERVER_URL || 'http://localhost:3000';
const ADMIN_KEY = process.env.ADMIN_KEY || 'admin-secret-key-change-this';

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
});

function makeRequest(method, path, data = null) {
    return new Promise((resolve, reject) => {
        const url = new URL(SERVER_URL + path);
        const protocol = url.protocol === 'https:' ? https : http;

        const options = {
            hostname: url.hostname,
            port: url.port,
            path: url.pathname,
            method: method,
            headers: {
                'x-admin-key': ADMIN_KEY,
                'Content-Type': 'application/json'
            }
        };

        const req = protocol.request(options, (res) => {
            let body = '';
            res.on('data', (chunk) => body += chunk);
            res.on('end', () => {
                try {
                    resolve(JSON.parse(body));
                } catch (e) {
                    resolve(body);
                }
            });
        });

        req.on('error', reject);

        if (data) {
            req.write(JSON.stringify(data));
        }
        req.end();
    });
}

async function generateLicense() {
    console.log('\n=== Generate New License ===');

    rl.question('Customer email: ', async (email) => {
        console.log('\nLicense Types:');
        console.log('1. Personal ($29.99 - 1 device)');
        console.log('2. Professional ($59.99 - 3 devices)');
        console.log('3. Business ($199.99 - unlimited devices)');
        console.log('4. Trial (14 days - 1 device)');

        rl.question('\nSelect type (1-4): ', async (choice) => {
            const types = ['Personal', 'Pro', 'Business', 'Trial'];
            const license_type = types[parseInt(choice) - 1] || 'Personal';

            try {
                const result = await makeRequest('POST', '/api/generate', {
                    email: email,
                    license_type: license_type
                });

                if (result.license_key) {
                    console.log('\n✅ License Generated Successfully!');
                    console.log('=====================================');
                    console.log(`License Key: ${result.license_key}`);
                    console.log(`Email: ${result.email}`);
                    console.log(`Type: ${result.license_type}`);
                    console.log(`Max Devices: ${result.max_devices}`);
                    console.log('=====================================');

                    console.log('\n📧 Send this to customer:');
                    console.log('---');
                    console.log(`Thank you for purchasing VoiceLite ${result.license_type}!`);
                    console.log(`\nYour license key: ${result.license_key}`);
                    console.log('\nTo activate:');
                    console.log('1. Open VoiceLite');
                    console.log('2. Click "Enter License"');
                    console.log('3. Enter your email and license key');
                    console.log('---');
                } else {
                    console.error('❌ Error:', result.error || 'Unknown error');
                }
            } catch (error) {
                console.error('❌ Connection error:', error.message);
            }

            showMenu();
        });
    });
}

async function revokeLicense() {
    console.log('\n=== Revoke License ===');

    rl.question('License key to revoke: ', async (license_key) => {
        rl.question('Are you sure? (yes/no): ', async (confirm) => {
            if (confirm.toLowerCase() === 'yes') {
                try {
                    const result = await makeRequest('POST', '/api/revoke', {
                        license_key: license_key
                    });

                    if (result.success) {
                        console.log('✅ License revoked successfully');
                    } else {
                        console.error('❌ Error:', result.error || 'Unknown error');
                    }
                } catch (error) {
                    console.error('❌ Connection error:', error.message);
                }
            } else {
                console.log('Cancelled');
            }
            showMenu();
        });
    });
}

async function viewStats() {
    console.log('\n=== License Statistics ===');

    try {
        const stats = await makeRequest('GET', '/api/stats');

        console.log('\n📊 Overview:');
        console.log(`Total Licenses: ${stats.total_licenses}`);
        console.log(`Active Licenses: ${stats.active_licenses}`);
        console.log(`Total Devices: ${stats.total_devices}`);

        console.log('\n📈 By Type:');
        console.log(`Trial: ${stats.trial_count}`);
        console.log(`Personal: ${stats.personal_count}`);
        console.log(`Professional: ${stats.pro_count}`);
        console.log(`Business: ${stats.business_count}`);

        const revenue = (stats.personal_count * 29.99) +
                       (stats.pro_count * 59.99) +
                       (stats.business_count * 199.99);

        console.log(`\n💰 Estimated Revenue: $${revenue.toFixed(2)}`);
    } catch (error) {
        console.error('❌ Connection error:', error.message);
    }

    showMenu();
}

async function testConnection() {
    console.log(`\nTesting connection to ${SERVER_URL}...`);

    try {
        const result = await makeRequest('GET', '/api/check');
        if (result.status === 'ok') {
            console.log('✅ Connection successful!');
            console.log(`Server: ${result.service} v${result.version}`);
        } else {
            console.error('❌ Unexpected response:', result);
        }
    } catch (error) {
        console.error('❌ Connection failed:', error.message);
        console.log('\nMake sure:');
        console.log('1. Server is running (npm start)');
        console.log('2. LICENSE_SERVER_URL is correct');
        console.log('3. ADMIN_KEY matches server configuration');
    }

    showMenu();
}

function showMenu() {
    console.log('\n=== VoiceLite License Admin ===');
    console.log('1. Generate new license');
    console.log('2. Revoke license');
    console.log('3. View statistics');
    console.log('4. Test connection');
    console.log('5. Exit');

    rl.question('\nSelect option (1-5): ', (choice) => {
        switch(choice) {
            case '1':
                generateLicense();
                break;
            case '2':
                revokeLicense();
                break;
            case '3':
                viewStats();
                break;
            case '4':
                testConnection();
                break;
            case '5':
                console.log('Goodbye!');
                rl.close();
                process.exit(0);
                break;
            default:
                console.log('Invalid option');
                showMenu();
        }
    });
}

// Quick command line arguments
const args = process.argv.slice(2);
if (args.length > 0) {
    const command = args[0];

    if (command === 'generate' && args[1] && args[2]) {
        // Quick generate: node admin.js generate email@example.com Personal
        makeRequest('POST', '/api/generate', {
            email: args[1],
            license_type: args[2] || 'Personal'
        }).then(result => {
            if (result.license_key) {
                console.log(`License: ${result.license_key}`);
            } else {
                console.error('Error:', result.error);
            }
            process.exit(0);
        });
    } else if (command === 'stats') {
        // Quick stats: node admin.js stats
        viewStats().then(() => process.exit(0));
    } else {
        console.log('Usage:');
        console.log('  node admin.js                        - Interactive mode');
        console.log('  node admin.js generate <email> <type> - Quick generate');
        console.log('  node admin.js stats                   - View statistics');
        process.exit(1);
    }
} else {
    // Interactive mode
    console.log('🚀 VoiceLite License Admin Tool');
    console.log(`Server: ${SERVER_URL}`);
    showMenu();
}