import { Routes, Route, Navigate } from 'react-router-dom';
import { Layout } from './app/Layout';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { LoginPage } from './features/auth/LoginPage';
import { DashboardPage } from './features/dashboard/DashboardPage';
import { ProductsPage } from './features/products/ProductsPage';
import { ProductDetailPage } from './features/products/ProductDetailPage';
import { WarehousesPage } from './features/warehouses/WarehousesPage';
import { SuppliersPage } from './features/suppliers/SuppliersPage';
import { MovementsPage } from './features/movements/MovementsPage';

/**
 * Routage complet (voir plan §7) : /login est public, tout le reste passe par
 * ProtectedRoute (redirection vers /login si non authentifié) puis le shell
 * Layout (nav + déconnexion). Les restrictions par rôle plus fines (ex. créer
 * un fournisseur) sont gérées à l'intérieur des pages via RequireRole.
 */
function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route
        element={
          <ProtectedRoute>
            <Layout />
          </ProtectedRoute>
        }
      >
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/products" element={<ProductsPage />} />
        <Route path="/products/:id" element={<ProductDetailPage />} />
        <Route path="/warehouses" element={<WarehousesPage />} />
        <Route path="/suppliers" element={<SuppliersPage />} />
        <Route path="/movements" element={<MovementsPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

export default App;
