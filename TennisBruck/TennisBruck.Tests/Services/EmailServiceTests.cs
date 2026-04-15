using Moq;
using Moq.Protected;
using System.Net;
using TennisBruck.Services;
using Xunit;

namespace TennisBruck.Tests.Services;

public class EmailServiceTests
{
    [Fact]
    public async Task SendEmailWithResendAsync_SuccessfulRequest_DoesNotThrow()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.Is<CancellationToken>(c => !c.IsCancellationRequested)
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{ \"id\": \"test\" }")
            });

        // Set environment variables required by the EmailService constructor
        Environment.SetEnvironmentVariable("RESEND__APIKEY", "testkey");
        Environment.SetEnvironmentVariable("RESEND_FROM", "test@example.com");

        var httpClient = new HttpClient(handlerMock.Object);
        var emailService = new EmailService(httpClient);

        // Act
        var exception = await Record.ExceptionAsync(() => 
            emailService.SendEmailWithResendAsync("to@example.com", "Test Subject", "123456"));

        // Assert
        Assert.Null(exception); // Ensure it didn't throw any exceptions
        
        // Verify the HTTP POST was actually called to the right URL
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Post && 
                req.RequestUri!.ToString() == "https://api.resend.com/emails"),
            ItExpr.IsAny<CancellationToken>()
        );
    }
    
    [Fact]
    public async Task SendEmailWithResendAsync_UnsuccessfulRequest_ThrowsException()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Bad Request")
            });

        Environment.SetEnvironmentVariable("RESEND__APIKEY", "testkey");
        Environment.SetEnvironmentVariable("RESEND_FROM", "test@example.com");

        var httpClient = new HttpClient(handlerMock.Object);
        var emailService = new EmailService(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            emailService.SendEmailWithResendAsync("to@example.com", "Test Subject", "123456"));
    }
}
