using System.Net;

namespace Wayd.Common.Application.Exceptions;

/// <summary>
/// A CSV file that cannot be read as its rows: a missing column, a cell that does not convert, or broken
/// quoting. <see cref="Interfaces.ICsvService"/> throws it for every such failure, whatever the parser raised,
/// so an import endpoint answers 400 by catching this one type, and one that escapes is still a 400. The
/// parser's exception is its <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class CsvFileException(string message, Exception innerException)
    : CustomException(message, HttpStatusCode.BadRequest, innerException);
