using System.Threading.Tasks;

namespace MqttPhone.Services
{
    public interface ITotpService
    {
        // Extract the TOTP-like digits from the device SMS inbox.
        // Return the cleaned digits (spaces removed) or null/empty if none found.
        Task<string?> ExtractTotpAsync();
    }
}
