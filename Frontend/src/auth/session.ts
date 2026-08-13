const TOKEN_KEY = 'tb_jwt'
const MANDANT_KEY = 'tb_mandantId'
const BENUTZERNAME_KEY = 'tb_benutzername'
const MANDANTEN_KEY = 'tb_mandanten'

export type MandantInfo = {
  id: number
  name: string
  kuerzel: string
  foregroundColor?: string
  backgroundColor?: string
  rowcounterColor?: string
}

export type Session = {
  token: string
  mandantId: number | null
  benutzername: string | null
  mandanten: MandantInfo[]
}

function parseFarbfeld(
  eintrag: Record<string, unknown>,
  camel: string,
  snake: string,
): string | undefined {
  const wert = eintrag[camel] ?? eintrag[snake]
  return typeof wert === 'string' && wert.trim() !== '' ? wert : undefined
}

function parseMandanten(raw: string | null): MandantInfo[] {
  if (!raw) {
    return []
  }
  try {
    const parsed: unknown = JSON.parse(raw)
    if (!Array.isArray(parsed)) {
      return []
    }
    return parsed.flatMap((eintrag) => {
      if (
        eintrag == null ||
        typeof eintrag !== 'object' ||
        !('id' in eintrag) ||
        !('name' in eintrag)
      ) {
        return []
      }
      const obj = eintrag as Record<string, unknown>
      const id = Number(obj.id)
      if (Number.isNaN(id)) {
        return []
      }
      const name = String(obj.name)
      const kuerzelRaw = obj.kuerzel
      return [
        {
          id,
          name,
          kuerzel:
            kuerzelRaw != null && String(kuerzelRaw).trim() !== ''
              ? String(kuerzelRaw)
              : name,
          foregroundColor: parseFarbfeld(
            obj,
            'foregroundColor',
            'foreground_color',
          ),
          backgroundColor: parseFarbfeld(
            obj,
            'backgroundColor',
            'background_color',
          ),
          rowcounterColor: parseFarbfeld(
            obj,
            'rowcounterColor',
            'rowcounter_color',
          ),
        },
      ]
    })
  } catch {
    return []
  }
}

function leseMandantIdAusQuery(): number | null {
  const query = new URLSearchParams(window.location.search)
  const mandantRaw = query.get('mandantId')
  if (mandantRaw == null || mandantRaw === '') {
    return null
  }
  const mandantId = Number.parseInt(mandantRaw, 10)
  return Number.isNaN(mandantId) ? null : mandantId
}

function sessionAusStorage(
  mandantIdOverride: number | null,
): Session | null {
  const token = sessionStorage.getItem(TOKEN_KEY)?.trim()
  if (!token) {
    return null
  }

  const storedMandant = sessionStorage.getItem(MANDANT_KEY)
  const fromStorage =
    storedMandant != null ? Number.parseInt(storedMandant, 10) : null
  const resolvedMandant =
    mandantIdOverride ??
    (fromStorage != null && !Number.isNaN(fromStorage) ? fromStorage : null)

  if (resolvedMandant != null) {
    sessionStorage.setItem(MANDANT_KEY, String(resolvedMandant))
  }

  const benutzername = sessionStorage.getItem(BENUTZERNAME_KEY)?.trim() || null
  const mandanten = parseMandanten(sessionStorage.getItem(MANDANTEN_KEY))

  return {
    token,
    mandantId: resolvedMandant,
    benutzername,
    mandanten,
  }
}

/**
 * Desktop-Handoff: Token (und Meta) aus URL-Hash → sessionStorage, Hash bereinigen.
 * Browser-Refresh: Session aus sessionStorage wiederherstellen (Login bleibt erhalten).
 * Ohne Token in Hash und Storage: keine Session.
 */
export function bootstrapSessionFromUrl(): Session | null {
  const hash = window.location.hash.startsWith('#')
    ? window.location.hash.slice(1)
    : window.location.hash
  const hashParams = new URLSearchParams(hash)
  const tokenFromHash = hashParams.get('token')?.trim()
  const mandantFromQuery = leseMandantIdAusQuery()

  if (tokenFromHash) {
    const benutzernameFromHash = hashParams.get('benutzername')?.trim() || null
    const mandantenFromHash = parseMandanten(hashParams.get('mandanten'))

    sessionStorage.setItem(TOKEN_KEY, tokenFromHash)
    if (mandantFromQuery != null) {
      sessionStorage.setItem(MANDANT_KEY, String(mandantFromQuery))
    } else {
      sessionStorage.removeItem(MANDANT_KEY)
    }
    if (benutzernameFromHash) {
      sessionStorage.setItem(BENUTZERNAME_KEY, benutzernameFromHash)
    } else {
      sessionStorage.removeItem(BENUTZERNAME_KEY)
    }
    if (mandantenFromHash.length > 0) {
      sessionStorage.setItem(MANDANTEN_KEY, JSON.stringify(mandantenFromHash))
    } else {
      sessionStorage.removeItem(MANDANTEN_KEY)
    }

    const clean = `${window.location.pathname}${window.location.search}`
    window.history.replaceState(null, '', clean)

    return {
      token: tokenFromHash,
      mandantId: mandantFromQuery,
      benutzername: benutzernameFromHash,
      mandanten: mandantenFromHash,
    }
  }

  const bestehende = sessionAusStorage(mandantFromQuery)
  if (bestehende) {
    return bestehende
  }

  clearSession()
  return null
}

export function clearSession(): void {
  sessionStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem(MANDANT_KEY)
  sessionStorage.removeItem(BENUTZERNAME_KEY)
  sessionStorage.removeItem(MANDANTEN_KEY)
}

export function setzeMandantId(mandantId: number): void {
  sessionStorage.setItem(MANDANT_KEY, String(mandantId))
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

export function mandantAnzeigename(
  mandanten: MandantInfo[],
  mandantId: number | null,
): string {
  if (mandantId == null) {
    return '—'
  }
  const gefunden = mandanten.find((m) => m.id === mandantId)
  if (!gefunden) {
    return `Mandant ${mandantId}`
  }
  return gefunden.name || gefunden.kuerzel
}
