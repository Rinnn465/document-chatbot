(() => {
    "use strict";

    const workspace = document.querySelector("[data-chat-workspace]");
    if (!workspace || typeof signalR === "undefined") return;

    const feed = workspace.querySelector("[data-chat-feed]");
    const welcomeTemplate = workspace.querySelector("[data-chat-empty]").cloneNode(true);
    const form = workspace.querySelector("[data-chat-form]");
    const question = form.querySelector("textarea");
    const sendButton = form.querySelector("button[type='submit']");
    const count = form.querySelector("[data-character-count]");
    const sessionTitle = workspace.querySelector("[data-session-title]");
    const sessionList = workspace.querySelector("[data-session-list]");
    const newChatButton = workspace.querySelector("[data-new-chat]");
    const connectionState = workspace.querySelector("[data-connection-state]");
    const connectionLabel = workspace.querySelector("[data-connection-label]");
    const liveStatus = workspace.querySelector("[data-live-status]");
    const liveStatusText = workspace.querySelector("[data-live-status-text]");
    const renameDialog = workspace.querySelector("[data-rename-dialog]");
    const renameForm = workspace.querySelector("[data-rename-form]");
    const renameInput = workspace.querySelector("[data-rename-input]");
    const renameError = workspace.querySelector("[data-rename-error]");
    const renameSubmit = workspace.querySelector("[data-rename-submit]");
    const deleteDialog = workspace.querySelector("[data-delete-dialog]");
    const deleteForm = workspace.querySelector("[data-delete-form]");
    const deleteSessionName = workspace.querySelector("[data-delete-session-name]");
    const deleteError = workspace.querySelector("[data-delete-error]");
    const deleteSubmit = workspace.querySelector("[data-delete-submit]");

    const sessions = new Map();
    let activeSessionId = null;
    let renameSessionId = null;
    let deleteSessionId = null;
    let waitingForAnswer = false;
    let reconnectTimer = null;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/chat")
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    function isConnected() {
        return connection.state === signalR.HubConnectionState.Connected;
    }

    function updateControls() {
        const ready = isConnected() && Boolean(activeSessionId) && !waitingForAnswer;
        question.disabled = !ready;
        sendButton.disabled = !ready || question.value.trim().length === 0;
        newChatButton.disabled = !isConnected() || waitingForAnswer;
    }

    function setConnection(state, label) {
        connectionState.dataset.state = state;
        connectionLabel.textContent = label;
        feed.setAttribute("aria-busy", state === "connecting" ? "true" : "false");
        updateControls();
    }

    function setWaiting(waiting, message = "") {
        waitingForAnswer = waiting;
        liveStatus.hidden = !waiting;
        liveStatusText.textContent = message;
        feed.setAttribute("aria-busy", waiting ? "true" : "false");
        updateControls();
    }

    function showWelcome() {
        feed.replaceChildren(welcomeTemplate.cloneNode(true));
        feed.scrollTop = 0;
    }

    function scrollToLatest(behavior = "smooth") {
        feed.scrollTo({ top: feed.scrollHeight, behavior });
    }

    function removeDocumentPreface(value) {
        return value.replace(
            /^\s*(?:(?:theo\s+(?:các\s+)?tài\s+liệu)|(?:dựa\s+(?:trên|theo)\s+(?:các\s+)?tài\s+liệu)|(?:theo\s+(?:các\s+)?nguồn)|(?:(?:các\s+)?tài\s+liệu\s+(?:cho\s+biết|mô\s+tả|nêu)))\s*(?:\[S\d+\])?\s*[:：,\-]?\s*/iu,
            "");
    }

    function appendInlineMarkdown(parent, value) {
        const tokenPattern = /(\*\*[^*\n]+\*\*|`[^`\n]+`)/g;
        let position = 0;
        for (const match of value.matchAll(tokenPattern)) {
            if (match.index > position) {
                parent.append(document.createTextNode(value.slice(position, match.index)));
            }

            const token = match[0];
            const element = token.startsWith("**")
                ? document.createElement("strong")
                : document.createElement("code");
            element.textContent = token.startsWith("**")
                ? token.slice(2, -2)
                : token.slice(1, -1);
            parent.append(element);
            position = match.index + token.length;
        }

        if (position < value.length) {
            parent.append(document.createTextNode(value.slice(position)));
        }
    }

    function renderAssistantMarkdown(container, content) {
        const lines = removeDocumentPreface(content)
            .replace(/\r\n?/g, "\n")
            .split("\n");
        let paragraphLines = [];
        let list = null;
        let listType = null;

        const flushParagraph = () => {
            if (paragraphLines.length === 0) return;
            const paragraph = document.createElement("p");
            appendInlineMarkdown(paragraph, paragraphLines.join(" ").trim());
            container.append(paragraph);
            paragraphLines = [];
        };
        const flushList = () => {
            if (!list) return;
            container.append(list);
            list = null;
            listType = null;
        };

        lines.forEach(line => {
            const unordered = line.match(/^\s*[-*]\s+(.+)$/);
            const ordered = line.match(/^\s*\d+[.)]\s+(.+)$/);
            const nextListType = unordered ? "ul" : ordered ? "ol" : null;

            if (nextListType) {
                flushParagraph();
                if (listType !== nextListType) {
                    flushList();
                    list = document.createElement(nextListType);
                    listType = nextListType;
                }
                const item = document.createElement("li");
                appendInlineMarkdown(item, (unordered?.[1] ?? ordered[1]).trim());
                list.append(item);
                return;
            }

            if (line.trim().length === 0) {
                flushParagraph();
                flushList();
                return;
            }

            flushList();
            paragraphLines.push(line.trim());
        });

        flushParagraph();
        flushList();
    }

    function appendMessage(role, content, options = {}) {
        feed.querySelector("[data-chat-empty]")?.remove();

        const row = document.createElement("article");
        row.className = `message-row message-row-${role}`;
        if (options.error) row.classList.add("message-row-error");

        const avatar = document.createElement("span");
        avatar.className = "message-avatar";
        avatar.setAttribute("aria-hidden", "true");
        avatar.textContent = role === "user" ? "B" : "PRN";

        const contentWrap = document.createElement("div");
        contentWrap.className = "message-content";

        const meta = document.createElement("div");
        meta.className = "message-meta";
        const author = document.createElement("strong");
        author.textContent = options.error
            ? "Không thể trả lời"
            : role === "user" ? "Bạn" : "Trợ lý PRN222";
        const time = document.createElement("time");
        time.textContent = formatTime(options.sentAtUtc);
        meta.append(author, time);

        const body = document.createElement(role === "assistant" ? "div" : "p");
        body.className = "message-body";
        if (role === "assistant" && !options.error) {
            renderAssistantMarkdown(body, content);
        } else {
            body.textContent = content;
        }
        contentWrap.append(meta, body);

        row.append(avatar, contentWrap);
        feed.appendChild(row);
        if (options.scroll !== false) scrollToLatest();
    }

    function formatTime(value) {
        const date = value ? new Date(value) : new Date();
        return new Intl.DateTimeFormat("vi-VN", {
            hour: "2-digit",
            minute: "2-digit"
        }).format(date);
    }

    function formatSessionTime(value) {
        const date = new Date(value);
        const today = new Date();
        if (date.toDateString() === today.toDateString()) return formatTime(value);
        return new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit" }).format(date);
    }

    function renderSessionList() {
        const ordered = [...sessions.values()]
            .sort((left, right) => new Date(right.updatedAtUtc) - new Date(left.updatedAtUtc));

        if (ordered.length === 0) {
            const empty = document.createElement("p");
            empty.className = "session-list-empty";
            empty.textContent = "Chưa có cuộc trò chuyện";
            sessionList.replaceChildren(empty);
            return;
        }

        const fragment = document.createDocumentFragment();
        ordered.forEach(session => {
            const row = document.createElement("div");
            row.className = "session-item-row";
            row.dataset.sessionRow = session.id;
            if (session.id === activeSessionId) row.classList.add("is-active");

            const button = document.createElement("button");
            button.type = "button";
            button.className = "session-item";
            button.dataset.sessionId = session.id;
            button.setAttribute("aria-current", session.id === activeSessionId ? "page" : "false");

            const icon = document.createElement("span");
            icon.className = "session-item-icon";
            icon.setAttribute("aria-hidden", "true");
            icon.textContent = "◇";
            const text = document.createElement("span");
            text.className = "session-item-title";
            text.textContent = session.title;
            const time = document.createElement("time");
            time.textContent = formatSessionTime(session.updatedAtUtc);
            button.append(icon, text, time);

            const menuToggle = document.createElement("button");
            menuToggle.type = "button";
            menuToggle.className = "session-actions-toggle";
            menuToggle.dataset.sessionMenu = session.id;
            menuToggle.setAttribute("aria-label", `Thao tác với ${session.title}`);
            menuToggle.setAttribute("aria-expanded", "false");
            menuToggle.textContent = "•••";

            const menu = document.createElement("div");
            menu.className = "session-actions-menu";
            menu.dataset.sessionMenuPanel = session.id;
            menu.hidden = true;

            const renameButton = document.createElement("button");
            renameButton.type = "button";
            renameButton.dataset.sessionAction = "rename";
            renameButton.dataset.sessionId = session.id;
            renameButton.textContent = "Đổi tên";

            const deleteButton = document.createElement("button");
            deleteButton.type = "button";
            deleteButton.className = "session-delete-action";
            deleteButton.dataset.sessionAction = "delete";
            deleteButton.dataset.sessionId = session.id;
            deleteButton.textContent = "Xóa";

            menu.append(renameButton, deleteButton);
            row.append(button, menuToggle, menu);
            fragment.appendChild(row);
        });
        sessionList.replaceChildren(fragment);
    }

    function closeSessionMenus(exceptPanel = null) {
        sessionList.querySelectorAll("[data-session-menu-panel]").forEach(panel => {
            if (panel !== exceptPanel) panel.hidden = true;
        });
        sessionList.querySelectorAll("[data-session-menu]").forEach(toggle => {
            const panel = sessionList.querySelector(`[data-session-menu-panel="${toggle.dataset.sessionMenu}"]`);
            toggle.setAttribute("aria-expanded", panel && !panel.hidden ? "true" : "false");
        });
    }

    function showDialogError(element, message = "") {
        element.textContent = message;
        element.hidden = message.length === 0;
    }

    function openRenameDialog(id) {
        const session = sessions.get(id);
        if (!session || waitingForAnswer) return;
        renameSessionId = id;
        renameInput.value = session.title;
        showDialogError(renameError);
        renameDialog.showModal();
        renameInput.focus();
        renameInput.select();
    }

    function openDeleteDialog(id) {
        const session = sessions.get(id);
        if (!session || waitingForAnswer) return;
        deleteSessionId = id;
        deleteSessionName.textContent = `“${session.title}”`;
        showDialogError(deleteError);
        deleteDialog.showModal();
    }

    function renderSession(session) {
        activeSessionId = session.id;
        sessionTitle.textContent = session.title;
        renderSessionList();

        if (!session.messages?.length) {
            showWelcome();
        } else {
            feed.replaceChildren();
            session.messages.forEach(message => {
                appendMessage(message.role, message.content, {
                    sentAtUtc: message.sentAtUtc,
                    scroll: false
                });
            });
            scrollToLatest("auto");
        }
        updateControls();
    }

    async function openSession(id) {
        if (waitingForAnswer || id === activeSessionId) return;
        setWaiting(true, "Đang mở cuộc trò chuyện...");
        try {
            const session = await connection.invoke("GetSession", id);
            sessions.set(session.id, session);
            renderSession(session);
        } catch {
            appendMessage("assistant", "Không thể mở phiên chat này.", { error: true });
        } finally {
            setWaiting(false);
            closeMobileSidebar();
            question.focus();
        }
    }

    async function createNewSession() {
        if (!isConnected() || waitingForAnswer) return;
        setWaiting(true, "Đang tạo cuộc trò chuyện mới...");
        try {
            const session = await connection.invoke("CreateSession");
            sessions.set(session.id, session);
            renderSession(session);
        } catch {
            appendMessage("assistant", "Không thể tạo cuộc trò chuyện mới.", { error: true });
        } finally {
            setWaiting(false);
            closeMobileSidebar();
            question.focus();
        }
    }

    async function renameSession(event) {
        event.preventDefault();
        const title = renameInput.value.trim();
        if (!renameSessionId || waitingForAnswer) return;
        if (!title) {
            showDialogError(renameError, "Tên đoạn chat không được để trống.");
            renameInput.focus();
            return;
        }

        renameSubmit.disabled = true;
        setWaiting(true, "Đang đổi tên đoạn chat...");
        try {
            const session = await connection.invoke("RenameSession", renameSessionId, title);
            sessions.set(session.id, session);
            if (session.id === activeSessionId) sessionTitle.textContent = session.title;
            renderSessionList();
            renameDialog.close();
            renameSessionId = null;
        } catch (error) {
            console.error("Không thể đổi tên đoạn chat.", error);
            showDialogError(renameError, "Không thể đổi tên. Hãy thử lại.");
        } finally {
            renameSubmit.disabled = false;
            setWaiting(false);
        }
    }

    async function deleteSession(event) {
        event.preventDefault();
        if (!deleteSessionId || waitingForAnswer) return;

        const id = deleteSessionId;
        deleteSubmit.disabled = true;
        setWaiting(true, "Đang xóa đoạn chat...");
        try {
            await connection.invoke("DeleteSession", id);
            sessions.delete(id);

            if (id === activeSessionId) {
                activeSessionId = null;
                const nextSession = [...sessions.values()]
                    .sort((left, right) => new Date(right.updatedAtUtc) - new Date(left.updatedAtUtc))[0];
                if (nextSession) {
                    renderSession(nextSession);
                } else {
                    const newSession = await connection.invoke("CreateSession");
                    sessions.set(newSession.id, newSession);
                    renderSession(newSession);
                }
            } else {
                renderSessionList();
            }

            deleteDialog.close();
            deleteSessionId = null;
        } catch (error) {
            console.error("Không thể xóa đoạn chat.", error);
            showDialogError(deleteError, "Không thể xóa. Hãy thử lại.");
        } finally {
            deleteSubmit.disabled = false;
            setWaiting(false);
        }
    }

    function resizeComposer() {
        question.style.height = "auto";
        question.style.height = `${Math.min(question.scrollHeight, 144)}px`;
        count.textContent = question.value.length.toString();
        updateControls();
    }

    function scheduleReconnect() {
        if (reconnectTimer !== null) return;
        reconnectTimer = window.setTimeout(() => {
            reconnectTimer = null;
            startConnection();
        }, 4000);
    }

    async function loadInitialChat() {
        try {
            const savedSessions = await connection.invoke("GetSessions");
            sessions.clear();
            savedSessions.forEach(session => sessions.set(session.id, session));

            if (savedSessions.length > 0) {
                renderSession(savedSessions[0]);
            } else {
                const session = await connection.invoke("CreateSession");
                sessions.set(session.id, session);
                renderSession(session);
            }

            question.focus();
        } catch (error) {
            console.error("Không thể tải dữ liệu chat.", error);
            setWaiting(false);
            appendMessage(
                "assistant",
                "Đã kết nối realtime nhưng không thể tải dữ liệu chat. Hãy tải lại trang.",
                { error: true });
        }
    }

    async function startConnection() {
        if (connection.state !== signalR.HubConnectionState.Disconnected) return;

        setConnection("connecting", "Đang kết nối");
        try {
            await connection.start();
            setConnection("connected", "Realtime");
            await loadInitialChat();
        } catch (error) {
            console.error("Không thể kết nối SignalR.", error);
            setConnection("offline", "Mất kết nối");
            scheduleReconnect();
        }
    }

    function closeMobileSidebar() {
        workspace.classList.remove("sidebar-open");
    }

    connection.on("ChatStatusChanged", payload => {
        if (payload.state === "retrieving") {
            setWaiting(true, payload.message);
        } else if (payload.state === "ready") {
            setWaiting(false);
            question.focus();
        }
    });

    connection.on("AnswerReceived", payload => {
        setWaiting(false);
        const current = sessions.get(payload.sessionId);
        let assistantAlreadyKnown = false;
        if (current) {
            const knownMessageIds = new Set(
                (current.messages ?? []).map(message => message.id));
            assistantAlreadyKnown = knownMessageIds.has(payload.assistantMessage.id);
            current.title = payload.sessionTitle;
            current.updatedAtUtc = payload.assistantMessage.sentAtUtc;
            current.messages = [
                ...(current.messages ?? []),
                ...[payload.userMessage, payload.assistantMessage]
                    .filter(message => !knownMessageIds.has(message.id))
            ];
        }
        if (payload.sessionId === activeSessionId) {
            sessionTitle.textContent = payload.sessionTitle;
            if (!assistantAlreadyKnown) {
                appendMessage(
                    "assistant",
                    payload.assistantMessage.content,
                    { sentAtUtc: payload.assistantMessage.sentAtUtc });
            }
        }
        renderSessionList();
        question.focus();
    });

    connection.on("ChatError", payload => {
        setWaiting(false);
        appendMessage("assistant", payload.message, { error: true });
        question.focus();
    });

    connection.onreconnecting(() => {
        setWaiting(false);
        setConnection("connecting", "Đang kết nối lại");
    });
    connection.onreconnected(async () => {
        setConnection("connected", "Realtime");
        await loadInitialChat();
    });
    connection.onclose(() => {
        setWaiting(false);
        setConnection("offline", "Mất kết nối");
        scheduleReconnect();
    });

    form.addEventListener("submit", async event => {
        event.preventDefault();
        const value = question.value.trim();
        if (!value || !activeSessionId || waitingForAnswer) return;

        const requestedSessionId = activeSessionId;
        appendMessage("user", value);
        question.value = "";
        resizeComposer();
        setWaiting(true, "Đang tìm trong tài liệu...");

        try {
            await connection.invoke("Ask", requestedSessionId, value);
        } catch {
            setWaiting(false);
            appendMessage("assistant", "Kết nối bị gián đoạn. Hãy gửi lại câu hỏi.", { error: true });
        }
    });

    question.addEventListener("input", resizeComposer);
    question.addEventListener("keydown", event => {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            form.requestSubmit();
        }
    });

    sessionList.addEventListener("click", event => {
        const action = event.target.closest("[data-session-action]");
        if (action) {
            closeSessionMenus();
            if (action.dataset.sessionAction === "rename") openRenameDialog(action.dataset.sessionId);
            if (action.dataset.sessionAction === "delete") openDeleteDialog(action.dataset.sessionId);
            return;
        }

        const menuToggle = event.target.closest("[data-session-menu]");
        if (menuToggle) {
            const panel = sessionList.querySelector(
                `[data-session-menu-panel="${menuToggle.dataset.sessionMenu}"]`);
            const willOpen = panel?.hidden ?? false;
            closeSessionMenus(willOpen ? panel : null);
            if (panel) panel.hidden = !willOpen;
            menuToggle.setAttribute("aria-expanded", willOpen ? "true" : "false");
            return;
        }

        const button = event.target.closest("[data-session-id]");
        if (button) openSession(button.dataset.sessionId);
    });
    renameForm.addEventListener("submit", renameSession);
    deleteForm.addEventListener("submit", deleteSession);
    workspace.querySelector("[data-rename-cancel]").addEventListener("click", () => renameDialog.close());
    workspace.querySelector("[data-delete-cancel]").addEventListener("click", () => deleteDialog.close());
    renameDialog.addEventListener("close", () => {
        renameSessionId = null;
        showDialogError(renameError);
    });
    deleteDialog.addEventListener("close", () => {
        deleteSessionId = null;
        showDialogError(deleteError);
    });
    document.addEventListener("click", event => {
        if (!event.target.closest(".session-item-row")) closeSessionMenus();
    });
    newChatButton.addEventListener("click", createNewSession);
    workspace.addEventListener("click", event => {
        const suggestion = event.target.closest("[data-suggestion]");
        if (suggestion) {
            question.value = suggestion.dataset.suggestion ?? "";
            resizeComposer();
            question.focus();
        }
    });
    workspace.querySelector("[data-sidebar-toggle]").addEventListener("click", () => {
        workspace.classList.toggle("sidebar-open");
    });
    workspace.querySelector("[data-sidebar-backdrop]").addEventListener("click", closeMobileSidebar);

    resizeComposer();
    startConnection();
})();
