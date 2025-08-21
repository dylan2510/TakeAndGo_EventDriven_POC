(() => {
  const qs = new URLSearchParams(location.search);
  const siteId = qs.get("siteId") || "SITE01";
  const roomId = qs.get("roomId") || "ROOMA";

  const roomEl = document.getElementById("room");
  const grid   = document.getElementById("grid");
  const status = document.getElementById("status");
  const count  = document.getElementById("count");

  roomEl.textContent = `${siteId}:${roomId}`;

  // state store (visitSessionId -> entry)
  const items = new Map();

  function render() {
    grid.innerHTML = "";
    for (const v of items.values()) {
      const card = document.createElement("div");
      card.className = "card fade-in";
      card.innerHTML = `
        <div class="name">${escapeHtml(v.enlisteeName)}</div>
        <div class="muted">Pack location: <b>${escapeHtml(v.packLocation)}</b></div>`;
      grid.appendChild(card);
    }
    count.textContent = String(items.size);
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  }

  async function loadState() {
    const url = `/display/state?siteId=${encodeURIComponent(siteId)}&roomId=${encodeURIComponent(roomId)}`;
    const res = await fetch(url, { cache: "no-store" });
    if (!res.ok) throw new Error(`state ${res.status}`);
    const arr = await res.json();
    items.clear();
    for (const x of arr) items.set(x.visitSessionId, x);
    render();
  }

  // resilient websocket with backoff
  let backoff = 500;
  const backoffMax = 8000;

  function connectWs() {
    const proto = location.protocol === "https:" ? "wss" : "ws";
    const ws = new WebSocket(`${proto}://${location.host}/ws?siteId=${encodeURIComponent(siteId)}&roomId=${encodeURIComponent(roomId)}`);

    ws.onopen = async () => {
      status.textContent = "live";
      backoff = 500;
      try { await loadState(); } catch (e) { console.warn(e); }
    };

    ws.onmessage = (e) => {
      try {
        const msg = JSON.parse(e.data);
        if (msg.type === "append") {
          items.set(msg.visitSessionId, {
            visitSessionId: msg.visitSessionId,
            enlisteeName: msg.enlisteeName,
            packLocation: msg.packLocation
          });
          render();
        } else if (msg.type === "remove") {
          items.delete(msg.visitSessionId);
          render();
        }
      } catch (err) {
        console.error("bad message", err);
      }
    };

    ws.onclose = () => {
      status.textContent = `reconnecting in ${Math.floor(backoff/1000)}s…`;
      setTimeout(connectWs, backoff);
      backoff = Math.min(backoff * 2, backoffMax);
    };

    ws.onerror = () => {
      try { ws.close(); } catch {}
    };
  }

  connectWs();
})();
