const TOKEN_KEY = 'tb_jwt'
const MANDANT_KEY = 'tb_mandantId'

export type Session = {
  token: string
  mandantId: number | null
}

/** Liest Token aus URL-Hash (#token=…) und Mandant/Jahr/Monat aus Query; speichert Session. */
export function bootstrapSessionFromUrl(): Session | null {
  const hash = window.location.hash.startsWith('#')
    ? window.location.hash.slice(1)
    : window.location.hash
  const hashParams = new URLSearchParams(hash)
  const tokenFromHash = hashParams.get('token')?.trim()

  const query = new URLSearchParams(window.location.search)
  const mandantRaw = query.get('mandantId')
  const mandantId =
    mandantRaw != null && mandantRaw !== '' ? Number.parseInt(mandantRaw, 10) : null

  if (tokenFromHash) {
    sessionStorage.setItem(TOKEN_KEY, tokenFromHash)
    if (mandantId != null && !Number.isNaN(mandantId)) {
      sessionStorage.setItem(MANDANT_KEY, String(mandantId))
    }
    // Token aus der Adresszeile entfernen
    const clean = `${window.location.pathname}${window.location.search}`
    window.history.replaceState(null, '', clean)
  }

  const token = sessionStorage.getItem(TOKEN_KEY)
  if (!token) {
    return null
  }

  const storedMandant = sessionStorage.getItem(MANDANT_KEY)
  const resolvedMandant =
    mandantId != null && !Number.isNaN(mandantId)
      ? mandantId
      : storedMandant != null
        ? Number.parseInt(storedMandant, 10)
        : null

  if (resolvedMandant != null && !Number.isNaN(resolvedMandant)) {
    sessionStorage.setItem(MANDANT_KEY, String(resolvedMandant))
  }

  return {
    token,
    mandantId:
      resolvedMandant != null && !Number.isNaN(resolvedMandant) ? resolvedMandant : null,
  }
}

export function clearSession(): void {
  sessionStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem(MANDANT_KEY)
}

export function readInitialMonth(): { jahr: number; monat: number } {
  const query = new URLSearchParams(window.location.search)
  const now = new Date()
  const jahrRaw = query.get('jahr')
  const monatRaw = query.get('monat')
  const jahr = jahrRaw ? Number.parseInt(jahrRaw, 10) : now.getFullYear()
  const monat = monatRaw ? Number.parseInt(monatRaw, 10) : now.getMonth() + 1
  return {
    jahr: Number.isNaN(jahr) ? now.getFullYear() : jahr,
    monat: Number.isNaN(monat) || monat < 1 || monat > 12 ? now.getMonth() + 1 : monat,
  }
}
