using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Moq;
using TennisBruck.Features.Championship;
using Xunit;

namespace TennisBruck.Tests.Features;

public class ChampionshipInfoServiceTests
{
    [Fact]
    public async Task SaveInfoAsync_SavesTextAndLoadsCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "TennisBruckTest_" + Guid.NewGuid().ToString("N"));
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.WebRootPath).Returns(tempDir);

        var service = new ChampionshipInfoService(mockEnv.Object);

        await service.SaveInfoAsync("Test Rules Text", null, false, null, false);

        var loaded = await service.GetInfoAsync();

        Assert.Equal("Test Rules Text", loaded.Text);

        // Cleanup
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }
}
