import { useEffect, useId, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { MagnifyingGlass } from '@phosphor-icons/react';
import { useProducts } from '../../features/products/useProducts';

/**
 * Recherche nom / SKU depuis n'importe quel écran authentifié.
 * Ouverte par le bouton ou par Ctrl+K (Cmd+K sur Mac).
 */
export function RechercheGlobale() {
  const [ouvert, setOuvert] = useState(false);
  const [saisie, setSaisie] = useState('');
  const [terme, setTerme] = useState('');
  const [index, setIndex] = useState(0);
  const champ = useRef<HTMLInputElement>(null);
  const titreId = useId();
  const navigate = useNavigate();

  useEffect(() => {
    const delai = setTimeout(() => setTerme(saisie.trim()), 250);
    return () => clearTimeout(delai);
  }, [saisie]);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setOuvert(true);
      }
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  useEffect(() => {
    if (!ouvert) return;
    champ.current?.focus();
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') setOuvert(false);
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [ouvert]);

  const actif = ouvert && terme.length >= 2;
  const { data, isFetching, isError } = useProducts(1, 8, { search: terme }, actif);
  const items = data?.items ?? [];

  useEffect(() => {
    setIndex(0);
  }, [terme]);

  function fermer() {
    setOuvert(false);
    setSaisie('');
    setTerme('');
  }

  function aller(id: string) {
    fermer();
    navigate(`/products/${id}`);
  }

  return (
    <>
      <button
        type="button"
        className="btn-secondary h-11 w-11 px-0"
        aria-label="Rechercher un produit"
        aria-keyshortcuts="Control+K"
        onClick={() => setOuvert(true)}
      >
        <MagnifyingGlass size={18} aria-hidden="true" />
      </button>

      {ouvert && (
        <div
          className="fixed inset-0 z-50 flex items-start justify-center bg-slate-900/40 px-4 pt-[12dvh]"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget) fermer();
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby={titreId}
            className="surface w-full max-w-lg p-4"
          >
            <h2 id={titreId} className="text-base font-semibold text-slate-900">
              Rechercher un produit
            </h2>
            <p className="mt-1 text-sm text-slate-600">Nom ou SKU. Échap ferme.</p>
            <input
              ref={champ}
              type="search"
              className="input mt-3"
              placeholder="Au moins 2 caractères"
              value={saisie}
              aria-label="Nom ou SKU"
              onChange={(e) => setSaisie(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'ArrowDown') {
                  e.preventDefault();
                  setIndex((i) => Math.min(i + 1, Math.max(items.length - 1, 0)));
                } else if (e.key === 'ArrowUp') {
                  e.preventDefault();
                  setIndex((i) => Math.max(i - 1, 0));
                } else if (e.key === 'Enter' && items[index]) {
                  e.preventDefault();
                  aller(items[index].id);
                }
              }}
            />

            <ul className="mt-3 max-h-72 overflow-auto" aria-live="polite">
              {terme.length < 2 && (
                <li className="px-2 py-3 text-sm text-slate-600">Saisissez un nom ou un SKU.</li>
              )}
              {actif && isFetching && (
                <li className="px-2 py-3 text-sm text-slate-600">Recherche…</li>
              )}
              {actif && isError && (
                <li className="px-2 py-3 text-sm text-rose-700">Recherche impossible.</li>
              )}
              {actif && !isFetching && !isError && items.length === 0 && (
                <li className="px-2 py-3 text-sm text-slate-600">Aucun produit.</li>
              )}
              {actif && items.map((p, i) => (
                <li key={p.id}>
                  <button
                    type="button"
                    className={`flex w-full cursor-pointer items-baseline justify-between gap-3 rounded-field px-2 py-2 text-left text-sm ${
                      i === index ? 'bg-slate-100' : 'hover:bg-slate-50'
                    }`}
                    onMouseEnter={() => setIndex(i)}
                    onClick={() => aller(p.id)}
                  >
                    <span className="min-w-0 truncate font-medium text-slate-800">{p.name}</span>
                    <span className="shrink-0 font-mono text-xs text-slate-600">{p.sku}</span>
                  </button>
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}
    </>
  );
}
