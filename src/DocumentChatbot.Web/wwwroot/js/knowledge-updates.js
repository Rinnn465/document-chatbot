(() => {
    "use strict";

    if (typeof signalR === "undefined") return;

    let hideTimer = null;

    function showKnowledgeUpdate(payload) {
        let toast = document.querySelector("[data-knowledge-update]");
        if (!toast) {
            toast = document.createElement("aside");
            toast.className = "knowledge-update-toast";
            toast.dataset.knowledgeUpdate = "";
            toast.setAttribute("role", "status");
            toast.setAttribute("aria-live", "polite");
            document.body.append(toast);
        }

        toast.replaceChildren();
        const label = document.createElement("span");
        label.textContent = "Kho tri thức PRN222 vừa được cập nhật";
        const title = document.createElement("strong");
        title.textContent = payload.documentTitle;
        const detail = document.createElement("small");
        detail.textContent = `${payload.chunkCount} chunks đã sẵn sàng để hỏi đáp.`;
        toast.append(label, title, detail);
        toast.classList.add("is-visible");

        window.clearTimeout(hideTimer);
        hideTimer = window.setTimeout(() => toast.classList.remove("is-visible"), 8000);
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/documents")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    connection.on("KnowledgeBaseUpdated", showKnowledgeUpdate);
    connection.start().catch(() => { });
})();
