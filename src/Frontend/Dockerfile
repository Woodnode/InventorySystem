# --- Build ---
FROM node:22-alpine AS build
WORKDIR /src

# Copie des manifests pour un install en cache.
COPY package.json package-lock.json ./
RUN npm ci

# VITE_API_URL est figée au build (Vite l'inline dans le bundle statique, pas de lecture
# à l'exécution) — voir docker-compose.yml pour la valeur par défaut en local.
ARG VITE_API_URL
ENV VITE_API_URL=${VITE_API_URL}

COPY . .
RUN npm run build

# --- Runtime ---
FROM nginx:alpine AS runtime
COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
