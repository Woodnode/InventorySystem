import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// vite.config.ts n'active pas test.globals — @testing-library/react ne trouve donc pas
// d'`afterEach` global au chargement et n'enregistre pas son cleanup automatique tout seul
// (bug identifié au ré-audit : plusieurs render() dans un même describe laissaient le DOM
// d'un test précédent visible pour le suivant). On l'enregistre explicitement ici, une seule
// fois pour toute la suite plutôt que par fichier de test.
afterEach(() => {
  cleanup();
});
