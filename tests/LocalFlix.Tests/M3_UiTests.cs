using LocalFlix.App.ViewModels;
using LocalFlix.Core.Models;
using Xunit;

namespace LocalFlix.Tests;

public class M3_UiTests
{
    [Fact]
    public void TitleCardViewModel_FormatsDisplayPropertiesCorrectly()
    {
        var vm = new TitleCardViewModel
        {
            Id = 1,
            Name = "Inception",
            Year = 2010,
            Rating = 8.8,
            Type = MediaType.Movie,
            IsDriveConnected = true,
            ProgressPercentage = 45.0
        };

        Assert.Equal("2010", vm.DisplayYear);
        Assert.Equal("★ 8.8", vm.DisplayRating);
        Assert.Equal("MOVIE", vm.TypeBadge);
        Assert.True(vm.IsDriveConnected);
        Assert.Equal(45.0, vm.ProgressPercentage);
    }

    [Theory]
    [InlineData(MediaType.Movie, "MOVIE")]
    [InlineData(MediaType.TvShow, "SERIES")]
    [InlineData(MediaType.Documentary, "DOC")]
    [InlineData(MediaType.Other, "VIDEO")]
    public void TitleCardViewModel_DisplaysAppropriateTypeBadges(MediaType type, string expectedBadge)
    {
        var vm = new TitleCardViewModel
        {
            Id = 2,
            Name = "Test Media",
            Type = type
        };

        Assert.Equal(expectedBadge, vm.TypeBadge);
    }
}
