namespace InventorySystem.Application.Common.Dtos;

/// <summary>
/// Résultat d'un export prêt à être renvoyé tel quel par un controller
/// (<c>File(Content, ContentType, FileName)</c>) — aucune connaissance HTTP ici (plan §3.2).
/// </summary>
public sealed record ExportFileDto(byte[] Content, string ContentType, string FileName);
