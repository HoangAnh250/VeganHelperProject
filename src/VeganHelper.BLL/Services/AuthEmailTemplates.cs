using System.Net;
using VeganHelper.BLL.DTOs;

namespace VeganHelper.BLL.Services;

public static class AuthEmailTemplates
{
    public static EmailMessage VerificationCode(
        string toEmail,
        string verificationCode,
        int lifetimeMinutes,
        bool isResend = false)
    {
        var subject = isResend
            ? "Your new VeganHelper verification code"
            : "Verify your VeganHelper account";
        var title = isResend ? "Your new verification code" : "Verify your email address";
        var intro = isResend
            ? "Here is the new one-time code for your VeganHelper account."
            : "Welcome to VeganHelper. Confirm your email address to finish creating your account.";
        var plainText = $"{title}\n\nYour verification code is: {verificationCode}\n\nThis code expires in {lifetimeMinutes} minutes. If you did not create a VeganHelper account, you can safely ignore this email.";
        var safeCode = Encode(verificationCode);
        var content = $$"""
            <p style="margin:0 0 18px;color:#344054;font-size:16px;line-height:1.6;">Use the one-time code below to continue:</p>
            <div style="margin:0 0 20px;padding:18px 16px;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:12px;text-align:center;">
                <span style="color:#166534;font-size:32px;font-weight:700;letter-spacing:8px;line-height:1.2;">{{safeCode}}</span>
            </div>
            <p style="margin:0;color:#667085;font-size:14px;line-height:1.6;">This code expires in <strong style="color:#344054;">{{lifetimeMinutes}} minutes</strong> and can only be used once.</p>
            """;

        return new EmailMessage(
            toEmail,
            subject,
            plainText,
            Render(
                preheader: "Your VeganHelper email verification code",
                title,
                intro,
                content));
    }

    public static EmailMessage PasswordReset(
        string toEmail,
        string resetLink,
        int lifetimeMinutes)
    {
        const string subject = "Reset your VeganHelper password";
        const string title = "Reset your password";
        const string intro = "We received a request to reset the password for your VeganHelper account.";
        var plainText = $"{title}\n\nClick the link below to reset your password:\n{resetLink}\n\nIt expires in {lifetimeMinutes} minutes. If you did not request a password reset, you can safely ignore this email.";
        var safeLink = Encode(resetLink);
        var content = $$"""
            <p style="margin:0 0 18px;color:#344054;font-size:16px;line-height:1.6;">Click the button below to reset your password:</p>
            <div style="margin:0 0 20px;text-align:center;">
                <a href="{{safeLink}}" style="display:inline-block;padding:12px 24px;background:#1f6f4a;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600;font-size:15px;">Reset Password</a>
            </div>
            <p style="margin:0 0 18px;color:#667085;font-size:14px;line-height:1.6;">Or copy and paste this link into your browser:<br><a href="{{safeLink}}" style="color:#1f6f4a;word-break:break-all;">{{safeLink}}</a></p>
            <p style="margin:0;color:#667085;font-size:14px;line-height:1.6;">This link expires in <strong style="color:#344054;">{{lifetimeMinutes}} minutes</strong> and can only be used once.</p>
            """;

        return new EmailMessage(
            toEmail,
            subject,
            plainText,
            Render(
                preheader: "A password reset was requested for your VeganHelper account",
                title,
                intro,
                content));
    }

    public static EmailMessage SecurityCode(
        string toEmail,
        string verificationCode,
        int lifetimeMinutes,
        string action)
    {
        const string subject = "Confirm a VeganHelper security action";
        const string title = "Confirm your security action";
        var intro = $"Use this one-time code to confirm that you want to {action}.";
        var plainText = $"{title}\n\nYour verification code is: {verificationCode}\n\nThis code expires in {lifetimeMinutes} minutes. If you did not request this action, change your password and contact support.";
        var safeCode = Encode(verificationCode);
        var content = $$"""
            <p style="margin:0 0 18px;color:#344054;font-size:16px;line-height:1.6;">Use the one-time code below to confirm this request:</p>
            <div style="margin:0 0 20px;padding:18px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:12px;text-align:center;">
                <span style="color:#9a3412;font-size:32px;font-weight:700;letter-spacing:8px;line-height:1.2;">{{safeCode}}</span>
            </div>
            <p style="margin:0;color:#667085;font-size:14px;line-height:1.6;">This code expires in <strong style="color:#344054;">{{lifetimeMinutes}} minutes</strong> and can only be used once.</p>
            """;

        return new EmailMessage(
            toEmail,
            subject,
            plainText,
            Render(
                preheader: "Confirm a VeganHelper security action",
                title,
                intro,
                content));
    }

    private static string Render(string preheader, string title, string intro, string content)
    {
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>{{Encode(title)}}</title>
            </head>
            <body style="margin:0;padding:0;background:#f2f4f7;font-family:Arial,Helvetica,sans-serif;color:#101828;">
                <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{{Encode(preheader)}}</div>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width:100%;background:#f2f4f7;">
                    <tr>
                        <td align="center" style="padding:32px 12px;">
                            <table role="presentation" width="600" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:600px;background:#ffffff;border:1px solid #eaecf0;border-radius:16px;overflow:hidden;">
                                <tr>
                                    <td style="padding:24px 32px;background:#1f6f4a;">
                                        <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                                            <tr>
                                                <td style="width:38px;height:38px;background:#ffffff;border-radius:11px;color:#1f6f4a;font-size:18px;font-weight:700;text-align:center;vertical-align:middle;">V</td>
                                                <td style="padding-left:12px;color:#ffffff;font-size:20px;font-weight:700;letter-spacing:.2px;">VeganHelper</td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding:36px 32px 32px;">
                                        <h1 style="margin:0 0 12px;color:#101828;font-size:26px;line-height:1.25;font-weight:700;">{{Encode(title)}}</h1>
                                        <p style="margin:0 0 28px;color:#667085;font-size:16px;line-height:1.6;">{{Encode(intro)}}</p>
                                        {{content}}
                                        <div style="height:1px;margin:30px 0 22px;background:#eaecf0;"></div>
                                        <p style="margin:0;color:#98a2b3;font-size:13px;line-height:1.6;">For your security, never share this code or token with anyone. If you did not request this email, you can safely ignore it.</p>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding:20px 32px;background:#f9fafb;border-top:1px solid #eaecf0;">
                                        <p style="margin:0;color:#98a2b3;font-size:12px;line-height:1.6;text-align:center;">This is an automated message from VeganHelper. Please do not reply to this email.</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
