import { useCallback, useEffect, useRef, useState } from 'react';
import { X } from '@phosphor-icons/react';

/*
 * Deux décodeurs, dans cet ordre.
 *
 * `BarcodeDetector` est fourni par le système sur Android et macOS : rien à charger,
 * décodage accéléré. Il est absent de Chrome sous Windows, où s'arrêter là rendrait
 * la fonction inutilisable. ZXing prend alors le relais — un peu plus de deux cents
 * kilo-octets, chargés seulement à l'ouverture du scanner et jamais au démarrage de
 * l'application.
 */
interface CodeDetecte {
  rawValue: string;
}
interface DetecteurNatif {
  detect(source: CanvasImageSource): Promise<CodeDetecte[]>;
}
type ConstructeurNatif = new (options?: { formats?: string[] }) => DetecteurNatif;

/** Les fiches produit portent un QR contenant le SKU ; les codes-barres couvrent les ISBN. */
const FORMATS = ['qr_code', 'ean_13', 'ean_8', 'code_128', 'code_39'];

function natifDisponible(): ConstructeurNatif | null {
  const g = window as unknown as { BarcodeDetector?: ConstructeurNatif };
  return g.BarcodeDetector ?? null;
}

type Etat =
  | { phase: 'demarrage' }
  | { phase: 'scan'; moteur: string }
  | { phase: 'recherche'; code: string }
  | { phase: 'erreur'; message: string };

export function ScannerCodeBarres({
  onCode,
  onClose,
}: {
  /** Appelé avec le contenu du code lu. Renvoyer false laisse le scanner ouvert. */
  onCode: (code: string) => Promise<boolean> | boolean;
  onClose: () => void;
}) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const fluxRef = useRef<MediaStream | null>(null);
  const actifRef = useRef(true);
  const arretZxingRef = useRef<(() => void) | null>(null);
  const [etat, setEtat] = useState<Etat>({ phase: 'demarrage' });

  /* Le flux doit être coupé explicitement : sans cela la caméra reste allumée,
     témoin compris, après la fermeture de la fenêtre. */
  const arreter = useCallback(() => {
    actifRef.current = false;
    arretZxingRef.current?.();
    arretZxingRef.current = null;
    fluxRef.current?.getTracks().forEach((t) => t.stop());
    fluxRef.current = null;
  }, []);

  const fermer = useCallback(() => {
    arreter();
    onClose();
  }, [arreter, onClose]);

  /** Traite un code lu ; renvoie true si le scanner doit s'arrêter. */
  const traiter = useCallback(
    async (valeur: string) => {
      if (!actifRef.current) return true;
      setEtat({ phase: 'recherche', code: valeur });
      const fini = await onCode(valeur);
      if (fini) {
        arreter();
        return true;
      }
      // Code lu mais inconnu : on reprend le balayage.
      if (actifRef.current) setEtat({ phase: 'scan', moteur: 'reprise' });
      return false;
    },
    [onCode, arreter],
  );

  useEffect(() => {
    if (!navigator.mediaDevices?.getUserMedia) {
      setEtat({ phase: 'erreur', message: "L'accès à la caméra n'est pas disponible ici." });
      return;
    }

    (async () => {
      let flux: MediaStream;
      try {
        // facingMode « environment » : sur un téléphone, la caméra arrière.
        flux = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
      } catch (err) {
        const refus = err instanceof DOMException && err.name === 'NotAllowedError';
        setEtat({
          phase: 'erreur',
          message: refus
            ? "Accès à la caméra refusé. Autorise-le dans les réglages du navigateur, puis rouvre le scanner."
            : "Impossible d'ouvrir la caméra.",
        });
        return;
      }

      if (!actifRef.current) {
        flux.getTracks().forEach((t) => t.stop());
        return;
      }

      fluxRef.current = flux;
      if (videoRef.current) {
        videoRef.current.srcObject = flux;
        await videoRef.current.play().catch(() => undefined);
      }

      const Natif = natifDisponible();
      if (Natif) {
        setEtat({ phase: 'scan', moteur: 'natif' });
        const detecteur = new Natif({ formats: FORMATS });
        let image = 0;

        const boucle = async () => {
          if (!actifRef.current || !videoRef.current) return;
          // Une analyse toutes les cinq images suffit et laisse le processeur tranquille.
          if (image++ % 5 === 0 && videoRef.current.readyState >= 2) {
            try {
              const codes = await detecteur.detect(videoRef.current);
              const valeur = codes[0]?.rawValue?.trim();
              if (valeur && (await traiter(valeur))) return;
            } catch {
              // Une image illisible n'est pas une erreur : on passe à la suivante.
            }
          }
          requestAnimationFrame(boucle);
        };
        requestAnimationFrame(boucle);
        return;
      }

      // Repli : le décodeur n'est téléchargé qu'ici, à l'ouverture du scanner.
      try {
        const { BrowserMultiFormatReader } = await import('@zxing/browser');
        if (!actifRef.current) return;

        const lecteur = new BrowserMultiFormatReader();
        setEtat({ phase: 'scan', moteur: 'zxing' });

        const controles = await lecteur.decodeFromStream(flux, videoRef.current!, (resultat) => {
          const valeur = resultat?.getText()?.trim();
          if (valeur) void traiter(valeur);
        });
        arretZxingRef.current = () => controles.stop();
      } catch {
        setEtat({
          phase: 'erreur',
          message: "Le lecteur de codes n'a pas pu être chargé. Utilise la recherche par SKU.",
        });
      }
    })();

    return arreter;
  }, [traiter, arreter]);

  // Échap ferme, comme pour le menu de navigation.
  useEffect(() => {
    const surTouche = (e: KeyboardEvent) => {
      if (e.key === 'Escape') fermer();
    };
    document.addEventListener('keydown', surTouche);
    return () => document.removeEventListener('keydown', surTouche);
  }, [fermer]);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Scanner un code"
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/70 p-4"
      onClick={(e) => {
        if (e.target === e.currentTarget) fermer();
      }}
    >
      <div className="surface w-full max-w-md overflow-hidden p-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-bold text-slate-800">Scanner un code</h2>
            <p className="mt-1 text-sm text-slate-600">
              Vise le code QR d'une fiche produit ou le code-barres du livre.
            </p>
          </div>
          <button
            type="button"
            className="-mr-2 -mt-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-field text-slate-700 hover:bg-slate-100"
            aria-label="Fermer le scanner"
            onClick={fermer}
          >
            <X size={20} aria-hidden="true" />
          </button>
        </div>

        {etat.phase === 'erreur' ? (
          <p role="alert" className="form-error mt-4">
            {etat.message}
          </p>
        ) : (
          <>
            <div className="relative mt-4 overflow-hidden rounded-card bg-slate-900">
              <video
                ref={videoRef}
                className="aspect-[4/3] w-full object-cover"
                playsInline
                muted
                /* Le flux n'a rien à dire à un lecteur d'écran : le statut sous la
                   vidéo porte l'information utile. */
                aria-hidden="true"
              />
              {/* Repère de visée, purement décoratif. */}
              <div
                className="pointer-events-none absolute inset-8 rounded-card border-2 border-white/70"
                aria-hidden="true"
              />
            </div>

            <p role="status" aria-live="polite" className="mt-3 text-sm text-slate-700">
              {etat.phase === 'demarrage' && 'Ouverture de la caméra…'}
              {etat.phase === 'scan' && 'Recherche d’un code…'}
              {etat.phase === 'recherche' && `Code lu : ${etat.code}`}
            </p>
          </>
        )}
      </div>
    </div>
  );
}
