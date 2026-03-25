function renderInstances(state) {
  const target = document.getElementById("instances");
  target.innerHTML = "";
  if (!state.instances.length) {
    target.innerHTML = '<div class="instance-card"><p class="muted">No instance controllers connected yet.</p></div>';
    return;
  }
  for (const instance of state.instances) {
    const card = document.createElement("article");
    card.className = "instance-card";
    const phase = instance.status.phase || "idle";
    const message = instance.status.message || "No status reported.";
    const logs = (instance.uploaded_logs || []).slice(-5).map((entry) => `<li>${entry}</li>`).join("");
    card.innerHTML = `
      <div class="badge">${phase}</div>
      <h3>${instance.instance_name}</h3>
      <p class="muted">${instance.base_url}</p>
      <p>${message}</p>
      <p><strong>Recent logs</strong></p>
      <ul>${logs || "<li>None</li>"}</ul>
    `;
    target.appendChild(card);
  }
}

async function refreshState() {
  const response = await fetch("/api/dashboard/state");
  const state = await response.json();
  renderInstances(state);
}

async function saveConfig(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const configName = form.dataset.configName;
  const payload = JSON.parse(form.elements.payload.value);
  const response = await fetch(`/api/configs/${configName}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!response.ok) {
    alert(`Failed to save ${configName}`);
  }
}

async function uploadContent(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const data = new FormData(form);
  const response = await fetch("/api/content/upload", {
    method: "POST",
    body: data,
  });
  if (!response.ok) {
    alert("Content upload failed");
  }
}

async function startExperiment() {
  const response = await fetch("/api/experiments/start", { method: "POST" });
  if (!response.ok) {
    alert("Failed to start experiment");
  }
}

document.querySelectorAll(".config-form").forEach((form) => form.addEventListener("submit", saveConfig));
document.getElementById("upload-form").addEventListener("submit", uploadContent);
document.getElementById("start-experiment").addEventListener("click", startExperiment);
renderInstances(window.__INITIAL_STATE__);
setInterval(refreshState, 5000);
