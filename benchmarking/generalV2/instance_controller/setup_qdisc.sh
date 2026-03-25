#!/usr/bin/env bash
# setup_qdisc.sh – Configure network shaping on the interface that owns a given IP.
#
# Usage:
#   ./setup_qdisc.sh <ip_address> [bandwidth] [latency] [loss]
#
# Arguments:
#   ip_address   IP address of the local interface to configure (required)
#   bandwidth    Token-bucket rate, e.g. 10mbit, 500kbit  (default: unlimited)
#   latency      One-way added delay, e.g. 50ms, 200ms    (default: 0ms)
#   loss         Packet-loss percentage, e.g. 1%, 0.5%    (default: 0%)
#
# Examples:
#   sudo ./setup_qdisc.sh 192.168.1.10 10mbit 50ms 1%
#   sudo ./setup_qdisc.sh 192.168.1.10              # clears any existing shaping
#
# Requirements: iproute2 (ip, tc)

set -euo pipefail

TARGET_IP="${1:-}"
BANDWIDTH="${2:-0}"     # 0 means no bandwidth limit
LATENCY="${3:-0ms}"
LOSS="${4:-0%}"

# ── Validate input ─────────────────────────────────────────────────────────────
if [[ -z "$TARGET_IP" ]]; then
    echo "Error: ip_address is required." >&2
    echo "Usage: $0 <ip_address> [bandwidth] [latency] [loss]" >&2
    exit 1
fi

# ── Find the interface ─────────────────────────────────────────────────────────
IFACE=$(ip -o addr show | awk -v ip="$TARGET_IP" '$4 ~ "^" ip "/" {print $2; exit}')

if [[ -z "$IFACE" ]]; then
    echo "Error: no local interface found with IP $TARGET_IP" >&2
    exit 1
fi

echo "Interface : $IFACE  (IP: $TARGET_IP)"
echo "Bandwidth : ${BANDWIDTH:-unlimited}"
echo "Latency   : $LATENCY"
echo "Loss      : $LOSS"

# ── Clear existing qdisc ───────────────────────────────────────────────────────
tc qdisc del dev "$IFACE" root 2>/dev/null && echo "Removed existing root qdisc" || true

# ── Determine what shaping is needed ──────────────────────────────────────────
NEED_NETEM=false
NEED_BW=false

[[ "$LATENCY" != "0ms"  && "$LATENCY" != "0"  ]] && NEED_NETEM=true
[[ "$LOSS"    != "0%"   && "$LOSS"    != "0"   ]] && NEED_NETEM=true
[[ "$BANDWIDTH" != "0"  && "$BANDWIDTH" != ""  ]] && NEED_BW=true

# ── Apply shaping ──────────────────────────────────────────────────────────────
if $NEED_BW && $NEED_NETEM; then
    # HTB for rate limiting + netem child for latency/loss
    tc qdisc  add dev "$IFACE" root           handle 1:   htb default 10
    tc class  add dev "$IFACE" parent 1:      classid 1:10 htb rate "$BANDWIDTH" ceil "$BANDWIDTH" burst 32k
    tc qdisc  add dev "$IFACE" parent 1:10    handle 10:  netem delay "$LATENCY" loss "$LOSS"
    echo "Applied HTB ($BANDWIDTH) + netem (delay=$LATENCY loss=$LOSS)"

elif $NEED_BW; then
    # Token-bucket filter – simple bandwidth cap
    tc qdisc add dev "$IFACE" root tbf rate "$BANDWIDTH" burst 32kbit latency 400ms
    echo "Applied TBF rate=$BANDWIDTH"

elif $NEED_NETEM; then
    # netem only – latency / loss, no bandwidth cap
    tc qdisc add dev "$IFACE" root netem delay "$LATENCY" loss "$LOSS"
    echo "Applied netem delay=$LATENCY loss=$LOSS"

else
    echo "No shaping parameters specified – qdisc cleared."
fi

echo "Done."
