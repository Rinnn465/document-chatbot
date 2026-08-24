(() => {
    "use strict";

    const report = document.querySelector("[data-report-realtime]");
    if (!report || typeof signalR === "undefined") return;

    const courseId = Number(report.dataset.courseId);
    let refreshTimer = null;

    async function refreshReport() {
        try {
            const response = await fetch(window.location.href, {
                headers: { "X-Requested-With": "SignalR" },
                cache: "no-store"
            });
            if (!response.ok) return;

            const html = await response.text();
            const page = new DOMParser().parseFromString(html, "text/html");
            const current = document.querySelector("[data-report-content]");
            const updated = page.querySelector("[data-report-content]");
            if (current && updated) {
                current.replaceWith(updated);
                document.dispatchEvent(new CustomEvent("report-content-updated"));
            }
        } catch {
            // The next usage event will retry the update.
        }
    }

    function scheduleRefresh(payload) {
        if (payload.courseId !== courseId) return;
        window.clearTimeout(refreshTimer);
        refreshTimer = window.setTimeout(refreshReport, 150);
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/documents")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    connection.on("ChatUsageUpdated", scheduleRefresh);
    connection.start().catch(() => undefined);
})();
