import { buildTargetUrl, isWebSocketUpgrade } from '../_proxy'

interface Env {
  /** Adresse HTTPS de l'API sur le serveur Oracle, ex. https://fleet.141-148-52-17.sslip.io */
  ORIGIN: string
  /** Secret partagé avec Caddy : sans lui, le serveur refuse la requête (403). */
  PROXY_SECRET: string
}

interface Context {
  request: Request
  env: Env
}

/**
 * Relais /api/* vers l'API : le navigateur ne voit que le domaine du site, ce qui garde les
 * cookies de session en première partie (indispensable sur Safari et iOS) et évite CORS.
 * Les requêtes WebSocket (SignalR) sont transmises telles quelles.
 */
export async function onRequest({ request, env }: Context): Promise<Response> {
  if (!env.ORIGIN || !env.PROXY_SECRET) {
    return new Response('Configuration manquante : ORIGIN et PROXY_SECRET', { status: 503 })
  }

  let target: string
  try {
    target = buildTargetUrl(request.url, env.ORIGIN)
  } catch (error) {
    return new Response((error as Error).message, { status: 503 })
  }

  // La requête d'origine est transmise intacte (méthode, en-têtes, cookies, corps, Upgrade).
  const proxied = new Request(target, request)
  proxied.headers.set('X-Forwarded-Host', new URL(request.url).host)
  proxied.headers.set('X-Forwarded-Proto', 'https')
  // Prouve au serveur que la requête passe par ce relais. « set » écrase toute valeur qu'un
  // visiteur aurait tenté d'injecter lui-même.
  proxied.headers.set('X-Portfolio-Proxy', env.PROXY_SECRET)

  if (isWebSocketUpgrade(request.headers)) {
    // Négociation WebSocket : la réponse 101 et le socket doivent passer sans être modifiés.
    return fetch(proxied)
  }

  const response = await fetch(proxied)
  // Réponse recopiée pour pouvoir ajuster les en-têtes si besoin (Set-Cookie est conservé tel quel).
  return new Response(response.body, response)
}
