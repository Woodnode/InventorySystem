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
