namespace Messenger.Services
{
    public interface ISmsService
    {
        Task<string> SendVerificationCodeAsync(string phoneNumber);
    }

    public class SmsService : ISmsService
    {
        public async Task<string> SendVerificationCodeAsync(string phoneNumber)
        {
            var code = new Random().Next(100000, 999999).ToString();

            Console.WriteLine($"SMS to {phoneNumber}: Your verification code is {code}");

            return await Task.FromResult(code);
        }
    }
}