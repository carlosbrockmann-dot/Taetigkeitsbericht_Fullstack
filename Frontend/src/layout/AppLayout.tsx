import { Link, Outlet, useLocation } from 'react-router-dom'
import { HeaderMenu } from '../components/HeaderMenu'
import type { Session } from '../auth/session'
import { mandantAnzeigename, setzeMandantId } from '../auth/session'

type Props = {
  session: Session | null
  mandantId: number | null
  onMandantChange: (id: number) => void
}

export function AppLayout({ session, mandantId, onMandantChange }: Props) {
  const location = useLocation()
  const mehrereMandanten = (session?.mandanten.length ?? 0) > 1
  const aktuellerMandant = session?.mandanten.find((m) => m.id === mandantId)

  return (
    <div className="page">
      <header className="header">
        <div className="header-title-row">
          <h1>
            <Link
              to={{ pathname: '/', search: location.search }}
              className="header-title-link"
            >
              Tätigkeitsbericht – Online
            </Link>
          </h1>
          <HeaderMenu />
        </div>
        {session?.token ? (
          <p className="sub header-meta">
            <span className="header-meta-left">
              <span>
                Benutzer: <strong>{session.benutzername ?? '—'}</strong>
              </span>
              <span className="meta-sep" aria-hidden="true">
                ·
              </span>
              <span>
                Mandant:{' '}
                <strong>
                  {mandantAnzeigename(session.mandanten, mandantId)}
                </strong>
              </span>
              <span className="meta-sep" aria-hidden="true">
                ·
              </span>
              <span>nur Ansicht (Backend)</span>
            </span>
            {mehrereMandanten && (
              <label className="mandant-select-label">
                <span className="visually-hidden">Mandant wechseln</span>
                <select
                  className="mandant-select"
                  value={mandantId ?? ''}
                  aria-label="Mandant auswählen"
                  style={{
                    color: aktuellerMandant?.foregroundColor,
                    backgroundColor: aktuellerMandant?.backgroundColor,
                  }}
                  onChange={(event) => {
                    const wert = Number.parseInt(event.target.value, 10)
                    if (!Number.isNaN(wert)) {
                      setzeMandantId(wert)
                      onMandantChange(wert)
                    }
                  }}
                >
                  {session.mandanten.map((m) => (
                    <option
                      key={m.id}
                      value={m.id}
                      style={{
                        color: m.foregroundColor,
                        backgroundColor: m.backgroundColor,
                      }}
                    >
                      {m.name}
                    </option>
                  ))}
                </select>
              </label>
            )}
          </p>
        ) : null}
      </header>
      <Outlet />
    </div>
  )
}
