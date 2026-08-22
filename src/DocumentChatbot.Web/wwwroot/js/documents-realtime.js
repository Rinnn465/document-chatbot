(() => {
    "use strict";

    const monitor = document.querySelector("[data-document-realtime]");
    if (!monitor || typeof signalR === "undefined") return;

    const courseId = Number(monitor.dataset.courseId);
    const documentId = monitor.dataset.documentId?.toLowerCase() ?? null;
    const connectionState = monitor.querySelector("[data-document-connection]");
    const eventList = monitor.querySelector("[data-document-events]");
    let refreshTimer = null;

    const stageNames = {
        queued: "Queued",
        uploaded: "Uploaded",
        extracting: "Extracting",
        indexing: "Chunking · Embedding · Indexing",
        indexed: "Indexed",
        failed: "Failed"
    };

    function setConnection(state, label) {
        connectionState.dataset.state = state;
        connectionState.lastChild.textContent = ` ${label}`;
    }

    function appendEvent(payload) {
        if (eventList.children.length === 1 &&
            eventList.firstElementChild?.textContent.includes("Đang chờ")) {
            eventList.replaceChildren();
        }

        const item = document.createElement("li");
        item.className = `realtime-event realtime-event-${payload.stage}`;

        const time = new Intl.DateTimeFormat("vi-VN", {
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit"
        }).format(new Date(payload.occurredAtUtc));

        const heading = document.createElement("div");
        const stage = document.createElement("strong");
        stage.textContent = `${stageNames[payload.stage] ?? payload.stage} · ${payload.progress}%`;
        const timestamp = document.createElement("time");
        timestamp.textContent = time;
        heading.append(stage, timestamp);

        const title = document.createElement("span");
        title.textContent = payload.title;
        const message = document.createElement("p");
        message.textContent = payload.message;

        item.append(heading, title, message);
        eventList.prepend(item);
        while (eventList.children.length > 6) eventList.lastElementChild?.remove();
    }

    function scheduleContentRefresh(payload) {
        if (documentId && payload.documentId.toLowerCase() !== documentId) return;
        window.clearTimeout(refreshTimer);
        refreshTimer = window.setTimeout(refreshContent, 120);
    }

    async function refreshContent() {
        try {
            const response = await fetch(window.location.href, {
                headers: { "X-Requested-With": "SignalR" },
                cache: "no-store"
            });
            if (!response.ok) return;

            const html = await response.text();
            const page = new DOMParser().parseFromString(html, "text/html");
            const current = document.querySelector("[data-document-content]");
            const updated = page.querySelector("[data-document-content]");
            if (current && updated) current.innerHTML = updated.innerHTML;
        } catch {
            // The live monitor remains usable; the next event retries the refresh.
        }
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/documents")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    connection.on("DocumentProcessingChanged", payload => {
        if (payload.courseId !== courseId) return;
        appendEvent(payload);
        scheduleContentRefresh(payload);
    });

    connection.onreconnecting(() => setConnection("connecting", "Đang kết nối lại"));
    connection.onreconnected(() => setConnection("connected", "Realtime"));
    connection.onclose(() => setConnection("disconnected", "Mất kết nối"));

    connection.start()
        .then(() => {
            setConnection("connected", "Realtime");
            refreshContent();
        })
        .catch(() => setConnection("disconnected", "Mất kết nối"));
})();
