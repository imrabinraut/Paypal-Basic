using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using server.Models;

namespace server.Services
{
    public class PayPalService
    {
        private readonly PayPalSettings _payPalSettings;
        private readonly HttpClient _httpClient;
        private AuthResponse _authResponse = new AuthResponse();

        public PayPalService(IOptions<PayPalSettings> payPalSettings, HttpClient httpClient)
        {
            _payPalSettings = payPalSettings.Value;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(_payPalSettings.Mode == "Live"
                ? "https://api-m.paypal.com"
                : "https://api-m.sandbox.paypal.com");
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (_authResponse != null && DateTime.UtcNow < _authResponse.Expiration)
                return _authResponse.AccessToken;

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_payPalSettings.ClientId}:{_payPalSettings.ClientSecret}"));
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token")
            {
                Headers = { { "Authorization", $"Basic {credentials}" } },
                Content = content
            };

            var response = await _httpClient.SendAsync(request);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            _authResponse = JsonSerializer.Deserialize<AuthResponse>(jsonResponse);
            _authResponse.Expiration = DateTime.UtcNow.AddSeconds(_authResponse.ExpiresIn);

            return _authResponse.AccessToken;
        }

        public Task<CreateOrderResponse> CreateOrderAsync(string value, string currency, string reference, string returnUrl, string cancelUrl)
        {
            var orderRequest = new CreateOrderRequest
            {
                Intent = "CAPTURE",
                PurchaseUnits = new List<PurchaseUnit>
                {
                    new PurchaseUnit
                    {
                        ReferenceId = reference,
                        Amount = new Amount { CurrencyCode = currency, Value = value }
                    }
                },
                ApplicationContext = new ApplicationContext
                {
                    ReturnUrl = returnUrl,
                    CancelUrl = cancelUrl
                }
            };

            return SendRequestAsync<CreateOrderRequest, CreateOrderResponse>(HttpMethod.Post, "/v2/checkout/orders", orderRequest);
        }

        public async Task<OrderStatus> GetOrderDetailsAsync(string orderId)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.GetAsync($"/v2/checkout/orders/{orderId}");

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<OrderStatus>(jsonResponse);
        }

        public async Task<CaptureOrderResponse> CaptureOrderAsync(string orderId)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync($"/v2/checkout/orders/{orderId}/capture", content);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CaptureOrderResponse>(jsonResponse);
        }

        public async Task<CaptureStatus> GetCaptureStatusAsync(string captureId)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.GetAsync($"/v2/payments/captures/{captureId}");

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var captureResult = JsonSerializer.Deserialize<CaptureResponse>(jsonResponse);

            return new CaptureStatus
            {
                Status = captureResult?.Status,
                Id = captureResult?.Id,
                Amount = captureResult?.Amount?.Value,
                Currency = captureResult?.Amount?.CurrencyCode
            };
        }

        public async Task<RefundStatus> RefundCaptureAsync(string captureId, string refundAmount = null, string currencyCode = "USD")
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Build refund request payload if a specific amount is provided
            var payload = refundAmount != null
                ? JsonSerializer.Serialize(new { amount = new { value = refundAmount, currency_code = currencyCode } })
                : "{}"; // Empty JSON for full refund
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync($"/v2/payments/captures/{captureId}/refund", content);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var refundResult = JsonSerializer.Deserialize<RefundResponse>(jsonResponse);

            return new RefundStatus
            {
                Id = refundResult?.Id,
                Status = refundResult?.Status,
                Amount = refundResult?.Amount?.Value,
                Currency = refundResult?.Amount?.CurrencyCode
            };
        }

        public async Task<RefundStatus> GetRefundStatusAsync(string refundId)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.GetAsync($"/v2/payments/refunds/{refundId}");

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var refundResult = JsonSerializer.Deserialize<RefundResponse>(jsonResponse);

            return new RefundStatus
            {
                Id = refundResult?.Id,
                Status = refundResult?.Status,
                Amount = refundResult?.Amount?.Value,
                Currency = refundResult?.Amount?.CurrencyCode
            };
        }

        public async Task<List<TransactionDetails>> GetTransactionListAsync(DateTime startDate, DateTime endDate, string transactionStatus = null, string transactionId = null
            , string transactionType = null, string transactionAmount = null, string transactionCurrency = null, string paymentInstrumentType = null
            , string storeId = null, string terminalId = null, string fields = "transaction_info", string balanceAffectingRecordsOnly = "y"
            , int pageSize = 100, int page = 1)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"/v1/reporting/transactions?start_date={startDate:yyyy-MM-ddTHH:mm:ssZ}&end_date={endDate:yyyy-MM-ddTHH:mm:ssZ}";

            if (!string.IsNullOrEmpty(transactionStatus))
                url += $"&transaction_status={transactionStatus}";
            if (!string.IsNullOrEmpty(transactionId))
                url += $"&transaction_id={transactionId}";
            if (!string.IsNullOrEmpty(transactionType))
                url += $"&transaction_type={transactionType}";
            if (!string.IsNullOrEmpty(transactionAmount))
                url += $"&transaction_amount={transactionAmount}";
            if (!string.IsNullOrEmpty(transactionCurrency))
                url += $"&transaction_currency={transactionCurrency}";
            if (!string.IsNullOrEmpty(paymentInstrumentType))
                url += $"&payment_instrument_type={paymentInstrumentType}";
            if (!string.IsNullOrEmpty(storeId))
                url += $"&store_id={storeId}";
            if (!string.IsNullOrEmpty(terminalId))
                url += $"&terminal_id={terminalId}";
            if (!string.IsNullOrEmpty(fields))
                url += $"&fields={fields}";
            if (!string.IsNullOrEmpty(balanceAffectingRecordsOnly))
                url += $"&balance_affecting_records_only={balanceAffectingRecordsOnly}";

            url += $"&page_size={pageSize}&page={page}";

            using var response = await _httpClient.GetAsync(url);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var transactionResponse = JsonSerializer.Deserialize<TransactionResponse>(jsonResponse);

            return transactionResponse?.TransactionDetails?.ToList() ?? new List<TransactionDetails>();
        }

        private async Task<TResponse> SendRequestAsync<TRequest, TResponse>(HttpMethod method, string url, TRequest? requestContent = default)
        {
            var accessToken = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var request = new HttpRequestMessage(method, url)
            {
                Content = requestContent is not null ? JsonContent.Create(requestContent) : null
            };

            using var response = await _httpClient.SendAsync(request);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(jsonResponse) ?? throw new JsonException($"Failed to deserialize response for {url}.");
        }
    }

    #region  AuthResponse
    public sealed class AuthResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = null!;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = null!;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        public DateTime Expiration { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = null!;
    }
    #endregion

    #region CreateOrderResponse
    public sealed class CreateOrderResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = null!;

        [JsonPropertyName("purchase_units")]
        public List<PurchaseUnitResponse> PurchaseUnits { get; set; } = new List<PurchaseUnitResponse>();

        [JsonPropertyName("links")]
        public List<Link> Links { get; set; } = new List<Link>();
    }

    public sealed class PurchaseUnitResponse
    {
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = null!;

        [JsonPropertyName("amount")]
        public Amount Amount { get; set; } = new Amount();
    }

    public sealed class CreateOrderRequest
    {
        [JsonPropertyName("intent")]
        public string Intent { get; set; } = null!;

        [JsonPropertyName("purchase_units")]
        public List<PurchaseUnit> PurchaseUnits { get; set; } = new List<PurchaseUnit>();

        [JsonPropertyName("application_context")]
        public ApplicationContext ApplicationContext { get; set; } = new ApplicationContext();
    }

    public sealed class PurchaseUnit
    {
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = null!;

        [JsonPropertyName("amount")]
        public Amount Amount { get; set; } = new Amount();
    }

    public sealed class ApplicationContext
    {
        [JsonPropertyName("return_url")]
        public string ReturnUrl { get; set; } = null!;

        [JsonPropertyName("cancel_url")]
        public string CancelUrl { get; set; } = null!;

        [JsonPropertyName("brand_name")]
        public string BrandName { get; set; } = null!;// Optional: Customize the PayPal checkout experience

        [JsonPropertyName("landing_page")]
        public string LandingPage { get; set; } = null!;// Optional: Options like "LOGIN" or "BILLING"

        [JsonPropertyName("user_action")]
        public string UserAction { get; set; } = null!;// Optional: "CONTINUE" or "PAY_NOW"
    }
    #endregion

    #region CaptureOrderResponse
    public class CaptureOrderResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = null!;

        [JsonPropertyName("purchase_units")]
        public List<PurchaseUnitCapture> PurchaseUnits { get; set; } = new List<PurchaseUnitCapture>();

        [JsonPropertyName("links")]
        public List<Link> Links { get; set; } = new List<Link>();
    }

    public class PurchaseUnitCapture
    {
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = null!;

        [JsonPropertyName("payments")]
        public Payments Payments { get; set; } = new Payments();
    }

    public class Payments
    {
        [JsonPropertyName("captures")]
        public List<Capture> Captures { get; set; } = new List<Capture>();
    }

    public class Capture
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = null!;

        [JsonPropertyName("amount")]
        public Amount Amount { get; set; } = new Amount();

        [JsonPropertyName("final_capture")]
        public bool FinalCapture { get; set; }

        [JsonPropertyName("seller_protection")]
        public SellerProtection SellerProtection { get; set; } = new SellerProtection();

        [JsonPropertyName("create_time")]
        public DateTime CreateTime { get; set; }

        [JsonPropertyName("update_time")]
        public DateTime UpdateTime { get; set; }
    }
    #endregion

    #region ShowCapturedPaymentDetails
    // public class CaptureResponse
    // {
    //     public string Id { get; set; } = null!;
    //     public string Status { get; set; } = null!;
    //     public AmountDetail Amount { get; set; } = new AmountDetail();
    // }

    public class AmountDetail
    {
        public string CurrencyCode { get; set; } = null!;
        public string Value { get; set; } = null!;
    }

    public class CaptureStatus
    {
        public string Status { get; set; } = null!;
        public string Id { get; set; } = null!;
        public string Amount { get; set; } = null!;
        public string Currency { get; set; } = null!;
    }

    public class CaptureResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("amount")]
        public Amount Amount { get; set; }

        [JsonPropertyName("final_capture")]
        public bool FinalCapture { get; set; }

        [JsonPropertyName("seller_protection")]
        public SellerProtection SellerProtection { get; set; }

        [JsonPropertyName("seller_receivable_breakdown")]
        public SellerReceivableBreakdown SellerReceivableBreakdown { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("supplementary_data")]
        public SupplementaryData SupplementaryData { get; set; }

        [JsonPropertyName("payee")]
        public Payee Payee { get; set; }

        [JsonPropertyName("create_time")]
        public DateTime CreateTime { get; set; }

        [JsonPropertyName("update_time")]
        public DateTime UpdateTime { get; set; }

        [JsonPropertyName("links")]
        public List<Link> Links { get; set; }
    }

    public class Amount
    {
        [JsonPropertyName("currency_code")]
        public string CurrencyCode { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    public class SellerProtection
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("dispute_categories")]
        public List<string> DisputeCategories { get; set; }
    }

    public class SellerReceivableBreakdown
    {
        [JsonPropertyName("gross_amount")]
        public Amount GrossAmount { get; set; }

        [JsonPropertyName("paypal_fee")]
        public Amount PaypalFee { get; set; }

        [JsonPropertyName("net_amount")]
        public Amount NetAmount { get; set; }
    }

    public class SupplementaryData
    {
        [JsonPropertyName("related_ids")]
        public RelatedIds RelatedIds { get; set; }
    }

    public class RelatedIds
    {
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; }
    }

    public class Payee
    {
        [JsonPropertyName("email_address")]
        public string EmailAddress { get; set; }

        [JsonPropertyName("merchant_id")]
        public string MerchantId { get; set; }
    }

    public class Link
    {
        [JsonPropertyName("href")]
        public string Href { get; set; }

        [JsonPropertyName("rel")]
        public string Rel { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; }
    }
    #endregion

    #region  RefundCapturedPayment
    public class RefundResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;
        public string Status { get; set; } = null!;
        public AmountDetail Amount { get; set; } = new AmountDetail();
    }

    public class RefundStatus
    {
        public string Id { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string Amount { get; set; } = null!;
        public string Currency { get; set; } = null!;
    }
    #endregion

    #region ListTransactions
    public class TransactionResponse
    {
        public TransactionDetails[] TransactionDetails { get; set; } = new TransactionDetails[0];
        public int Count { get; set; }
        public string NextPage { get; set; } = null!;
    }

    public class TransactionDetails
    {
        public string TransactionId { get; set; } = null!;
        public string TransactionStatus { get; set; } = null!;
        public AmountDetail Amount { get; set; } = new AmountDetail();
        public DateTime TransactionDate { get; set; }
        public string PayerEmail { get; set; } = null!;
        // Add other fields as needed
    }
    #endregion

    #region Show order details
    public class OrderStatus
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("intent")]
        public string Intent { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
        public PaymentSource PaymentSource { get; set; }
        public List<PurchaseUnit> PurchaseUnits { get; set; }
    }

    public class PaymentSource
    {
        public Paypal Paypal { get; set; }
    }

    public class Paypal
    {
        public string EmailAddress { get; set; }
        public string AccountId { get; set; }
        public string AccountStatus { get; set; }
        public Name Name { get; set; }
        public Address Address { get; set; }
    }

    public class Name
    {
        public string GivenName { get; set; }
        public string Surname { get; set; }
    }

    public class Address
    {
        public string CountryCode { get; set; }
    }

    public class Shipping
    {
        public Name Name { get; set; }
        public Address Address { get; set; }
    }
    #endregion
}