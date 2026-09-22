using FluentValidation;
using InventorySystem.Application.Auth.Dtos;
using InventorySystem.Application.Common.Exceptions;
using InventorySystem.Application.Common.Interfaces;
using MediatR;

namespace InventorySystem.Application.Auth.Commands;

/// <summary>Cas d'usage : authentifier un utilisateur et émettre access + refresh token.</summary>
public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResultDto>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("L'adresse courriel est obligatoire.")
            .EmailAddress().WithMessage("L'adresse courriel n'est pas valide.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Le mot de passe est obligatoire.");
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
{
    private readonly IIdentityService _identity;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;

    public LoginCommandHandler(
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

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Message volontairement générique : ne jamais révéler si c'est l'email ou le
        // mot de passe qui est incorrect (évite l'énumération de comptes).
        var user = await _identity.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken)
            ?? throw new AuthenticationException("Identifiants invalides.");

        return await AuthTokenIssuer.IssueAsync(
            _identity, _tokens, _refreshTokens, _unitOfWork, user.Id, cancellationToken, user);
    }
}
