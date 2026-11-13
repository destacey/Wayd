using Serilog;
using Serilog.Configuration;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;

namespace Wayd.Infrastructure.Logging;

/// <summary>
/// Registers the <c>Serilog.Exceptions</c> enricher with the destructurer and filter that keep entity values,
/// entity graphs and message payloads out of a logged exception's <c>ExceptionDetail</c>.
/// </summary>
/// <remarks>
/// Do not also add <c>WithExceptionDetails</c> to <c>logger.json</c> or a <c>Serilog__Enrich__*</c> variable.
/// The enricher adds <c>ExceptionDetail</c> only when the event has none yet, so a second registration
/// without these options can set the property first and log what this one leaves out.
/// <c>ExceptionDetailsTests</c> fails if the configuration files register it.
/// </remarks>
internal static class ExceptionDetails
{
    /// <summary>Adds exception details to every log event that carries an exception.</summary>
    public static LoggerConfiguration WithWaydExceptionDetails(this LoggerEnrichmentConfiguration enrich) =>
        enrich.WithExceptionDetails(new DestructuringOptionsBuilder()
            .WithDefaultDestructurers()
            .WithDestructurers([new DbUpdateExceptionDestructurer()])
            .WithFilter(new PayloadPropertyFilter()));
}
