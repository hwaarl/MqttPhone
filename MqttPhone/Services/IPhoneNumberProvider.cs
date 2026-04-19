using System.Threading.Tasks;

namespace MqttPhone.Services
{
    public interface IPhoneNumberProvider
    {
        // Returns the SIM1 phone number formatted in E.164 or null/empty if unavailable
        Task<string?> GetSim1PhoneNumberAsync();
    }
}
