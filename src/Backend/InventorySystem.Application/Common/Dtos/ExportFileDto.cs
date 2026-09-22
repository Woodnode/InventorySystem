namespace InventorySystem.Application.Common.Dtos;

/// <summary>
/// Export prêt à streaming HTTP (<c>File(Stream, …)</c>).
/// <paramref name="Content"/> doit être seekable ou positionné au début ; le framework dispose le flux.
/// </summary>
public sealed record ExportFileDto(
    Stream Content,
    string ContentType,
    string FileName,
    bool Truncated = false);
