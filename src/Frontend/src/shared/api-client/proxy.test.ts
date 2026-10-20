import { describe, it, expect } from 'vitest'
import { buildTargetUrl, forwardClientIp } from '../../../functions/_proxy'

describe('relais /api vers le serveur', () => {
  it('garde le domaine du serveur quel que soit le chemin demandé', () => {
    const target = buildTargetUrl('https://inventaire.pages.dev/api/v1/../../etc', 'https://inventaire.203-0-113-10.sslip.io')
    expect(new URL(target).host).toBe('inventaire.203-0-113-10.sslip.io')
  })

  it("transmet l'IP du visiteur en écrasant celle qu'il aurait envoyée lui-même", () => {
    const headers = new Headers({ 'X-Real-Client-IP': '1.2.3.4' })
    forwardClientIp(headers, new Headers({ 'CF-Connecting-IP': '198.51.100.7' }))
    expect(headers.get('X-Real-Client-IP')).toBe('198.51.100.7')

    forwardClientIp(headers, new Headers())
    expect(headers.has('X-Real-Client-IP')).toBe(false)
  })
})
