import { z } from 'zod';

/** Schéma aligné sur AuthResultDto côté backend. */
export const authResultSchema = z.object({
  accessToken: z.string(),
  accessTokenExpiresAtUtc: z.string(),
  refreshToken: z.string(),
  email: z.string(),
  displayName: z.string(),
  roles: z.array(z.string()),
});
export type AuthResult = z.infer<typeof authResultSchema>;

export const loginSchema = z.object({
  email: z.string().min(1, "L'email est requis").email('Email invalide'),
  password: z.string().min(1, 'Le mot de passe est requis'),
});
export type LoginInput = z.infer<typeof loginSchema>;

export const registerSchema = z.object({
  email: z.string().min(1, "L'email est requis").email('Email invalide'),
  password: z
    .string()
    .min(8, 'Au moins 8 caractères')
    .regex(/[A-Z]/, 'Au moins une majuscule')
    .regex(/[0-9]/, 'Au moins un chiffre'),
  displayName: z.string().min(1, 'Le nom est requis').max(200),
});
export type RegisterInput = z.infer<typeof registerSchema>;

export type Role = 'Admin' | 'Gestionnaire' | 'Employe';
