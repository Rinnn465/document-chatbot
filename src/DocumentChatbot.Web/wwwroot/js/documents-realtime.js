(() => {
    "use strict";

    const monitor = document.querySelector("[data-document-realtime]");
    if (!monitor || typeof signalR === "undefined") return;

    const courseId = Number(monitor.dataset.courseId);
    const documentId = monitor.dataset.documentId?.toLowerCase() ?? null;
    const connectionState = monitor.querySelector("[data-document-connection]");
    const progress = monitor.querySelector("[data-document-progress]");
    const progressBar = monitor.querySelector("[data-document-progressbar]");
    const progressFill = monitor.querySelector("[data-document-progress-fill]");
    const progressStage = monitor.querySelector("[data-document-stage]");
    const progressTitle = monitor.querySelector("[data-document-title]");
    const progressPercent = monitor.querySelector("[data-document-percent]");
    const progressMessage = monitor.querySelector("[data-document-message]");
    let refreshTimer = null;

    const stageNames = {
        queued: "Đang chờ xử lý",
        uploaded: "Đã tải lên",
        extracting: "Đang trích xuất nội dung",
        indexing: "Đang chunking và indexing",
        indexed: "Hoàn tất",
        failed: "Thất bại"
    };

    function setConnection(state, label) {
        connectionState.dataset.state = state;
        connectionState.lastChild.textContent = ` ${label}`;
    }

    function updateProgress(payload) {
        const value = Math.max(0, Math.min(100, Number(payload.progress) || 0));
        progress.dataset.state = payload.stage;
        progressStage.textContent = stageNames[payload.stage] ?? payload.stage;
        progressTitle.textContent = payload.title;
        progressPercent.textContent = `${value}%`;
        progressMessage.textContent = payload.message;
        progressBar.setAttribute("aria-valuenow", value.toString());
        progressFill.style.transform = `scaleX(${value / 100})`;
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
        updateProgress(payload);
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
