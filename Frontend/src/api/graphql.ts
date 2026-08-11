export type Zeiteintrag = {
  id: string
  mandantId: number | null
  datum: string
  kategorie: string
  uhrzeitVon: string | null
  uhrzeitBis: string | null
  pauseBeginn: string | null
  pauseEnde: string | null
  pause2Beginn: string | null
  pause2Ende: string | null
  anmerkung: string | null
}

const ZEITEINTRAEGE_QUERY = `
query Zeiteintraege($von: LocalDate, $bis: LocalDate, $mandantId: Int) {
  zeiteintraege(von: $von, bis: $bis, mandantId: $mandantId) {
    id
    mandantId
    datum
    kategorie
    uhrzeitVon
    uhrzeitBis
    pauseBeginn
    pauseEnde
    pause2Beginn
    pause2Ende
    anmerkung
  }
}
`

type GraphQlError = { message: string }

type GraphQlResponse<T> = {
  data?: T
  errors?: GraphQlError[]
}

function graphqlUrl(): string {
  const fromEnv = import.meta.env.VITE_GRAPHQL_URL?.trim()
  return fromEnv && fromEnv.length > 0
    ? fromEnv.replace(/\/$/, '')
    : 'http://localhost:5108/graphql'
}

export async function ladeZeiteintraege(options: {
  token: string
  von: string
  bis: string
  mandantId: number | null
}): Promise<Zeiteintrag[]> {
  const response = await fetch(graphqlUrl(), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${options.token}`,
    },
    body: JSON.stringify({
      query: ZEITEINTRAEGE_QUERY,
      variables: {
        von: options.von,
        bis: options.bis,
        mandantId: options.mandantId,
      },
    }),
  })

  if (response.status === 401) {
    throw new Error('Nicht angemeldet oder Token abgelaufen.')
  }

  const payload = (await response.json()) as GraphQlResponse<{
    zeiteintraege: Zeiteintrag[]
  }>

  if (payload.errors?.length) {
    throw new Error(payload.errors.map((e) => e.message).join('; '))
  }

  return payload.data?.zeiteintraege ?? []
}
