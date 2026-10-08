import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Moon, Sun } from '@phosphor-icons/react';
import { Field } from '../../shared/components/Field';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { useTheme } from '../../app/ThemeProvider';
import { useAuth } from './useAuth';
import { loginSchema, registerSchema, type LoginInput, type RegisterInput } from './types';
import './LoginPage.css';

/**
 * Comptes de démonstration, affichés seulement si le build les définit (site vitrine,
 * poste de développement). Absents de tout autre déploiement : les variables ne sont
 * pas renseignées.
 *
 * `VITE_DEMO_ACCOUNTS` liste « Libellé:adresse », séparés par des virgules, et permet
 * d'ouvrir l'application sous chaque rôle : les écrans diffèrent sensiblement selon
 * qu'on est Employé ou Gestionnaire, et un seul compte ne montrerait pas ces écarts.
 * `VITE_DEMO_EMAIL` reste accepté pour un compte unique.
 */
const DEMO_PASSWORD = import.meta.env.VITE_DEMO_PASSWORD as string | undefined;

const DEMO_ACCOUNTS: { libelle: string; email: string }[] = (() => {
  if (!DEMO_PASSWORD) return [];

  const liste = import.meta.env.VITE_DEMO_ACCOUNTS as string | undefined;
  if (liste) {
    return liste
      .split(',')
      .map((entree) => entree.trim())
      .filter(Boolean)
      .map((entree) => {
        const separateur = entree.indexOf(':');
        // Sans séparateur, l'entrée est une adresse seule : elle se décrit elle-même.
        return separateur === -1
          ? { libelle: entree, email: entree }
          : { libelle: entree.slice(0, separateur).trim(), email: entree.slice(separateur + 1).trim() };
      });
  }

  const unique = import.meta.env.VITE_DEMO_EMAIL as string | undefined;
  return unique ? [{ libelle: 'Compte de démonstration', email: unique }] : [];
})();

/** Inscription libre : fermée sur le site vitrine (l'API la refuse aussi, voir AuthController). */
const REGISTRATION_ENABLED = import.meta.env.VITE_ALLOW_REGISTRATION !== 'false';

/** Écran de connexion + inscription (voir plan §7). */
export function LoginPage() {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const { theme, setTheme } = useTheme();
  const { login, register: registerAccount, isAuthenticated, isInitializing } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [serverError, setServerError] = useState<string | null>(null);

  const redirectTo = (location.state as { from?: string } | null)?.from ?? '/dashboard';

  const loginForm = useForm<LoginInput>({ resolver: zodResolver(loginSchema) });
  const registerForm = useForm<RegisterInput>({ resolver: zodResolver(registerSchema) });

  // Un refresh token persisté déclenche un refresh silencieux au montage (voir AuthContext) :
  // tant qu'il est en vol, on ne sait pas encore si la session est valide. Sans ce garde, le
  // formulaire de connexion s'affichait brièvement avant la redirection vers /dashboard pour
  // un utilisateur déjà connecté qui recharge la page (voir AUDIT.md F-1).
  if (isInitializing) {
    return (
      <div className="auth-wrapper">
        <p className="text-sm font-medium text-slate-700">Chargement…</p>
      </div>
    );
  }

  if (isAuthenticated) {
    return <Navigate to={redirectTo} replace />;
  }

  async function onLogin(input: LoginInput) {
    setServerError(null);
    try {
      await login(input);
      navigate(redirectTo, { replace: true });
    } catch (err) {
      setServerError(getErrorMessage(err, 'Identifiants invalides.'));
    }
  }

  async function onRegister(input: RegisterInput) {
    setServerError(null);
    try {
      await registerAccount(input);
      navigate(redirectTo, { replace: true });
    } catch (err) {
      setServerError(getErrorMessage(err, 'Impossible de créer le compte.'));
    }
  }

  return (
    <div className="auth-wrapper">
      <button
        type="button"
        className="btn-secondary absolute right-4 top-4 h-11 w-11 px-0"
        aria-label={theme === 'dark' ? 'Activer le thème clair' : 'Activer le thème sombre'}
        onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
      >
        {theme === 'dark' ? <Sun size={18} aria-hidden="true" /> : <Moon size={18} aria-hidden="true" />}
      </button>
      <div className="auth-glass-card">
        <div className="auth-header">
          <h1 className="auth-title">Système d'inventaire</h1>
          <p className="auth-subtitle">
            {mode === 'login' ? 'Bienvenue, veuillez vous connecter.' : 'Créez votre compte collaborateur.'}
          </p>
        </div>

        {serverError && <div className="auth-error-msg">{serverError}</div>}

        {mode === 'login' ? (
          <form className="auth-form" onSubmit={loginForm.handleSubmit(onLogin)}>
            <Field label="Email" error={loginForm.formState.errors.email?.message}>
              <input
                type="email"
                className="auth-input-custom"
                autoComplete="email"
                placeholder="nom@entreprise.com"
                {...loginForm.register('email')}
              />
            </Field>
            <Field label="Mot de passe" error={loginForm.formState.errors.password?.message}>
              <input
                type="password"
                className="auth-input-custom"
                autoComplete="current-password"
                placeholder="Votre mot de passe"
                {...loginForm.register('password')}
              />
            </Field>
            <button type="submit" className="auth-submit-btn" disabled={loginForm.formState.isSubmitting}>
              {loginForm.formState.isSubmitting ? 'Connexion en cours…' : 'Se connecter'}
            </button>
          </form>
        ) : (
          <form className="auth-form" onSubmit={registerForm.handleSubmit(onRegister)}>
            <Field label="Nom" error={registerForm.formState.errors.displayName?.message}>
              <input 
                className="auth-input-custom" 
                placeholder="Jean Dupont"
                {...registerForm.register('displayName')} 
              />
            </Field>
            <Field label="Email" error={registerForm.formState.errors.email?.message}>
              <input 
                type="email" 
                className="auth-input-custom" 
                placeholder="jean.dupont@entreprise.com"
                {...registerForm.register('email')} 
              />
            </Field>
            <Field label="Mot de passe" error={registerForm.formState.errors.password?.message}>
              <input 
                type="password" 
                className="auth-input-custom" 
                placeholder="Au moins 8 caractères"
                {...registerForm.register('password')} 
              />
            </Field>
            <button
              type="submit"
              className="auth-submit-btn"
              disabled={registerForm.formState.isSubmitting}
            >
              {registerForm.formState.isSubmitting ? 'Création en cours…' : 'Créer le compte'}
            </button>
          </form>
        )}

        {DEMO_ACCOUNTS.length > 0 && mode === 'login' && (
          <div className="auth-demo">
            <p className="auth-demo-title">
              {DEMO_ACCOUNTS.length > 1 ? 'Comptes de démonstration' : 'Compte de démonstration'}
            </p>
            <p className="auth-demo-line">Mot de passe : {DEMO_PASSWORD}</p>
            <div className="auth-demo-roles">
              {DEMO_ACCOUNTS.map(({ libelle, email }) => (
                <button
                  key={email}
                  type="button"
                  className="auth-switch-btn"
                  /* Le formulaire est rempli, jamais soumis : la connexion reste une
                     action deliberee de la personne. */
                  onClick={() => {
                    loginForm.setValue('email', email);
                    loginForm.setValue('password', DEMO_PASSWORD!);
                  }}
                >
                  {libelle}
                </button>
              ))}
            </div>
          </div>
        )}

        {REGISTRATION_ENABLED && (
          <div className="auth-switch-container">
            <button
              type="button"
              className="auth-switch-btn"
              onClick={() => {
                setServerError(null);
                setMode((m) => (m === 'login' ? 'register' : 'login'));
              }}
            >
              {mode === 'login' ? "Pas encore de compte ? S'inscrire" : 'Déjà un compte ? Se connecter'}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
