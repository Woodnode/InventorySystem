/**
 * Construit l'URL de l'API sur le serveur Oracle à partir de l'URL reçue par Cloudflare.
 * Seuls le chemin et la requête sont repris : le domaine technique du serveur n'apparaît
 * jamais dans le navigateur du visiteur.
 */
export function buildTargetUrl(requestUrl: string, origin: string): string {
  const incoming = new URL(requestUrl)
  const target = new URL(origin)

  if (target.protocol !== 'https:') {
    // Le relais transporte des cookies de session : la liaison doit être chiffrée.
    throw new Error(`ORIGIN doit être en https (reçu : ${target.protocol}//)`)
  }

  const base = target.pathname.replace(/\/$/, '')
  target.pathname = base + incoming.pathname
  target.search = incoming.search
  return target.toString()
}

export function isWebSocketUpgrade(headers: { get(name: string): string | null }): boolean {
  return (headers.get('Upgrade') ?? '').toLowerCase() === 'websocket'
}

/** En-tête lu par l'API pour connaître l'IP du visiteur (limites de débit par adresse). */
export const CLIENT_IP_HEADER = 'X-Real-Client-IP'

/**
 * Copie l'IP du visiteur, fournie par Cloudflare, dans l'en-tête lu par l'API. Toute valeur
 * envoyée par le visiteur lui-même est écrasée ou retirée : il ne peut pas choisir son adresse.
 */
export function forwardClientIp(
  target: { set(name: string, value: string): void; delete(name: string): void },
  incoming: { get(name: string): string | null },
): void {
  const ip = incoming.get('CF-Connecting-IP')
  if (ip) target.set(CLIENT_IP_HEADER, ip)
  else target.delete(CLIENT_IP_HEADER)
}
