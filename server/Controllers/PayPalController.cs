using Microsoft.AspNetCore.Mvc;
using server.Services;

namespace server.Controllers
{
    [Route("api")]
    [ApiController]
    public class PayPalController : ControllerBase
    {
        private readonly PayPalService _payPalService;

        public PayPalController(PayPalService payPalService)
        {
            _payPalService = payPalService;
        }

        [HttpPost("create-order")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequestDto request)
        {
            var response = await _payPalService.CreateOrderAsync(request.Value, request.Currency, request.Reference, request.ReturnUrl, request.CancelUrl);
            return Ok(response);
        }

        [HttpGet("order-status/{orderId}")]
        public async Task<IActionResult> GetOrderStatus(string orderId)
        {
            var response = await _payPalService.GetOrderDetailsAsync(orderId);
            return Ok(response);
        }

        [HttpPost("capture-order/{orderId}")]
        public async Task<IActionResult> CaptureOrder(string orderId)
        {
            var response = await _payPalService.CaptureOrderAsync(orderId);
            return Ok(response);
        }

        [HttpGet("capture-status/{captureId}")]
        public async Task<IActionResult> GetCaptureStatus(string captureId)
        {
            var response = await _payPalService.GetCaptureStatusAsync(captureId);
            return Ok(response);
        }

        [HttpPost("refund-capture")]
        public async Task<IActionResult> RefundCapture([FromBody] RefundCaptureRequestDto request)
        {
            var response = await _payPalService.RefundCaptureAsync(request.CaptureId, request.RefundAmount, request.CurrencyCode);
            return Ok(response);
        }

        [HttpGet("refund-status/{refundId}")]
        public async Task<IActionResult> GetRefundStatus(string refundId)
        {
            var response = await _payPalService.GetRefundStatusAsync(refundId);
            return Ok(response);
        }

        [HttpGet("transaction-report")]
        public async Task<IActionResult> GetTransactionReport([FromQuery] TransactionReportDto request)
        {
            var response = await _payPalService.GetTransactionListAsync(request.StartDate, request.EndDate, request.TransactionStatus, request.TransactionId, request.TransactionType, request.TransactionAmount, request.TransactionCurrency, request.PaymentInstrumentType, request.StoreId, request.TerminalId, request.Fields, request.BalanceAffectingRecordsOnly, request.PageSize, request.Page);
            return Ok(response);
        }
    }

    #region Order
    public class CreateOrderRequestDto
    {
        public string? Value { get; set; }
        public string? Currency { get; set; }
        public string? Reference { get; set; }
        public string? ReturnUrl { get; set; }
        public string? CancelUrl { get; set; }
    }
    #endregion

    #region Refund
    public class RefundCaptureRequestDto
    {
        public string? CaptureId { get; set; }
        public string? RefundAmount { get; set; }
        public string? CurrencyCode { get; set; }
    }
    #endregion

    #region  Transaction Report
    public class TransactionReportDto
    {
        public string? TransactionStatus { get; set; }
        public string? TransactionId { get; set; }
        public string? TransactionType { get; set; }
        public string? TransactionAmount { get; set; }
        public string? TransactionCurrency { get; set; }
        public string? PaymentInstrumentType { get; set; }
        public string? StoreId { get; set; }
        public string? TerminalId { get; set; }
        public string? Fields { get; set; }
        public string? BalanceAffectingRecordsOnly { get; set; }
        public int PageSize { get; set; } = 100;
        public int Page { get; set; } = 1;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
    #endregion
}