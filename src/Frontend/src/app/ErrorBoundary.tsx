import {
  Component,
  type ErrorInfo,
  type ReactNode,
} from 'react';

type Props = {
  children: ReactNode;
  /** Si true, UI compacte pour un boundary de route (pas plein écran). */
  compact?: boolean;
};

type State = { error: Error | null; retryKey: number };

/**
 * Filet de sécurité : un throw de render ne doit pas blanchir toute l'app.
 * `retryKey` remonte les enfants pour forcer un vrai remount au « Réessayer ».
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null, retryKey: 0 };

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('ErrorBoundary', error, info.componentStack);
  }

  private handleRetry = (): void => {
    this.setState((s) => ({ error: null, retryKey: s.retryKey + 1 }));
  };

  render() {
    if (this.state.error) {
      const wrapperClass = this.props.compact
        ? 'flex min-h-[40vh] flex-col items-center justify-center gap-4 px-6'
        : 'flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50 px-6';

      return (
        <div className={wrapperClass}>
          <h1 className="text-xl font-semibold text-slate-900">
            Une erreur inattendue est survenue
          </h1>
          <p className="max-w-md text-center text-sm text-slate-600">
            {this.state.error.message || 'Impossible d’afficher cette page.'}
          </p>
          <button type="button" className="btn-primary" onClick={this.handleRetry}>
            Réessayer
          </button>
        </div>
      );
    }

    return <div key={this.state.retryKey}>{this.props.children}</div>;
  }
}
