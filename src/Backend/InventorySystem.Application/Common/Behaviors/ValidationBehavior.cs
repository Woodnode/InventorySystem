using FluentValidation;
using MediatR;

namespace InventorySystem.Application.Common.Behaviors;

/// <summary>
/// Pipeline MediatR : exécute tous les FluentValidation.IValidator&lt;TRequest&gt;
/// enregistrés avant que le handler ne soit appelé. Validation <b>séquentielle</b>
/// pour rester compatible avec un DbContext scoped (EF Core n'est pas thread-safe).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = new List<FluentValidation.Results.ValidationFailure>();

            foreach (var validator in _validators)
            {
                var result = await validator.ValidateAsync(context, cancellationToken);
                failures.AddRange(result.Errors.Where(f => f is not null));
            }

            if (failures.Count != 0)
                throw new ValidationException(failures);
        }

        return await next();
    }
}
