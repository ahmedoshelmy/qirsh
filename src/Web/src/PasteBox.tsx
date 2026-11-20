import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from './api'
import { intakeSummary } from './format'

const EXAMPLE = 'CIB: Your card ****4821 was charged EGP 250.00 at CARREFOUR MAADI on 05/11/2026. Avail Bal EGP 4,120.00'

export function PasteBox() {
  const [text, setText] = useState('')
  const queryClient = useQueryClient()
  const paste = useMutation({
    mutationFn: api.pasteSms,
    onSuccess: () => {
      setText('')
      // The ledger fills asynchronously (outbox → broker → consumer); refresh shortly after.
      setTimeout(() => void queryClient.invalidateQueries(), 1500)
    },
  })

  return (
    <section className="card paste">
      <h2>Add messages</h2>
      <p className="muted">Paste one or more bank / wallet SMS, English or Arabic. Separate messages with a blank line.</p>
      <textarea
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder={EXAMPLE}
        rows={6}
        dir="auto"
        aria-label="SMS messages"
      />
      <div className="row">
        <button onClick={() => paste.mutate(text)} disabled={!text.trim() || paste.isPending}>
          {paste.isPending ? 'Reading…' : 'Add to ledger'}
        </button>
        {paste.data && <span className="muted">{intakeSummary(paste.data.recorded, paste.data.duplicates, paste.data.skipped)}</span>}
        {paste.error && <span className="error">{paste.error.message}</span>}
      </div>
    </section>
  )
}
