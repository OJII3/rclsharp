#!/usr/bin/env bash
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

ROOT_DIR="$(interop_repo_root)"
LOG_DIR="${ROOT_DIR}/artifacts/interop"
DOMAIN_ID="$(interop_pick_domain_id)"
RMW_IMPLEMENTATION="${RMW_IMPLEMENTATION:-rmw_fastrtps_cpp}"

interop_configure_darwin_dyld_path
mkdir -p "${LOG_DIR}"

RCLSHARP_LISTENER_LOG="${LOG_DIR}/fastdds_large_rclsharp_listener.log"
ROS_PUBLISHER_LOG="${LOG_DIR}/fastdds_large_ros_publisher.log"

PIDS=()

cleanup() {
  interop_cleanup_pids "${PIDS[@]:-}"
}
trap cleanup EXIT

interop_require_command dotnet
interop_require_command ros2
interop_require_command python3

export ROS_DOMAIN_ID="${DOMAIN_ID}"
export ROS_LOCALHOST_ONLY="${ROS_LOCALHOST_ONLY:-1}"
export RMW_IMPLEMENTATION

rm -f "${RCLSHARP_LISTENER_LOG}" "${ROS_PUBLISHER_LOG}"

dotnet build "${ROOT_DIR}/samples/TalkerListener/TalkerListener.csproj" >/dev/null
ros2 daemon stop >/dev/null 2>&1 || true

echo "== Fast DDS interop: ROS 2 large String -> rclsharp listener =="
dotnet run --no-build --project "${ROOT_DIR}/samples/TalkerListener" -- listener "${DOMAIN_ID}" 23 rclsharp_large_payload_listener \
  >"${RCLSHARP_LISTENER_LOG}" 2>&1 &
PIDS+=("$!")

sleep 3

MESSAGE="$(python3 - <<'PY'
payload = "large-payload-" + ("x" * 32768)
print("{data: '" + payload + "'}")
PY
)"

ros2 topic pub --times 3 --rate 1 /chatter std_msgs/msg/String "${MESSAGE}" \
  >"${ROS_PUBLISHER_LOG}" 2>&1 &
PIDS+=("$!")

interop_wait_for_log "large-payload-" "${RCLSHARP_LISTENER_LOG}" 45

echo "Fast DDS large payload interop succeeded. Logs: ${LOG_DIR}"
