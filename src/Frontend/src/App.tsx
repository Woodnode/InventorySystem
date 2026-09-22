import { lazy, Suspense, type ReactNode } from 'react';
import { Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { ErrorBoundary } from './app/ErrorBoundary';
import { Layout } from './app/Layout';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { RequireRole } from './routes/RequireRole';

const LoginPage = lazy(() =>
  import('./features/auth/LoginPage').then((m) => ({ default: m.LoginPage })),
);
const DashboardPage = lazy(() =>
  import('./features/dashboard/DashboardPage').then((m) => ({ default: m.DashboardPage })),
);
const ProductsPage = lazy(() =>
  import('./features/products/ProductsPage').then((m) => ({ default: m.ProductsPage })),
);
const ProductDetailPage = lazy(() =>
  import('./features/products/ProductDetailPage').then((m) => ({
    default: m.ProductDetailPage,
  })),
);
const WarehousesPage = lazy(() =>
  import('./features/warehouses/WarehousesPage').then((m) => ({ default: m.WarehousesPage })),
);
const SuppliersPage = lazy(() =>
  import('./features/suppliers/SuppliersPage').then((m) => ({ default: m.SuppliersPage })),
);
const MovementsPage = lazy(() =>
  import('./features/movements/MovementsPage').then((m) => ({ default: m.MovementsPage })),
);

function RouteFallback() {
  return (
    <div className="flex min-h-[40vh] items-center justify-center text-sm text-slate-500">
      Chargement…
    </div>
  );
}

function App() {
  const location = useLocation();

  // key={location.pathname} : sans elle, naviguer entre deux instances de la même route
  // (ex. /products/A -> /products/B) ne remonte pas l'ErrorBoundary — une erreur déclenchée
  // par le produit A resterait affichée en arrivant sur le produit B (voir AUDIT.md F-2).
  function withRouteBoundary(page: ReactNode) {
    return (
      <ErrorBoundary compact key={location.pathname}>
        {page}
      </ErrorBoundary>
    );
  }

  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route path="/login" element={withRouteBoundary(<LoginPage />)} />

        <Route
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={withRouteBoundary(<DashboardPage />)} />
          <Route path="/products" element={withRouteBoundary(<ProductsPage />)} />
          <Route path="/products/:id" element={withRouteBoundary(<ProductDetailPage />)} />
          <Route path="/warehouses" element={withRouteBoundary(<WarehousesPage />)} />
          <Route
            path="/suppliers"
            element={withRouteBoundary(
              <RequireRole role="Gestionnaire" fallback={<Navigate to="/dashboard" replace />}>
                <SuppliersPage />
              </RequireRole>,
            )}
          />
          <Route path="/movements" element={withRouteBoundary(<MovementsPage />)} />
        </Route>

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </Suspense>
  );
}

export default App;
