using System.Text.Json;
using Wayd.Integrations.AzureDevOps.Models.Processes;
using Wayd.Integrations.AzureDevOps.Tests.Models;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Models.Processes;

public class ProcessWorkItemTypeDtoExtensionsTests : CommonResponseOptions
{
    [Fact]
    public void ToAzdoWorkType_WithBehavior_Succeeds()
    {
        // Arrange
        var json = GetToAzdoWorkTypeJson();
        var workItemType = JsonSerializer.Deserialize<ProcessWorkItemTypeDto>(json, _options);

        // Act
        var workType = workItemType!.ToAzdoWorkType();

        // Assert
        Assert.NotNull(workType);
        workType.Name.Should().Be("Epic");
        workType.Description.Should().Be("Epics help teams effectively manage and groom their product backlog");
        workType.WorkTypeLevelId.Should().Be("Microsoft.VSTS.Agile.EpicBacklogBehavior");
        workType.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ToAzdoWorkType_WithoutBehavior_Succeeds()
    {
        // Arrange
        var json = GetToAzdoWorkTypeJsonWithoutBehavior();
        var workItemType = JsonSerializer.Deserialize<ProcessWorkItemTypeDto>(json, _options);

        // Act
        var workType = workItemType!.ToAzdoWorkType();

        // Assert
        Assert.NotNull(workType);
        workType.Name.Should().Be("Bug");
        workType.Description.Should().Be("Describes a divergence between required and actual behavior, and tracks the work done to correct the defect and verify the correction.");
        workType.WorkTypeLevelId.Should().Be("System.RequirementBacklogBehavior");
        workType.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ToAzdoWorkTypes_Succeeds()
    {
        // Arrange
        var json = GetToAzdoWorkTypesJson();
        var workItemTypes = JsonSerializer.Deserialize<List<ProcessWorkItemTypeDto>>(json, _options);

        // Act
        var workTypes = workItemTypes!.ToIExternalWorkTypes();

        // Assert
        Assert.NotNull(workTypes);
        workTypes.Count.Should().Be(6);
    }



    [Fact]
    public void ToAzdoWorkType_KeysTheTypeByReferenceName_AndEachStateById()
    {
        // Arrange
        var json = GetToAzdoWorkTypeJson();
        var workItemType = JsonSerializer.Deserialize<ProcessWorkItemTypeDto>(json, _options);

        // Act
        var workType = workItemType!.ToAzdoWorkType();

        // Assert
        // Both survive a rename in Azure DevOps, which the names do not.
        workType.ExternalKey.Should().Be("TestAgileProcess.Epic");
        workType.WorkflowStates.Take(3).Select(s => (s.ExternalKey, s.StatusName)).Should().Equal(
            ("7b7e3e8c-e500-40b6-ad56-d59b8d64d757", "New"),
            ("277237cd-0bc0-4ffb-bdc6-d358b154ba9e", "Active"),
            ("f36cfea7-889a-448e-b5d1-fbc9b134ec82", "Resolved"));
    }

    [Fact]
    public void ToAzdoWorkType_WithAHiddenInheritedState_TakesTheCopyThatHidesIt()
    {
        // Arrange
        // As Azure DevOps lists a hidden inherited state: the system copy, then the inherited copy that hides
        // it, under one id (see GetJson in ProcessWorkItemTypeDtoTests).
        var id = Guid.Parse("f36cfea7-889a-448e-b5d1-fbc9b134ec82");
        var workItemType = new ProcessWorkItemTypeDto
        {
            ReferenceName = "TestAgileProcess.Epic",
            Name = "Epic",
            States =
            [
                new() { Id = Guid.Parse("7b7e3e8c-e500-40b6-ad56-d59b8d64d757"), Name = "New", StateCategory = "Proposed", Order = 1 },
                new() { Id = id, Name = "Resolved", StateCategory = "InProgress", Order = 3 },
                new() { Id = id, Name = "resolved", StateCategory = "Resolved", Order = 6, Hidden = true },
            ],
        };

        // Act
        var workType = workItemType.ToAzdoWorkType();

        // Assert
        // One state, in its first place and under its first spelling, hidden as the last copy says.
        workType.WorkflowStates.Select(s => (s.ExternalKey, s.StatusName, s.Order, s.IsActive)).Should().Equal(
            ("7b7e3e8c-e500-40b6-ad56-d59b8d64d757", "New", 1, true),
            ("f36cfea7-889a-448e-b5d1-fbc9b134ec82", "Resolved", 6, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToAzdoWorkType_WithCopiesUnderDifferentIds_KeysThemByTheLowestId(bool lowestFirst)
    {
        // Arrange
        var low = new ProcessWorkItemStateDto { Id = Guid.Parse("11111111-0000-0000-0000-000000000000"), Name = "Closed", StateCategory = "Completed", Order = 3 };
        var high = new ProcessWorkItemStateDto { Id = Guid.Parse("99999999-0000-0000-0000-000000000000"), Name = "Closed", StateCategory = "Completed", Order = 4 };
        var workItemType = new ProcessWorkItemTypeDto
        {
            ReferenceName = "Custom.Bug",
            Name = "Bug",
            States = lowestFirst ? [low, high] : [high, low],
        };

        // Act
        var workType = workItemType.ToAzdoWorkType();

        // Assert
        workType.WorkflowStates.Should().ContainSingle().Which.ExternalKey.Should().Be("11111111-0000-0000-0000-000000000000");
    }

    private static string GetToAzdoWorkTypeJson()
    {
        return """
           {
           	"referenceName": "TestAgileProcess.Epic",
           	"name": "Epic",
           	"description": "Epics help teams effectively manage and groom their product backlog",
           	"url": "https://dev.azure.com/test/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic",
           	"customization": "inherited",
           	"color": "E06C00",
           	"icon": "icon_crown",
           	"isDisabled": false,
           	"inherits": "Microsoft.VSTS.WorkItemTypes.Epic",
           	"states": [
           		{
           			"id": "7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
           			"name": "New",
           			"color": "b2b2b2",
           			"stateCategory": "Proposed",
           			"order": 1,
           			"url": "https://dev.azure.com/test/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
           			"customizationType": "system"
           		},
           		{
           			"id": "277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
           			"name": "Active",
           			"color": "007acc",
           			"stateCategory": "InProgress",
           			"order": 2,
           			"url": "https://dev.azure.com/test/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
           			"customizationType": "system"
           		},
           		{
           			"id": "f36cfea7-889a-448e-b5d1-fbc9b134ec82",
           			"name": "Resolved",
           			"color": "007acc",
           			"stateCategory": "InProgress",
           			"order": 3,
           			"url": "https://dev.azure.com/test/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/f36cfea7-889a-448e-b5d1-fbc9b134ec82",
           			"customizationType": "system"
           		},
           		{
           			"id": "9f479b88-4542-4f9d-8048-5d9c953b5082",
           			"name": "Closed",
           			"color": "339933",
           			"stateCategory": "Completed",
           			"order": 4,
           			"url": "https://dev.azure.com/test/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/9f479b88-4542-4f9d-8048-5d9c953b5082",
           			"customizationType": "system"
           		},
           		{
           			"id": "0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
           			"name": "Removed",
           			"color": "ffffff",
           			"stateCategory": "Removed",
           			"order": 5,
           			"url": "https://dev.azure.com/test/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
           			"customizationType": "system"
           		},
           		{
           			"id": "f36cfea7-889a-448e-b5d1-fbc9b134ec82",
           			"name": "Resolved",
           			"color": "007acc",
           			"stateCategory": "InProgress",
           			"order": 6,
           			"url": "https://dev.azure.com/test/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/f36cfea7-889a-448e-b5d1-fbc9b134ec82",
           			"hidden": true,
           			"customizationType": "inherited"
           		}
           	],
           	"behaviors": [
           		{
           			"behavior": {
           				"id": "Microsoft.VSTS.Agile.EpicBacklogBehavior",
           				"url": "https://dev.azure.com/test/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/Microsoft.VSTS.Agile.EpicBacklogBehavior"
           			},
           			"isDefault": true,
           			"isLegacyDefault": true,
           			"url": "https://dev.azure.com/test/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/Microsoft.VSTS.Agile.EpicBacklogBehavior"
           		}
           	]
           }
           """;
    }

    private static string GetToAzdoWorkTypeJsonWithoutBehavior()
    {
        return """
            {
                "referenceName": "Microsoft.VSTS.WorkItemTypes.Bug",
                "name": "Bug",
                "description": "Describes a divergence between required and actual behavior, and tracks the work done to correct the defect and verify the correction.",
                "customization": "system",
                "color": "CC293D",
                "icon": "icon_insect",
                "isDisabled": false,
                "inherits": null,
                "states": [
                    {
                        "id": "7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                        "name": "New",
                        "color": "b2b2b2",
                        "stateCategory": "Proposed",
                        "order": 1,
                        "customizationType": "system"
                    },
                    {
                        "id": "277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                        "name": "Active",
                        "color": "007acc",
                        "stateCategory": "InProgress",
                        "order": 2,
                        "customizationType": "system"
                    },
                    {
                        "id": "f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                        "name": "Resolved",
                        "color": "ff9d00",
                        "stateCategory": "Resolved",
                        "order": 3,
                        "customizationType": "system"
                    },
                    {
                        "id": "9f479b88-4542-4f9d-8048-5d9c953b5082",
                        "name": "Closed",
                        "color": "339933",
                        "stateCategory": "Completed",
                        "order": 4,
                        "customizationType": "system"
                    }
                ],
                "behaviors": []
            }
            """;
    }

    private static string GetToAzdoWorkTypesJson()
    {
        return """
            [
                {
                    "referenceName": "TestAgileProcess.Epic",
                    "name": "Epic",
                    "description": "Epics help teams effectively manage and groom their product backlog",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic",
                    "customization": "inherited",
                    "color": "E06C00",
                    "icon": "icon_crown",
                    "isDisabled": false,
                    "inherits": "Microsoft.VSTS.WorkItemTypes.Epic",
                    "states": [
                        {
                            "id": "7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "name": "New",
                            "color": "b2b2b2",
                            "stateCategory": "Proposed",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "customizationType": "system"
                        },
                        {
                            "id": "277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "name": "Active",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "customizationType": "system"
                        },
                        {
                            "id": "f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "name": "Resolved",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 3,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "customizationType": "system"
                        },
                        {
                            "id": "9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "name": "Closed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 4,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "customizationType": "system"
                        },
                        {
                            "id": "0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
                            "name": "Removed",
                            "color": "ffffff",
                            "stateCategory": "Removed",
                            "order": 5,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
                            "customizationType": "system"
                        },
                        {
                            "id": "f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "name": "Resolved",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 6,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Epic/states/f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "hidden": true,
                            "customizationType": "inherited"
                        }
                    ],
                    "behaviors": [
                        {
                            "behavior": {
                                "id": "Microsoft.VSTS.Agile.EpicBacklogBehavior",
                                "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/Microsoft.VSTS.Agile.EpicBacklogBehavior"
                            },
                            "isDefault": true,
                            "isLegacyDefault": true,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/Microsoft.VSTS.Agile.EpicBacklogBehavior"
                        }
                    ]
                },
                {
                    "referenceName": "TestAgileProcess.Spike",
                    "name": "Spike",
                    "description": "Used to track research or investigations.",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Spike",
                    "customization": "custom",
                    "color": "aaaaaa",
                    "icon": "icon_review",
                    "isDisabled": false,
                    "inherits": null,
                    "states": [
                        {
                            "id": "56ba8e2f-e2a6-4839-83bb-187ec295fb0f",
                            "name": "New",
                            "color": "D5D5D5",
                            "stateCategory": "Proposed",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Spike/states/56ba8e2f-e2a6-4839-83bb-187ec295fb0f",
                            "customizationType": "custom"
                        },
                        {
                            "id": "71e5a51a-0339-4c13-b289-6a44d5b65fb6",
                            "name": "Active",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Spike/states/71e5a51a-0339-4c13-b289-6a44d5b65fb6",
                            "customizationType": "custom"
                        },
                        {
                            "id": "8e312e6e-468c-46a3-b7f3-d55d58428291",
                            "name": "Closed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 3,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Spike/states/8e312e6e-468c-46a3-b7f3-d55d58428291",
                            "customizationType": "custom"
                        }
                    ],
                    "behaviors": [
                        {
                            "behavior": {
                                "id": "System.RequirementBacklogBehavior",
                                "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/System.RequirementBacklogBehavior"
                            },
                            "isDefault": false,
                            "isLegacyDefault": false,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/System.RequirementBacklogBehavior"
                        }
                    ]
                },
                {
                    "referenceName": "TestAgileProcess.Initiative",
                    "name": "Initiative",
                    "description": "",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Initiative",
                    "customization": "custom",
                    "color": "009CCC",
                    "icon": "icon_chart",
                    "isDisabled": false,
                    "inherits": null,
                    "states": [
                        {
                            "id": "62743a40-e727-4db4-8485-b9e0ebf856a1",
                            "name": "New",
                            "color": "b2b2b2",
                            "stateCategory": "Proposed",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Initiative/states/62743a40-e727-4db4-8485-b9e0ebf856a1",
                            "customizationType": "custom"
                        },
                        {
                            "id": "d6e7b40d-6d46-4632-a93e-79ad59264555",
                            "name": "Active",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Initiative/states/d6e7b40d-6d46-4632-a93e-79ad59264555",
                            "customizationType": "custom"
                        },
                        {
                            "id": "9887fdc4-819e-474e-aad0-eea428d2ab43",
                            "name": "Closed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 3,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Initiative/states/9887fdc4-819e-474e-aad0-eea428d2ab43",
                            "customizationType": "custom"
                        },
                        {
                            "id": "28db9425-a6bc-4932-b93d-6a789fb233b3",
                            "name": "Removed",
                            "color": "f06673",
                            "stateCategory": "Removed",
                            "order": 4,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Initiative/states/28db9425-a6bc-4932-b93d-6a789fb233b3",
                            "customizationType": "custom"
                        }
                    ],
                    "behaviors": [
                        {
                            "behavior": {
                                "id": "Custom.1bdcd692-19c0-4b8f-9439-356410b60583",
                                "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/Custom.1bdcd692-19c0-4b8f-9439-356410b60583"
                            },
                            "isDefault": true,
                            "isLegacyDefault": false,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/Custom.1bdcd692-19c0-4b8f-9439-356410b60583"
                        }
                    ]
                },
                {
                    "referenceName": "TestAgileProcess.Feature",
                    "name": "Feature",
                    "description": "Tracks a feature that will be released with the product",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Feature",
                    "customization": "inherited",
                    "color": "773B93",
                    "icon": "icon_trophy",
                    "isDisabled": false,
                    "inherits": "Microsoft.VSTS.WorkItemTypes.Feature",
                    "states": [
                        {
                            "id": "7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "name": "New",
                            "color": "b2b2b2",
                            "stateCategory": "Proposed",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Feature/states/7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "customizationType": "system"
                        },
                        {
                            "id": "277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "name": "Active",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Feature/states/277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "customizationType": "system"
                        },
                        {
                            "id": "f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "name": "Resolved",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 3,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Feature/states/f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "customizationType": "system"
                        },
                        {
                            "id": "9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "name": "Closed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 4,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Feature/states/9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "customizationType": "system"
                        },
                        {
                            "id": "0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
                            "name": "Removed",
                            "color": "ffffff",
                            "stateCategory": "Removed",
                            "order": 5,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Feature/states/0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
                            "customizationType": "system"
                        },
                        {
                            "id": "f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "name": "Resolved",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 6,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Feature/states/f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "hidden": true,
                            "customizationType": "inherited"
                        }
                    ],
                    "behaviors": [
                        {
                            "behavior": {
                                "id": "Microsoft.VSTS.Agile.FeatureBacklogBehavior",
                                "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/Microsoft.VSTS.Agile.FeatureBacklogBehavior"
                            },
                            "isDefault": true,
                            "isLegacyDefault": true,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/Microsoft.VSTS.Agile.FeatureBacklogBehavior"
                        }
                    ]
                },
                {
                    "referenceName": "TestAgileProcess.Issue",
                    "name": "Issue",
                    "description": "Tracks an obstacle to progress.",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Issue",
                    "customization": "inherited",
                    "color": "B4009E",
                    "icon": "icon_traffic_cone",
                    "isDisabled": false,
                    "inherits": "Microsoft.VSTS.WorkItemTypes.Issue",
                    "states": [
                        {
                            "id": "277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "name": "Active",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Issue/states/277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "customizationType": "system"
                        },
                        {
                            "id": "9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "name": "Closed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/TestAgileProcess.Issue/states/9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "customizationType": "system"
                        }
                    ],
                    "behaviors": [
                        {
                            "behavior": {
                                "id": "System.TaskBacklogBehavior",
                                "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/System.TaskBacklogBehavior"
                            },
                            "isDefault": false,
                            "isLegacyDefault": false,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/System.TaskBacklogBehavior"
                        }
                    ]
                },
                {
                    "referenceName": "Microsoft.VSTS.WorkItemTypes.Bug",
                    "name": "Bug",
                    "description": "Describes a divergence between required and actual behavior, and tracks the work done to correct the defect and verify the correction.",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Bug",
                    "customization": "system",
                    "color": "CC293D",
                    "icon": "icon_insect",
                    "isDisabled": false,
                    "inherits": null,
                    "states": [
                        {
                            "id": "7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "name": "New",
                            "color": "b2b2b2",
                            "stateCategory": "Proposed",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Bug/states/7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "customizationType": "system"
                        },
                        {
                            "id": "277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "name": "Active",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Bug/states/277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "customizationType": "system"
                        },
                        {
                            "id": "f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "name": "Resolved",
                            "color": "ff9d00",
                            "stateCategory": "Resolved",
                            "order": 3,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Bug/states/f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "customizationType": "system"
                        },
                        {
                            "id": "9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "name": "Closed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 4,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Bug/states/9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "customizationType": "system"
                        }
                    ],
                    "behaviors": []
                },
                {
                    "referenceName": "Microsoft.VSTS.WorkItemTypes.Task",
                    "name": "Task",
                    "description": "Tracks work that needs to be done.",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Task",
                    "customization": "system",
                    "color": "A4880A",
                    "icon": "icon_clipboard",
                    "isDisabled": false,
                    "inherits": null,
                    "states": [
                        {
                            "id": "7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "name": "New",
                            "color": "b2b2b2",
                            "stateCategory": "Proposed",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Task/states/7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "customizationType": "system"
                        },
                        {
                            "id": "277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "name": "Active",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Task/states/277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "customizationType": "system"
                        },
                        {
                            "id": "9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "name": "Closed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 3,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Task/states/9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "customizationType": "system"
                        },
                        {
                            "id": "0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
                            "name": "Removed",
                            "color": "ffffff",
                            "stateCategory": "Removed",
                            "order": 4,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.Task/states/0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
                            "customizationType": "system"
                        }
                    ],
                    "behaviors": [
                        {
                            "behavior": {
                                "id": "System.TaskBacklogBehavior",
                                "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/System.TaskBacklogBehavior"
                            },
                            "isDefault": true,
                            "isLegacyDefault": true,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/System.TaskBacklogBehavior"
                        }
                    ]
                },
                {
                    "referenceName": "Microsoft.VSTS.WorkItemTypes.TestCase",
                    "name": "Test Case",
                    "description": "Server-side data for a set of steps to be tested.",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestCase",
                    "customization": "system",
                    "color": "004B50",
                    "icon": "icon_test_case",
                    "isDisabled": false,
                    "inherits": null,
                    "states": [
                        {
                            "id": "067742a4-07cb-4a53-b2fa-2ea058f3332b",
                            "name": "Design",
                            "color": "b2b2b2",
                            "stateCategory": "Proposed",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestCase/states/067742a4-07cb-4a53-b2fa-2ea058f3332b",
                            "customizationType": "system"
                        },
                        {
                            "id": "17cfeadc-f49b-41b9-897a-43655d35c0c4",
                            "name": "Ready",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestCase/states/17cfeadc-f49b-41b9-897a-43655d35c0c4",
                            "customizationType": "system"
                        },
                        {
                            "id": "9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "name": "Closed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 3,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestCase/states/9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "customizationType": "system"
                        }
                    ],
                    "behaviors": []
                },
                {
                    "referenceName": "Microsoft.VSTS.WorkItemTypes.TestPlan",
                    "name": "Test Plan",
                    "description": "Tracks test activities for a specific milestone or release.",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestPlan",
                    "customization": "system",
                    "color": "004B50",
                    "icon": "icon_test_plan",
                    "isDisabled": false,
                    "inherits": null,
                    "states": [
                        {
                            "id": "277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "name": "Active",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestPlan/states/277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "customizationType": "system"
                        },
                        {
                            "id": "da8f3a6e-e52d-4d1d-9890-783a1bca5da1",
                            "name": "Inactive",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestPlan/states/da8f3a6e-e52d-4d1d-9890-783a1bca5da1",
                            "customizationType": "system"
                        }
                    ],
                    "behaviors": []
                },
                {
                    "referenceName": "Microsoft.VSTS.WorkItemTypes.TestSuite",
                    "name": "Test Suite",
                    "description": "Tracks test activites for a specific feature, requirement, or user story.",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestSuite",
                    "customization": "system",
                    "color": "004B50",
                    "icon": "icon_test_suite",
                    "isDisabled": false,
                    "inherits": null,
                    "states": [
                        {
                            "id": "93320ff7-1042-403d-93b8-b8ed5dcf2663",
                            "name": "In Planning",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestSuite/states/93320ff7-1042-403d-93b8-b8ed5dcf2663",
                            "customizationType": "system"
                        },
                        {
                            "id": "5fcb8472-3c27-43f5-af7d-f39c9e48f7c6",
                            "name": "In Progress",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestSuite/states/5fcb8472-3c27-43f5-af7d-f39c9e48f7c6",
                            "customizationType": "system"
                        },
                        {
                            "id": "d6920af5-ea13-4c20-aab6-f48d807fbda1",
                            "name": "Completed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 3,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.TestSuite/states/d6920af5-ea13-4c20-aab6-f48d807fbda1",
                            "customizationType": "system"
                        }
                    ],
                    "behaviors": []
                },
                {
                    "referenceName": "Microsoft.VSTS.WorkItemTypes.UserStory",
                    "name": "User Story",
                    "description": "Tracks an activity the user will be able to perform with the product",
                    "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.UserStory",
                    "customization": "system",
                    "color": "0098C7",
                    "icon": "icon_book",
                    "isDisabled": false,
                    "inherits": null,
                    "states": [
                        {
                            "id": "7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "name": "New",
                            "color": "b2b2b2",
                            "stateCategory": "Proposed",
                            "order": 1,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.UserStory/states/7b7e3e8c-e500-40b6-ad56-d59b8d64d757",
                            "customizationType": "system"
                        },
                        {
                            "id": "277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "name": "Active",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 2,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.UserStory/states/277237cd-0bc0-4ffb-bdc6-d358b154ba9e",
                            "customizationType": "system"
                        },
                        {
                            "id": "f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "name": "Resolved",
                            "color": "007acc",
                            "stateCategory": "InProgress",
                            "order": 3,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.UserStory/states/f36cfea7-889a-448e-b5d1-fbc9b134ec82",
                            "customizationType": "system"
                        },
                        {
                            "id": "9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "name": "Closed",
                            "color": "339933",
                            "stateCategory": "Completed",
                            "order": 4,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.UserStory/states/9f479b88-4542-4f9d-8048-5d9c953b5082",
                            "customizationType": "system"
                        },
                        {
                            "id": "0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
                            "name": "Removed",
                            "color": "ffffff",
                            "stateCategory": "Removed",
                            "order": 5,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/workItemTypes/Microsoft.VSTS.WorkItemTypes.UserStory/states/0293a2ce-2a42-4d0e-bbbf-d2237efa0db8",
                            "customizationType": "system"
                        }
                    ],
                    "behaviors": [
                        {
                            "behavior": {
                                "id": "System.RequirementBacklogBehavior",
                                "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/System.RequirementBacklogBehavior"
                            },
                            "isDefault": true,
                            "isLegacyDefault": true,
                            "url": "https://dev.azure.com/dstacey/_apis/work/processes/15e0d0dd-8d89-46ae-ad4a-7c1d9742bd77/behaviors/System.RequirementBacklogBehavior"
                        }
                    ]
                }
            ]
            """;
    }
}
