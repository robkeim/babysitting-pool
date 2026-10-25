using BabysittingPool.Infrastructure;

namespace BabysittingPool.Api;

internal static class EmailSettings
{
    /// <summary>
    /// <c>Email:Smtp:*</c>, or null unless both the address and the password are set. Host and port are not
    /// secret and default to Gmail with STARTTLS.
    /// </summary>
    public static SmtpSettings? SmtpSettings(this IConfiguration configuration)
    {
        var section = configuration.GetSection("Email:Smtp");
        var address = section["Address"];
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var defaults = new SmtpSettings(address, password);
        return defaults with
        {
            Host = section["Host"] is { Length: > 0 } host ? host : defaults.Host,
            Port = section.GetValue("Port", defaults.Port),
        };
    }
}
