using Jellyfin.Api.Controllers;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public static class ImageControllerTests
{
    [Theory]
    [InlineData("image/apng", ".apng")]
    [InlineData("image/avif", ".avif")]
    [InlineData("image/bmp", ".bmp")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/x-icon", ".ico")]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("image/png; charset=utf-8", ".png")]
    [InlineData("image/svg+xml", ".svg")]
    [InlineData("image/tiff", ".tiff")]
    [InlineData("image/webp", ".webp")]
    public static void TryGetImageExtensionFromContentType_Valid_True(string contentType, string extension)
    {
        Assert.True(ImageController.TryGetImageExtensionFromContentType(contentType, out var ex));
        Assert.Equal(extension, ex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("text/html")]
    public static void TryGetImageExtensionFromContentType_InValid_False(string? contentType)
    {
        Assert.False(ImageController.TryGetImageExtensionFromContentType(contentType, out var ex));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/jpeg; charset=utf-8")]
    [InlineData("image/webp")]
    public static void IsAllowedCustomLogoContentType_Valid_True(string contentType)
    {
        Assert.True(ImageController.IsAllowedCustomLogoContentType(contentType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("text/html")]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    public static void IsAllowedCustomLogoContentType_InValid_False(string? contentType)
    {
        Assert.False(ImageController.IsAllowedCustomLogoContentType(contentType));
    }
}
