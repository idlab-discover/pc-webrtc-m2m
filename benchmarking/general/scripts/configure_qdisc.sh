#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  configure_qdisc.sh apply <target-ip> [--bandwidth-kbit N] [--latency-ms N] [--jitter-ms N] [--loss-percent N]
  configure_qdisc.sh clear <target-ip>
EOF
}

if [[ $# -lt 2 ]]; then
  usage
  exit 1
fi

action="$1"
target_ip="$2"
shift 2

bandwidth_kbit=""
latency_ms=""
jitter_ms=""
loss_percent=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --bandwidth-kbit)
      bandwidth_kbit="$2"
      shift 2
      ;;
    --latency-ms)
      latency_ms="$2"
      shift 2
      ;;
    --jitter-ms)
      jitter_ms="$2"
      shift 2
      ;;
    --loss-percent)
      loss_percent="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

interface_name="$(ip -o route get "$target_ip" | awk '{for (i=1; i<=NF; ++i) if ($i == "dev") { print $(i+1); exit }}')"
if [[ -z "$interface_name" ]]; then
  echo "Unable to determine interface for $target_ip" >&2
  exit 1
fi

tc qdisc del dev "$interface_name" root 2>/dev/null || true

if [[ "$action" == "clear" ]]; then
  exit 0
fi

delay_args=()
if [[ -n "$latency_ms" ]]; then
  delay_args+=(delay "${latency_ms}ms")
  if [[ -n "$jitter_ms" ]]; then
    delay_args+=("${jitter_ms}ms")
  fi
fi
if [[ -n "$loss_percent" ]]; then
  delay_args+=(loss "${loss_percent}%")
fi

if [[ -n "$bandwidth_kbit" ]]; then
  tc qdisc add dev "$interface_name" root handle 1: tbf rate "${bandwidth_kbit}kbit" burst 32kbit latency 400ms
  if [[ ${#delay_args[@]} -gt 0 ]]; then
    tc qdisc add dev "$interface_name" parent 1:1 handle 10: netem "${delay_args[@]}"
  fi
else
  if [[ ${#delay_args[@]} -eq 0 ]]; then
    echo "At least one shaping parameter must be provided for apply" >&2
    exit 1
  fi
  tc qdisc add dev "$interface_name" root netem "${delay_args[@]}"
fi
