#!/usr/bin/env bash
# End-to-end smoke test against a running compose stack (docker compose up -d --build --wait).
set -euo pipefail

ING=${ING:-http://localhost:5101}
LED=${LED:-http://localhost:5201}
BUD=${BUD:-http://localhost:5301}
WEB=${WEB:-http://localhost:8080}
month=$(date +%Y-%m)
today=$(date +%d/%m/%Y)

for url in "$ING" "$LED" "$BUD"; do
  printf '%s/health/ready -> ' "$url"; curl -fsS "$url/health/ready"; echo
done
printf 'web -> '; curl -fsS "$WEB/healthz"

echo "== budget first (backfill is 0), then three messages"
curl -fsS -X POST "$BUD/api/budgets" -H 'Content-Type: application/json' \
  -d "{\"category\":\"Groceries\",\"limit\":1000,\"month\":\"$month\"}" >/dev/null

paste=$(cat <<SMS
CIB: Your card ****4821 was charged EGP 640.00 at CARREFOUR MAADI on $today. Avail Bal EGP 5,360.00

QNB ALAHLI: Purchase of EGP 95.50 at UBER TRIP using card ending 3307 on $today.

البنك الأهلي المصري: تم خصم مبلغ 210.00 جنيه من بطاقتكم المنتهية ب 7781 لدى SEOUDI MARKET بتاريخ $today
SMS
)
body=$(python3 -c 'import json,sys; print(json.dumps({"text": sys.argv[1]}))' "$paste")
curl -fsS -X POST "$ING/api/sms" -H 'Content-Type: application/json' -d "$body" | python3 -c 'import json,sys; d=json.load(sys.stdin); print("intake:", d["recorded"], "recorded,", d["duplicates"], "duplicates,", d["skipped"], "skipped")'

echo "== waiting for the ledger (outbox -> RabbitMQ -> consumer)"
rows=0
for _ in $(seq 1 20); do
  rows=$(curl -fsS "$LED/api/transactions?month=$month" | python3 -c 'import json,sys; print(len(json.load(sys.stdin)))')
  [ "$rows" -ge 3 ] && break
  sleep 1
done
echo "ledger rows: $rows"
[ "$rows" -ge 3 ] || { echo "SMOKE FAIL: ledger has $rows rows"; exit 1; }

spent=0
for _ in $(seq 1 20); do
  spent=$(curl -fsS "$BUD/api/budgets?month=$month" | python3 -c 'import json,sys; print(next((b["spent"] for b in json.load(sys.stdin) if b["category"]=="Groceries"), 0))')
  python3 -c "import sys; sys.exit(0 if float('$spent') > 0 else 1)" && break
  sleep 1
done
echo "groceries budget spent: $spent"
python3 -c "import sys; sys.exit(0 if float('$spent') > 0 else 1)" || { echo "SMOKE FAIL: budget not updated"; exit 1; }
printf 'web proxy -> '; curl -fsS "$WEB/api/budgets?month=$month" | head -c 120; echo
echo "SMOKE PASS"
