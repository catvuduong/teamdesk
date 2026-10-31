import { useCallback, useEffect, useState } from 'react'
import { messageOf } from './api'

export interface Loadable<T> {
  data: T | null
  error: string | null
  loading: boolean
  reload: () => void
}

/** Runs `load` whenever `deps` change (or reload is called). Stale responses are ignored. */
export function useLoadable<T>(load: () => Promise<T>, deps: unknown[]): Loadable<T> {
  const [state, setState] = useState<{ data: T | null; error: string | null; loading: boolean }>({
    data: null,
    error: null,
    loading: true,
  })
  const [version, setVersion] = useState(0)

  useEffect(() => {
    let cancelled = false
    setState((previous) => ({ ...previous, loading: true, error: null }))
    load().then(
      (data) => {
        if (!cancelled) setState({ data, error: null, loading: false })
      },
      (error: unknown) => {
        if (!cancelled) setState({ data: null, error: messageOf(error), loading: false })
      },
    )
    return () => {
      cancelled = true
    }
    // The caller lists every value that `load` depends on in `deps`
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [version, ...deps])

  const reload = useCallback(() => setVersion((value) => value + 1), [])
  return { ...state, reload }
}

/** Waits `delay` ms after the value stops changing (used for the search box). */
export function useDebounced<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delay)
    return () => window.clearTimeout(timer)
  }, [value, delay])
  return debounced
}
