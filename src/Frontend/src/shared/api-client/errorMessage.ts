import { isAxiosError } from 'axios';

/** Reflète ProblemDetails côté backend (voir ExceptionHandlingMiddleware). */
interface ProblemDetailsBody {
  title?: string;
  detail?: string;
}

/**
 * Traduit une erreur axios en message affichable, en remontant le `detail` renvoyé par
 * ExceptionHandlingMiddleware (règle métier violée, conflit de concurrence, validation…)
 * plutôt qu'un message générique identique pour toutes les pannes.
 */
export function getErrorMessage(error: unknown, fallback = 'Une erreur est survenue. Réessaie.'): string {
  if (!isAxiosError(error)) return fallback;

  if (!error.response) {
    return "Impossible de contacter le serveur — vérifie ta connexion ou que l'API est démarrée.";
  }

  const body = error.response.data as ProblemDetailsBody | undefined;
  if (body?.detail) return body.detail;
  if (body?.title) return body.title;

  switch (error.response.status) {
    case 401:
      return 'Session expirée — reconnecte-toi.';
    case 403:
      return "Tu n'as pas la permission d'effectuer cette action.";
    case 404:
      return 'Ressource introuvable.';
    default:
      return fallback;
  }
}
