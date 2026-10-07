using System.Text.Json;
using Wayd.Common.Extensions;
using Wayd.Integrations.AzureDevOps.Models;
using Wayd.Integrations.AzureDevOps.Tests.Models;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Models;

public class ClassificationNodeResponseTests : CommonResponseOptions
{
    [Fact]
    public void JsonSerilizer_Deserialize_Succeeds()
    {
        // Arrange
        var json = GetJson();

        // Act
        var actualResponse = JsonSerializer.Deserialize<ClassificationNodeResponse>(json, _options);


        // Assert
        Assert.NotNull(actualResponse);
        actualResponse.Id.Should().Be(45);
        actualResponse.Identifier.Should().Be(Guid.Parse("e060e843-5539-4ec4-becf-f61c5f3c5f85"));
        actualResponse.Name.Should().Be("Wayd");
        //actualResponse.HasChildren.Should().BeTrue();
        actualResponse.Children.Should().NotBeNull();
        actualResponse.Children!.Count.Should().Be(3);
        //actualResponse.Path.Should().Be("\\Wayd\\Area");

        var list = actualResponse.FlattenHierarchy(a => a.Children).ToList();

        list.Count.Should().Be(7);
    }

    //[Theory]
    //[InlineData("\\Wayd\\Area", "Wayd")]
    //[InlineData("\\Wayd\\Area\\Core", "Wayd\\Core")]
    //[InlineData("\\Wayd\\Area\\Core\\Integrations", "Wayd\\Core\\Integrations")]
    //[InlineData("\\Wayd\\Area\\Data", "Wayd\\Data")]
    //[InlineData("\\Wayd\\Area\\Product", "Wayd\\Product")]
    //[InlineData("\\Wayd\\Area\\Product\\Planning", "Wayd\\Product\\Planning")]
    //[InlineData("\\Wayd\\Area\\Product\\Work Management", "Wayd\\Product\\Work Management")]
    //public void WorkItemPath_ReturnsCorrectPath(string path, string expected)
    //{
    //    // Arrange
    //    var node = new ClassificationNodeResponse 
    //    { 
    //        Id = 1,
    //        Identifier = Guid.NewGuid(),
    //        Name = "Test",
    //        Path = path
    //    };

    //    // Act
    //    var actual = node.WorkItemPath;

    //    // Assert
    //    actual.Should().Be(expected);
    //}

    private static string GetJson()
    {
        return """
            {
                "id": 45,
                "identifier": "e060e843-5539-4ec4-becf-f61c5f3c5f85",
                "name": "Wayd",
                "structureType": "area",
                "hasChildren": true,
                "children": [
                    {
                        "id": 121,
                        "identifier": "5e501e7e-9ceb-4fa6-b8bb-5aeda0fa5b39",
                        "name": "Core",
                        "structureType": "area",
                        "hasChildren": true,
                        "children": [
                            {
                                "id": 122,
                                "identifier": "66f4c0e1-6420-47db-9ea0-13f18ea0cb46",
                                "name": "Integrations",
                                "structureType": "area",
                                "hasChildren": false,
                                "path": "\\Wayd\\Area\\Core\\Integrations"
                            }
                        ],
                        "path": "\\Wayd\\Area\\Foundation"
                    },
                    {
                        "id": 123,
                        "identifier": "db7fb143-8886-4246-9a21-7d89bc76ee21",
                        "name": "Data",
                        "structureType": "area",
                        "hasChildren": false,
                        "path": "\\Wayd\\Area\\Data"
                    },
                    {
                        "id": 124,
                        "identifier": "d21f95ee-933f-4529-911a-6c79aefe1487",
                        "name": "Product",
                        "structureType": "area",
                        "hasChildren": true,
                        "children": [
                            {
                                "id": 125,
                                "identifier": "4262f35d-5033-4341-8882-cf19dc1c8506",
                                "name": "Planning",
                                "structureType": "area",
                                "hasChildren": false,
                                "path": "\\Wayd\\Area\\Product\\Planning"
                            },
                            {
                                "id": 126,
                                "identifier": "8e3837dd-a5f4-41f7-aab5-4cb1dd68c867",
                                "name": "Work Management",
                                "structureType": "area",
                                "hasChildren": false,
                                "path": "\\Wayd\\Area\\Product\\Work Management"
                            }
                        ],
                        "path": "\\Wayd\\Area\\Product"
                    },
                ],
                "path": "\\Wayd\\Area"
            }
            """;
    }
}
