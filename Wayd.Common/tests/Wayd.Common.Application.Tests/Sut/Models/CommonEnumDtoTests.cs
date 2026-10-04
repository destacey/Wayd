using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Wayd.Common.Application.Models;

namespace Wayd.Common.Application.Tests.Sut.Models;

public sealed class CommonEnumDtoTests
{
    [Fact]
    public void GetValues_ReturnsOneDtoPerEnumValue()
    {
        // Arrange & Act
        var values = CommonEnumDto<CommonEnumDtoTestsEnum>.GetValues<CommonEnumDtoTestsDto>();

        // Assert
        values.Select(v => v.Code).Should().Equal(
            CommonEnumDtoTestsEnum.Described, CommonEnumDtoTestsEnum.Plain);
    }

    [Fact]
    public void GetValues_CarriesTheCodeBesideTheNumberAndDisplayMetadata()
    {
        // Arrange & Act
        var described = CommonEnumDto<CommonEnumDtoTestsEnum>.GetValues<CommonEnumDtoTestsDto>()
            .Single(v => v.Code == CommonEnumDtoTestsEnum.Described);

        // Assert
        described.Id.Should().Be(3);
        described.Name.Should().Be("Has A Name");
        described.Description.Should().Be("Has a description");
        described.Order.Should().Be(2);
    }

    [Fact]
    public void GetValues_FallsBackToTheMemberNameWithoutDisplayMetadata()
    {
        // Arrange & Act
        var plain = CommonEnumDto<CommonEnumDtoTestsEnum>.GetValues<CommonEnumDtoTestsDto>()
            .Single(v => v.Code == CommonEnumDtoTestsEnum.Plain);

        // Assert
        plain.Id.Should().Be(7);
        plain.Name.Should().Be("Plain");
        plain.Description.Should().BeNull();
    }
}

internal enum CommonEnumDtoTestsEnum
{
    [Display(Name = "Has A Name", Description = "Has a description", Order = 2)]
    Described = 3,

    Plain = 7,
}

internal sealed record CommonEnumDtoTestsDto : CommonEnumDto<CommonEnumDtoTestsEnum> { }
