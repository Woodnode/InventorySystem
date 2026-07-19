using FluentValidation;
using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Interfaces;
using InventorySystem.Domain.Exceptions;
using MediatR;

namespace InventorySystem.Application.Auth.Commands;

/// <summary>
/// Cas d'usage : créer un compte utilisateur. Le rôle par défaut est "Employe" —
/// seul un Admin peut créer un compte Gestionnaire/Admin (contrôlé au niveau du controller,
/// voir plan §6 : policies de rôles).
/// </summary>
public sealed record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string Role) : IRequest<AuthResultDto>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public static readonly string[] AllowedRoles = { "Admin", "Gestionnaire", "Employe" };

    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Le mot de passe est obligatoire.")
            .MinimumLength(8).WithMessage("Le mot de passe doit contenir au moins 8 caractères.")
            .Matches("[A-Z]").WithMessage("Le mot de passe doit contenir au moins une majuscule.")
            .Matches("[0-9]").WithMessage("Le mot de passe doit contenir au moins un chiffre.");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role).NotEmpty().Must(r => AllowedRoles.Contains(r))
            .WithMessage($"Rôle invalide. Valeurs autorisées : {string.Join(", ", AllowedRoles)}.");
    }
}

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResultDto>
{
    private readonly IIdentityService _identity;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCommandHandler(
        IIdentityService identity,
        ITokenService tokens,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork)
    {
        _identity = identity;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var result = await _identity.CreateUserAsync(
            request.Email, request.Password, request.DisplayName, request.Role, cancellationToken);

        if (!result.Succeeded || result.UserId is null)
            throw new DomainException(string.Join(" | ", result.Errors));

        return await AuthTokenIssuer.IssueAsync(
            _identity, _tokens, _refreshTokens, _unitOfWork, result.UserId.Value, cancellationToken);
    }
}
