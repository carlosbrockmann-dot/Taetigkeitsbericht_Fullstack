import { useEffect, useId, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'

export function HeaderMenu() {
  const [offen, setOffen] = useState(false)
  const menuId = useId()
  const rootRef = useRef<HTMLDivElement>(null)
  const location = useLocation()

  useEffect(() => {
    setOffen(false)
  }, [location.pathname])

  useEffect(() => {
    if (!offen) {
      return
    }
    const onPointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOffen(false)
      }
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOffen(false)
      }
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [offen])

  const search = location.search

  return (
    <div className="header-menu" ref={rootRef}>
      <button
        type="button"
        className="header-menu-button"
        aria-expanded={offen}
        aria-haspopup="menu"
        aria-controls={menuId}
        aria-label="Menü öffnen"
        onClick={() => setOffen((v) => !v)}
      >
        ☰
      </button>
      {offen && (
        <ul id={menuId} className="header-menu-list" role="menu">
          <li role="none">
            <Link
              role="menuitem"
              to={{ pathname: '/', search }}
              className={location.pathname === '/' ? 'active' : undefined}
            >
              Tabellenansicht
            </Link>
          </li>
          <li role="none">
            <Link
              role="menuitem"
              to={{ pathname: '/disclaimer', search }}
              className={
                location.pathname === '/disclaimer' ? 'active' : undefined
              }
            >
              Hilfe &amp; Disclaimer
            </Link>
          </li>
        </ul>
      )}
    </div>
  )
}
