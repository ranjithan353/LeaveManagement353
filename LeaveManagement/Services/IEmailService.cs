using SendGrid;
using SendGrid.Helpers.Mail;

namespace LeaveManagement.Services
{
    public interface IEmailService
    {
        Task SendLeaveStatusNotificationAsync(string recipientEmail, string employeeName, string leaveType, DateTime startDate, DateTime endDate, string status, string reason = "");
    }

    public class SendGridEmailService : IEmailService
    {
        private readonly SendGridClient _sendGridClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SendGridEmailService> _logger;

        public SendGridEmailService(IConfiguration configuration, ILogger<SendGridEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            var apiKey = configuration["SendGrid:ApiKey"] ?? "dummy-key-for-dev";
            _sendGridClient = new SendGridClient(apiKey);
        }

        public async Task SendLeaveStatusNotificationAsync(string recipientEmail, string employeeName, string leaveType, DateTime startDate, DateTime endDate, string status, string reason = "")
        {
            try
            {
                var fromEmail = _configuration["SendGrid:FromEmail"];
                var fromName = _configuration["SendGrid:FromName"];

                var subject = $"Leave Request {status} - {leaveType}";
                var htmlContent = GenerateEmailBody(employeeName, leaveType, startDate, endDate, status, reason);

                var msg = new SendGridMessage()
                {
                    From = new EmailAddress(fromEmail, fromName),
                    Subject = subject,
                    HtmlContent = htmlContent
                };

                msg.AddTo(new EmailAddress(recipientEmail, employeeName));

                var response = await _sendGridClient.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Email notification sent to {recipientEmail} for leave request {status}");
                }
                else
                {
                    _logger.LogError($"Failed to send email notification to {recipientEmail}: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending email notification: {ex.Message}");
                // Don't throw - logging failure is sufficient
            }
        }

        private string GenerateEmailBody(string employeeName, string leaveType, DateTime startDate, DateTime endDate, string status, string reason)
        {
            var statusColor = status == "Approved" ? "green" : status == "Rejected" ? "red" : "blue";
            var reasonSection = string.IsNullOrEmpty(reason) ? "" : $"<p><strong>Reason:</strong> {reason}</p>";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 20px; margin-top: 20px; }}
        .status {{ color: {statusColor}; font-weight: bold; font-size: 18px; }}
        .details {{ margin-top: 15px; }}
        .details p {{ margin: 8px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h2>Leave Request Notification</h2>
        </div>
        <div class=""content"">
            <p>Dear {employeeName},</p>
            <p>Your leave request has been <span class=""status"">{status}</span>.</p>
            
            <div class=""details"">
                <p><strong>Leave Type:</strong> {leaveType}</p>
                <p><strong>Start Date:</strong> {startDate:yyyy-MM-dd}</p>
                <p><strong>End Date:</strong> {endDate:yyyy-MM-dd}</p>
                <p><strong>Status:</strong> {status}</p>
                {reasonSection}
            </div>

            <p style=""margin-top: 20px;"">If you have any questions, please contact your manager or HR department.</p>

            <p>Best regards,<br/>Leave Management System</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}
