using Wayd.Common.Application.Dtos;

namespace Wayd.Work.Application.WorkProcesses.Dtos;

public sealed record WorkProcessDto : IMapFrom<WorkProcess>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required SimpleNavigationDto Ownership { get; set; }
    public bool IsActive { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkProcess, WorkProcessDto>()
            .Map(dest => dest.Ownership, src => SimpleNavigationDto.FromEnum(src.OwnershipInfo.Ownership));
    }
}
