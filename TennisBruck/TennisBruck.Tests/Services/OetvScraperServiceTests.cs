using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using TennisBruck.Services;
using Xunit;

namespace TennisBruck.Tests.Services;

public class OetvScraperServiceTests
{
    [Fact]
    public async Task GetPlayerItnAsync_ValidResponse_ReturnsItn()
    {
        // Arrange
        var jsonResponse = @"{
            ""success"": true,
            ""data"": {
                ""player"": {
                    ""fedRank"": 5.6
                }
            }
        }";

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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<OetvScraperService>>();
        var configMock = new Mock<IConfiguration>();

        Environment.SetEnvironmentVariable("OETV_API_KEY", "testkey");

        var service = new OetvScraperService(httpClient, loggerMock.Object, configMock.Object);

        // Act
        var result = await service.GetPlayerItnAsync("https://www.oetv.at/spieler/NU12345");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5.6m, result.Value);
    }
    
    [Fact]
    public async Task GetPlayerItnAsync_StringItnResponse_ReturnsParsedItn()
    {
        // Arrange
        var jsonResponse = @"{
            ""success"": true,
            ""data"": {
                ""player"": {
                    ""fedRank"": ""7,8""
                }
            }
        }";

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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<OetvScraperService>>();
        var configMock = new Mock<IConfiguration>();

        Environment.SetEnvironmentVariable("OETV_API_KEY", "testkey");

        var service = new OetvScraperService(httpClient, loggerMock.Object, configMock.Object);

        // Act
        var result = await service.GetPlayerItnAsync("https://www.oetv.at/spieler/NU99999");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(7.8m, result.Value);
    }
    
    [Fact]
    public async Task AutomaticallyFindPlayerUrlAsync_ExactMatchFound_ReturnsUrl()
    {
        // Arrange
        var jsonResponse = @"{
            ""success"": true,
            ""data"": {
                ""players"": [
                    {
                        ""firstname"": ""Max"",
                        ""lastname"": ""Mustermann"",
                        ""clubName"": ""ASKÖ Bruck an der Mur"",
                        ""playerId"": ""12345""
                    }
                ]
            }
        }";

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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<OetvScraperService>>();
        var configMock = new Mock<IConfiguration>();

        Environment.SetEnvironmentVariable("OETV_API_KEY", "testkey");

        var service = new OetvScraperService(httpClient, loggerMock.Object, configMock.Object);

        // Act
        var result = await service.AutomaticallyFindPlayerUrlAsync("Max", "Mustermann", "ASKÖ Bruck an der Mur");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://www.oetv.at/spieler/12345", result);
    }
}
