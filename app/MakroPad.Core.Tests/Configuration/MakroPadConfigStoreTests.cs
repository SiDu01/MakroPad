using MakroPad.Core.Configuration;

namespace MakroPad.Core.Tests.Configuration;

public sealed class MakroPadConfigStoreTests
{
    [Fact]
    public async Task SaveAsyncReplacesConfigWithoutLeavingTempFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MakroPad.Tests.{Guid.NewGuid():N}");
        var configPath = Path.Combine(directory, "config.json");

        try
        {
            var store = new MakroPadConfigStore(configPath);
            await store.SaveAsync(MakroPadConfig.CreateDefault());
            await store.SaveAsync(MakroPadConfig.CreateDefault() with { ActiveProfile = "Gaming" });

            var loaded = await store.LoadOrCreateDefaultAsync();

            Assert.Equal("Gaming", loaded.ActiveProfile);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
