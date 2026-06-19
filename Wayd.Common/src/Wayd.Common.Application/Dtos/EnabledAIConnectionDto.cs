namespace Wayd.Common.Application.Dtos;

public sealed record EnabledAIConnectionDto
{
    /// <summary>
    /// The unique identifier for the AI connection.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the AI connection.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The type of AI connector for the connection.
    /// </summary>
    public required string ConnectorType { get; set; }

    /// <summary>
    /// Indicates whether the AI connection is active or not.
    /// </summary>
    public bool IsActive { get; set; }
}