// using Microsoft.AspNetCore.Mvc;
// using Twilio;
// using Twilio.Rest.Verify.V2.Service;
//
// namespace TennisBruck.Services
// {
//     public class SmsService
//     {
//         private readonly string _accountSid = Environment.GetEnvironmentVariable("TwilioAccountSid")!;
//         private readonly string _authToken = Environment.GetEnvironmentVariable("TwilioAuthToken")!;
//         private readonly string _twilioPhoneNumber = Environment.GetEnvironmentVariable("TwilioPhoneNumber")!;
//         private readonly string _verifyServiceSid = Environment.GetEnvironmentVariable("TwilioPathServiceId")!;
//
//         public SmsService()
//         {
//             TwilioClient.Init(_accountSid, _authToken);
//         }
//
//         // Send the verification code
//         public ActionResult SendSms(string toPhoneNumber)
//         {
//             var verification = VerificationResource.Create(
//                 to: toPhoneNumber,
//                 channel: "sms",
//                 pathServiceSid: _verifyServiceSid
//             );
//
//             Console.WriteLine($"Verification SID: {verification.Sid}");
//             return new OkResult();
//         }
//
//         // Verify the code entered by the user
//         public ActionResult VerifyCode(string? toPhoneNumber, string code)
//         {
//             var verificationCheck = VerificationCheckResource.Create(
//                 to: toPhoneNumber,
//                 code: code,
//                 pathServiceSid: _verifyServiceSid
//             );
//
//             if (verificationCheck.Status == "approved")
//             {
//                 Console.WriteLine("Verification successful!");
//                 return new OkResult(); // Return success response
//             }
//             else
//             {
//                 Console.WriteLine("Verification failed.");
//                 return new BadRequestObjectResult("Invalid verification code."); // Return error response
//             }
//         }
//     }
// }