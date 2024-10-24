using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public class SmsService
{
    private readonly string _accountSid = "your_account_sid"; // Twilio Account SID
    private readonly string _authToken = "your_auth_token";   // Twilio Auth Token
    private readonly string _twilioPhoneNumber = "your_twilio_phone_number"; // Your Twilio Phone Number

    public SmsService()
    {
        TwilioClient.Init(_accountSid, _authToken);
    }

    public void SendSms(string toPhoneNumber, string messageBody)
    {
        var message = MessageResource.Create(
            body: messageBody,
            from: new PhoneNumber(_twilioPhoneNumber),
            to: new PhoneNumber(toPhoneNumber)
        );

        Console.WriteLine($"Message sent to {toPhoneNumber} with SID: {message.Sid}");
    }
}
