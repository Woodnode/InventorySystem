using System.Globalization;
using System.Reflection;
using FluentValidation;
using InventorySystem.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InventorySystem.Application;

/// <summary>
/// Enregistrement des services de la couche Application (MediatR + validation).
/// Appelé depuis la composition root de l'API.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Toute règle FluentValidation sans .WithMessage(...) explicite (ex. NotEmpty,
        // EmailAddress, MaximumLength) utilise sinon le texte anglais intégré à la librairie
        // — qui remonte tel quel jusqu'au frontend (voir ValidationBehavior ->
        // ExceptionHandlingMiddleware) et s'afficherait mélangé à une UI en français.
        // Les .WithMessage(...) déjà posés dans les validators restent prioritaires.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("fr");

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
