namespace Wayd.Infrastructure.Auditing;

// Max length of 32
public enum TrailType : byte
{
    None = 0,
    Create = 1,
    Update = 2,
    Delete = 3,
    SoftDelete = 4,
    Restore = 5
}