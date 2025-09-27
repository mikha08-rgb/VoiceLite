using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class PaymentProcessor
    {
        private readonly HttpClient httpClient;
        private readonly string paddleVendorId;
        private readonly string paddleApiKey;
        private readonly string webhookSecret;

        // For production, these would come from secure configuration
        private const string PADDLE_API_URL = "https://vendors.paddle.com/api/2.0";
        private const string LICENSE_SERVER_URL = "http://localhost:3000"; // Update to production URL when deployed

        public PaymentProcessor()
        {
            httpClient = new HttpClient();

            // These would be loaded from secure storage in production
            paddleVendorId = Environment.GetEnvironmentVariable("PADDLE_VENDOR_ID") ?? "";
            paddleApiKey = Environment.GetEnvironmentVariable("PADDLE_API_KEY") ?? "";
            webhookSecret = Environment.GetEnvironmentVariable("PADDLE_WEBHOOK_SECRET") ?? "";
        }

        // Generate a payment link for purchasing a license
        public async Task<PaymentLink?> GeneratePaymentLink(LicenseType licenseType, string email)
        {
            try
            {
                var productId = GetProductId(licenseType);
                var price = GetPrice(licenseType);

                // For production: Call Paddle API to generate payment link
                // This is a simplified example
                var requestData = new
                {
                    vendor_id = paddleVendorId,
                    vendor_auth_code = paddleApiKey,
                    product_id = productId,
                    prices = new[] { $"USD:{price}" },
                    customer_email = email,
                    passthrough = JsonSerializer.Serialize(new
                    {
                        license_type = licenseType.ToString(),
                        machine_id = GetMachineId()
                    }),
                    return_url = "https://voicelite.com/success",
                    webhook_url = "https://api.voicelite.com/webhook/paddle"
                };

                // In production, this would make actual API call to Paddle
                // var response = await httpClient.PostAsync(
                //     $"{PADDLE_API_URL}/product/generate_pay_link",
                //     new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
                // );

                // For now, return a mock payment link
                return new PaymentLink
                {
                    Url = $"https://checkout.paddle.com/checkout/custom/mock-{Guid.NewGuid()}",
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    LicenseType = licenseType,
                    Price = price
                };
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to generate payment link", ex);
                return null;
            }
        }

        // Verify a purchase and activate the license
        public async Task<LicenseActivationResult> VerifyAndActivateLicense(string paymentId, string email)
        {
            try
            {
                // Call license server to verify payment and get license key
                var request = new
                {
                    payment_id = paymentId,
                    email = email,
                    machine_id = GetMachineId(),
                    hardware_info = GetHardwareInfo()
                };

                // In production, this would call your license server
                // var response = await httpClient.PostAsync(
                //     $"{LICENSE_SERVER_URL}/activate",
                //     new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
                // );

                // For now, return a mock successful activation
                return new LicenseActivationResult
                {
                    Success = true,
                    LicenseKey = GenerateLicenseKey(LicenseType.Personal),
                    LicenseType = LicenseType.Personal,
                    Email = email,
                    ActivatedAt = DateTime.UtcNow,
                    ExpiresAt = null // Lifetime license
                };
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to verify and activate license", ex);
                return new LicenseActivationResult
                {
                    Success = false,
                    ErrorMessage = "Failed to activate license. Please contact support."
                };
            }
        }

        // Handle Paddle webhook for payment confirmation
        public async Task<bool> HandleWebhook(string payload, string signature)
        {
            try
            {
                // Verify webhook signature
                if (!VerifyWebhookSignature(payload, signature))
                {
                    ErrorLogger.LogMessage("Invalid webhook signature");
                    return false;
                }

                // Parse webhook data
                var webhookData = JsonSerializer.Deserialize<JsonElement>(payload);
                var alertName = webhookData.GetProperty("alert_name").GetString();

                switch (alertName)
                {
                    case "payment_succeeded":
                        await HandlePaymentSuccess(webhookData);
                        break;
                    case "payment_refunded":
                        await HandlePaymentRefund(webhookData);
                        break;
                    case "subscription_cancelled":
                        await HandleSubscriptionCancelled(webhookData);
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to handle webhook", ex);
                return false;
            }
        }

        private async Task HandlePaymentSuccess(JsonElement data)
        {
            var email = data.GetProperty("email").GetString();
            var passthrough = JsonSerializer.Deserialize<JsonElement>(
                data.GetProperty("passthrough").GetString() ?? "{}"
            );

            var licenseType = Enum.Parse<LicenseType>(
                passthrough.GetProperty("license_type").GetString() ?? "Personal"
            );

            // Generate and store license in database
            var licenseKey = GenerateLicenseKey(licenseType);

            // In production, save to database and send email
            await SendLicenseEmail(email!, licenseKey, licenseType);
        }

        private async Task HandlePaymentRefund(JsonElement data)
        {
            var email = data.GetProperty("email").GetString();
            // In production, revoke the license in database
            ErrorLogger.LogMessage($"Processing refund for {email}");
        }

        private async Task HandleSubscriptionCancelled(JsonElement data)
        {
            var email = data.GetProperty("email").GetString();
            // In production, mark license for expiration
            ErrorLogger.LogMessage($"Subscription cancelled for {email}");
        }

        private bool VerifyWebhookSignature(string payload, string signature)
        {
            // In production, implement Paddle webhook signature verification
            // using HMAC-SHA256 with the webhook secret
            return true; // Mock verification
        }

        private async Task SendLicenseEmail(string email, string licenseKey, LicenseType licenseType)
        {
            // In production, use email service (SendGrid, AWS SES, etc.)
            ErrorLogger.LogMessage($"Sending license key {licenseKey} to {email}");
            await Task.CompletedTask;
        }

        private string GenerateLicenseKey(LicenseType type)
        {
            // Generate a license key in format: XXXX-XXXX-XXXX-XXXX
            var prefix = type switch
            {
                LicenseType.Personal => "PERS",
                LicenseType.Pro => "PROF",
                LicenseType.Business => "BUSI",
                _ => "TRIAL"
            };

            var random = new Random();
            var suffix = string.Join("-",
                Enumerable.Range(0, 3).Select(_ =>
                    random.Next(1000, 9999).ToString()
                )
            );

            return $"{prefix}-{suffix}";
        }

        private string GetProductId(LicenseType type)
        {
            return type switch
            {
                LicenseType.Personal => "prod_personal_001",
                LicenseType.Pro => "prod_pro_001",
                LicenseType.Business => "prod_business_001",
                _ => "prod_trial_001"
            };
        }

        private decimal GetPrice(LicenseType type)
        {
            return type switch
            {
                LicenseType.Personal => 29.99m,
                LicenseType.Pro => 59.99m,
                LicenseType.Business => 199.99m,
                _ => 0m
            };
        }

        private string GetMachineId()
        {
            // Reuse the machine ID from LicenseManager
            var licenseManager = new LicenseManager();
            return licenseManager.GetCurrentLicense().MachineId;
        }

        private object GetHardwareInfo()
        {
            return new
            {
                os = Environment.OSVersion.ToString(),
                machine_name = Environment.MachineName,
                processor_count = Environment.ProcessorCount,
                is_64bit = Environment.Is64BitOperatingSystem
            };
        }
    }

    public class PaymentLink
    {
        public string Url { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public LicenseType LicenseType { get; set; }
        public decimal Price { get; set; }
    }

    public class LicenseActivationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string LicenseKey { get; set; } = "";
        public LicenseType LicenseType { get; set; }
        public string Email { get; set; } = "";
        public DateTime ActivatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}