import { useCallback, useEffect, useMemo, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { ladeZeiteintraege, type Zeiteintrag } from '../api/graphql'
import {
  clearSession,
  readInitialMonth,
  type Session,
} from '../auth/session'

const MONATSNAMEN = [
  'Januar',
  'Februar',
  'März',
  'April',
  'Mai',
  'Juni',
  'Juli',
  'August',
  'September',
  'Oktober',
  'November',
  'Dezember',
] as const

const WOCHENTAGE = ['So', 'Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa'] as const

type Kalenderzeile = {
  key: string
  datumIso: string
  wochentag: string
  wochenende: boolean
  eintrag: Zeiteintrag | null
}

function daysInMonth(jahr: number, monat: number): number {
  return new Date(jahr, monat, 0).getDate()
}

function toIsoDatum(jahr: number, monat: number, tag: number): string {
  return `${jahr}-${String(monat).padStart(2, '0')}-${String(tag).padStart(2, '0')}`
}

function wochentagInfo(jahr: number, monat: number, tag: number): {
  wochentag: string
  wochenende: boolean
} {
  const weekday = new Date(jahr, monat - 1, tag).getDay()
  return {
    wochentag: WOCHENTAGE[weekday],
    wochenende: weekday === 0 || weekday === 6,
  }
}

function formatDatum(iso: string): string {
  const [y, m, d] = iso.split('-')
  if (!y || !m || !d) return iso
  return `${d}.${m}.${y}`
}

function formatZeit(wert: string | null): string {
  if (!wert) return ''
  return wert.length >= 5 ? wert.slice(0, 5) : wert
}

function formatPause(von: string | null, bis: string | null): string {
  const a = formatZeit(von)
  const b = formatZeit(bis)
  if (!a && !b) return ''
  return `${a}–${b}`
}

function kategorieLabel(wert: string | undefined): string {
  if (!wert) return ''
  switch (wert) {
    case 'ARBEITSTAG':
      return 'Arbeit'
    case 'URLAUB':
      return 'Urlaub'
    case 'KRANKHEIT':
      return 'Krankheit'
    case 'SONDERURLAUB':
      return 'Sonderurlaub'
    case 'ABWESENHEIT':
      return 'Abwesenheit'
    case 'FEIERTAG':
      return 'Feiertag'
    case 'BETRIEBSFERIEN':
      return 'Betriebsferien'
    default:
      return wert
  }
}

function kategorieZellenKlasse(wert: string | undefined): string | undefined {
  switch (wert) {
    case 'URLAUB':
      return 'kategorie-urlaub'
    case 'KRANKHEIT':
      return 'kategorie-krankheit'
    default:
      return undefined
  }
}

function shiftMonth(jahr: number, monat: number, delta: number): {
  jahr: number
  monat: number
} {
  const date = new Date(jahr, monat - 1 + delta, 1)
  return { jahr: date.getFullYear(), monat: date.getMonth() + 1 }
}

function baueKalenderzeilen(
  jahr: number,
  monat: number,
  eintraege: Zeiteintrag[],
): Kalenderzeile[] {
  const nachDatum = new Map<string, Zeiteintrag[]>()
  for (const e of eintraege) {
    const liste = nachDatum.get(e.datum) ?? []
    liste.push(e)
    nachDatum.set(e.datum, liste)
  }

  const zeilen: Kalenderzeile[] = []
  const anzahlTage = daysInMonth(jahr, monat)
  for (let tag = 1; tag <= anzahlTage; tag++) {
    const datumIso = toIsoDatum(jahr, monat, tag)
    const { wochentag, wochenende } = wochentagInfo(jahr, monat, tag)
    const amTag = nachDatum.get(datumIso) ?? []
    if (amTag.length === 0) {
      zeilen.push({
        key: datumIso,
        datumIso,
        wochentag,
        wochenende,
        eintrag: null,
      })
      continue
    }
    for (const eintrag of amTag) {
      zeilen.push({
        key: eintrag.id,
        datumIso,
        wochentag,
        wochenende,
        eintrag,
      })
    }
  }
  return zeilen
}

type Props = {
  session: Session | null
  mandantId: number | null
}

export function Monatsansicht({ session, mandantId }: Props) {
  const navigate = useNavigate()
  const location = useLocation()
  const initial = useMemo(() => readInitialMonth(), [])
  const [jahr, setJahr] = useState(initial.jahr)
  const [monat, setMonat] = useState(initial.monat)
  const [eintraege, setEintraege] = useState<Zeiteintrag[]>([])
  const [laden, setLaden] = useState(() => Boolean(session?.token))
  const [fehler, setFehler] = useState<string | null>(null)

  const von = useMemo(
    () => `${jahr}-${String(monat).padStart(2, '0')}-01`,
    [jahr, monat],
  )
  const bis = useMemo(
    () =>
      `${jahr}-${String(monat).padStart(2, '0')}-${String(daysInMonth(jahr, monat)).padStart(2, '0')}`,
    [jahr, monat],
  )

  const kalenderzeilen = useMemo(
    () => baueKalenderzeilen(jahr, monat, eintraege),
    [jahr, monat, eintraege],
  )

  const ladenMonat = useCallback(async () => {
    if (!session?.token) {
      return
    }
    setLaden(true)
    setFehler(null)
    try {
      const data = await ladeZeiteintraege({
        token: session.token,
        von,
        bis,
        mandantId,
      })
      setEintraege(data)
    } catch (err) {
      setEintraege([])
      setFehler(err instanceof Error ? err.message : String(err))
    } finally {
      setLaden(false)
    }
  }, [session, von, bis, mandantId])

  useEffect(() => {
    void ladenMonat()
  }, [ladenMonat])

  useEffect(() => {
    const params = new URLSearchParams(location.search)
    params.set('jahr', String(jahr))
    params.set('monat', String(monat))
    if (mandantId != null) {
      params.set('mandantId', String(mandantId))
    } else {
      params.delete('mandantId')
    }
    const next = `?${params.toString()}`
    if (next !== location.search) {
      navigate({ pathname: location.pathname, search: next }, { replace: true })
    }
  }, [jahr, monat, mandantId, location.pathname, location.search, navigate])

  if (!session?.token) {
    return (
      <main className="panel">
        <p className="error">
          Kein Login-Token. Bitte öffnen Sie die Seite über den Desktop-Button
          „Online ansehen“ (Anmeldung am Backend erforderlich).
        </p>
      </main>
    )
  }

  return (
    <>
      <nav className="month-nav" aria-label="Monatsnavigation">
        <button
          type="button"
          onClick={() => {
            const next = shiftMonth(jahr, monat, -1)
            setJahr(next.jahr)
            setMonat(next.monat)
          }}
        >
          ← Vorheriger Monat
        </button>
        <h2>
          {MONATSNAMEN[monat - 1]} {jahr}
        </h2>
        <button
          type="button"
          onClick={() => {
            const next = shiftMonth(jahr, monat, 1)
            setJahr(next.jahr)
            setMonat(next.monat)
          }}
        >
          Nächster Monat →
        </button>
      </nav>

      <main className="panel">
        {laden && <p className="muted">Lade Einträge…</p>}
        {!laden && fehler && (
          <p className="error">
            {fehler}{' '}
            <button
              type="button"
              className="linkish"
              onClick={() => {
                clearSession()
                window.location.reload()
              }}
            >
              Session löschen
            </button>
          </p>
        )}
        {!laden && !fehler && (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Tag</th>
                  <th>Datum</th>
                  <th>Kategorie</th>
                  <th>Von</th>
                  <th>Bis</th>
                  <th>Pause</th>
                  <th>Pause 2</th>
                  <th>Kommentar</th>
                </tr>
              </thead>
              <tbody>
                {kalenderzeilen.map((zeile) => (
                  <tr
                    key={zeile.key}
                    className={zeile.wochenende ? 'wochenende' : undefined}
                  >
                    <td>{zeile.wochentag}</td>
                    <td>{formatDatum(zeile.datumIso)}</td>
                    <td className={kategorieZellenKlasse(zeile.eintrag?.kategorie)}>
                      {kategorieLabel(zeile.eintrag?.kategorie)}
                    </td>
                    <td>{formatZeit(zeile.eintrag?.uhrzeitVon ?? null)}</td>
                    <td>{formatZeit(zeile.eintrag?.uhrzeitBis ?? null)}</td>
                    <td>
                      {formatPause(
                        zeile.eintrag?.pauseBeginn ?? null,
                        zeile.eintrag?.pauseEnde ?? null,
                      )}
                    </td>
                    <td>
                      {formatPause(
                        zeile.eintrag?.pause2Beginn ?? null,
                        zeile.eintrag?.pause2Ende ?? null,
                      )}
                    </td>
                    <td>{zeile.eintrag?.anmerkung ?? ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </main>
    </>
  )
}
