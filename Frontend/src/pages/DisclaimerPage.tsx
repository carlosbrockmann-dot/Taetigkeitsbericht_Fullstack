export function DisclaimerPage() {
  return (
    <main className="panel disclaimer">
      <h2>Hilfe &amp; Disclaimer</h2>
      <p>
        Die Online-Ansicht des Tätigkeitsberichts dient nur der{' '}
        <strong>Anzeige</strong> bereits abgelegter Zeiteinträge. Erfassen,
        Ändern und Abgeben erfolgen in der Desktop-Anwendung.
      </p>

      <h3>Wo erhalte ich Hilfe?</h3>
      <ul>
        <li>
          <strong>Desktop-App:</strong> Im Reiter Zeiteinträge finden Sie die
          integrierte Hilfe (Markdown-Hilfeseiten im Programm).
        </li>
        <li>
          <strong>Anmeldung / Online-Ansicht:</strong> Öffnen Sie die Seite über
          den Button „Online ansehen“ in der Desktop-App. Ein direkter Aufruf
          ohne gültiges Token ist nicht vorgesehen.
        </li>
        <li>
          <strong>Technische Fragen:</strong> Wenden Sie sich an Ihre interne
          IT- bzw. Anwendungsbetreuung (Mandant / Trägerorganisation).
        </li>
      </ul>

      <h3>Hinweise</h3>
      <ul>
        <li>
          Dargestellte Daten stammen vom Backend und können sich von noch nicht
          abgegebenen lokalen Entwürfen in der Desktop-App unterscheiden.
        </li>
        <li>
          Sitzungsdaten (Token) werden nur beim Aufruf aus der Desktop-App
          übergeben und nicht dauerhaft als Cookie gespeichert.
        </li>
        <li>
          Diese Webansicht ersetzt keine arbeitsrechtliche oder
          datenschutzrechtliche Beratung.
        </li>
      </ul>
    </main>
  )
}
