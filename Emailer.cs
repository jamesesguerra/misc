using System.Net.Mail;
using eqd.Interfaces;
using eqd.Models;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace eqd.Services;

public class EmailSenderService : IEmailSenderService
{
    private readonly string _smtpServer = "smtp.gmail.com";
    private readonly int _smtpPort = 587;
    private readonly string _smtpUsername = "jamesesguerra025@gmail.com";
    private readonly string _smtpPassword = "";
    
    public async void SendThresholdsEmailAsync(IEnumerable<Threshold> thresholds)
    {
        var toEmail = "jamesesguerra025@gmail.com";
        var subject = "Thresholds Update";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("James Esguerra", _smtpUsername));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;

        var htmlBody = @"
             <html lang='en'>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1'>
                <title>Threshold Update</title>
            </head>
            <body>
                <div>
                    <p style='margin-bottom: 20px;'>Dear User,</p>
                    <p style='margin-bottom: 20px;'>This is to inform you that a threshold has been updated in the DX Dashboard. Please see details below:</p>
       ";

        foreach (var threshold in thresholds)
        {
            htmlBody += BuildThresholdHtml(threshold);
        }
        
        htmlBody += @"
                    <p style='margin: 20px 0;'>You may log into the system and navigate to the <b>Settings Page</b> to review the updated thresholds.</p>
                    <p style='color: red;'><i>This is a system-generated notification. Please do not reply to this email.</i></p>
                </div>
            </body>
            </html>
        ";
        
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody
        };

        message.Body = bodyBuilder.ToMessageBody();
        
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
            await client.SendAsync(message);
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }

    private string BuildThresholdHtml(Threshold threshold)
    {
        var isDetectionSpecialistThreshold = threshold.ThresholdTypeId == 0;
        
        string thresholdType = isDetectionSpecialistThreshold ? "Detection Specialist" : "Group";
        string warningLabel = isDetectionSpecialistThreshold ? "In Warning" : "At Risk Of No Operator Available";
        string fullLabel = isDetectionSpecialistThreshold ? "Full" : "No Operator Available";

        var warningValue = isDetectionSpecialistThreshold ? threshold.InWarningThreshold : threshold.AtRiskThreshold;
        var fullValue = isDetectionSpecialistThreshold ? threshold.FullThreshold : threshold.NoDetectionThreshold;

        string thresholdValues = $"{warningLabel}: {(int)(warningValue * 100)}% | {fullLabel}: {(int)(fullValue * 100)}%";

        return $@"
        <p style='margin: 0px;'><b>Threshold:</b> {thresholdType} - {threshold.GroupId}</p>
        <p style='margin: 0px;'><b>Threshold Value:</b> {thresholdValues}</p>
        <p style='margin: 0px;'><b>Updated By:</b> {threshold.UpdatedBy}</p>
        <p style='margin: 0px;'><b>Updated Date:</b> {threshold.UpdatedDate?.ToString("dd MMM yyyy")}</p>
        <p style='margin: 0px; margin-bottom: 20px;'><b>Approved By:</b> {threshold.ApprovedBy}</p>";
    }


    public async Task SendEmailSampleAsync()
    {
        var toEmail = "jamesesguerra025@gmail.com";
        var subject = "Thresholds Update";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("James Esguerra", _smtpUsername));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;

        var htmlBody = @"
            <html lang='en'>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1'>
                <title>Threshold Update</title>
            </head>
            <body>
                <div>
                    <p style='margin-bottom: 20px;'>Dear User,</p>
                    <p style='margin-bottom: 20px;'>This is to inform you that a threshold has been updated in the DX Dashboard. Please see details below:</p>
                    
                    <p style='margin: 0px;'><b>Threshold:</b> Detection Specialist - Default</p>
                    <p style='margin: 0px;'><b>Threshold:</b> Detection Specialist - Default</p>
                    <p style='margin: 0px;'><b>Threshold:</b> Detection Specialist - Default</p>
                    <p style='margin: 0px;'><b>Threshold:</b> Detection Specialist - Default</p>
                    
                    <p style='margin: 20px 0;'>You may log into the system and navigate to the <b>Settings Page</b> to review the updated thresholds.</p>

                    <p style='color: red;'><i>This is a system-generated notification. Please do not reply to this email</i></p>
                </div>
       ";

        htmlBody += @"
            </body>
            </html>
        ";
        
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody
        };

        message.Body = bodyBuilder.ToMessageBody();
        
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
            await client.SendAsync(message);
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }
}
