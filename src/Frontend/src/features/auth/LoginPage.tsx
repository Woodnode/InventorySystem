import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useLocation, useNavigate } from 'react-router-dom';
import { Field } from '../../shared/components/Field';
import { getErrorMessage } from '../../shared/api-client/errorMessage';
import { useAuth } from './useAuth';
import { loginSchema, registerSchema, type LoginInput, type RegisterInput } from './types';

/** Écran de connexion + inscription (voir plan §7). */
export function LoginPage() {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const { login, register: registerAccount } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [serverError, setServerError] = useState<string | null>(null);

  const redirectTo = (location.state as { from?: string } | null)?.from ?? '/dashboard';

  const loginForm = useForm<LoginInput>({ resolver: zodResolver(loginSchema) });
  const registerForm = useForm<RegisterInput>({ resolver: zodResolver(registerSchema) });

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
    <main className="mx-auto flex min-h-screen max-w-sm flex-col justify-center px-6">
      <h1 className="text-2xl font-semibold tracking-tight text-slate-900">
        Système d'inventaire
      </h1>
      <p className="mt-1 text-sm text-slate-500">
        {mode === 'login' ? 'Connecte-toi pour continuer.' : 'Crée un compte (rôle Employé).'}
      </p>

      {serverError && (
        <p className="mt-4 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-600">{serverError}</p>
      )}

      {mode === 'login' ? (
        <form className="mt-6 flex flex-col gap-4" onSubmit={loginForm.handleSubmit(onLogin)}>
          <Field label="Email" error={loginForm.formState.errors.email?.message}>
            <input
              type="email"
              className="input"
              autoComplete="email"
              {...loginForm.register('email')}
            />
          </Field>
          <Field label="Mot de passe" error={loginForm.formState.errors.password?.message}>
            <input
              type="password"
              className="input"
              autoComplete="current-password"
              {...loginForm.register('password')}
            />
          </Field>
          <button type="submit" className="btn-primary" disabled={loginForm.formState.isSubmitting}>
            {loginForm.formState.isSubmitting ? 'Connexion…' : 'Se connecter'}
          </button>
        </form>
      ) : (
        <form className="mt-6 flex flex-col gap-4" onSubmit={registerForm.handleSubmit(onRegister)}>
          <Field label="Nom" error={registerForm.formState.errors.displayName?.message}>
            <input className="input" {...registerForm.register('displayName')} />
          </Field>
          <Field label="Email" error={registerForm.formState.errors.email?.message}>
            <input type="email" className="input" {...registerForm.register('email')} />
          </Field>
          <Field label="Mot de passe" error={registerForm.formState.errors.password?.message}>
            <input type="password" className="input" {...registerForm.register('password')} />
          </Field>
          <button
            type="submit"
            className="btn-primary"
            disabled={registerForm.formState.isSubmitting}
          >
            {registerForm.formState.isSubmitting ? 'Création…' : 'Créer le compte'}
          </button>
        </form>
      )}

      <button
        type="button"
        className="mt-4 text-sm text-slate-500 underline underline-offset-2 hover:text-slate-700"
        onClick={() => {
          setServerError(null);
          setMode((m) => (m === 'login' ? 'register' : 'login'));
        }}
      >
        {mode === 'login' ? "Pas de compte ? S'inscrire" : 'Déjà un compte ? Se connecter'}
      </button>
    </main>
  );
}
