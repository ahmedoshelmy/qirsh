import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Dev proxy mirrors nginx.conf: each path goes to the service that owns it.
const ingestion = 'http://localhost:5101'
const ledger = 'http://localhost:5201'
const budgets = 'http://localhost:5301'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api/sms': ingestion,
      '/api/transactions': ledger,
      '/api/categories': ledger,
      '/api/rules': ledger,
      '/api/budgets': budgets,
      '/hubs': { target: budgets, ws: true },
    },
  },
  test: {
    environment: 'node',
  },
})
