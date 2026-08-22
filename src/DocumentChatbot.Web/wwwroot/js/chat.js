(() => {
    "use strict";

    const workspace = document.querySelector("[data-chat-workspace]");
    if (!workspace || typeof signalR === "undefined") return;

    const feed = workspace.querySelector("[data-chat-feed]");
    const emptySection = workspace.querySelector("[data-chat-empty]");
    const welcomeTemplate = emptySection ? emptySection.cloneNode(true) : null;
    const form = workspace.querySelector("[data-chat-form]");
    const question = form.querySelector("textarea");
    const sendButton = form.querySelector("button[type='submit']");
    const count = form.querySelector("[data-character-count]");
    const sessionTitle = workspace.querySelector("[data-session-title]");
    const sessionList = workspace.querySelector("[data-session-list]");
    const sessionCountBadge = workspace.querySelector("[data-session-count]");
    const sessionSearch = workspace.querySelector("[data-session-search]");
    const newChatButton = workspace.querySelector("[data-new-chat]");
    const connectionState = workspace.querySelector("[data-connection-state]");
    const connectionLabel = workspace.querySelector("[data-connection-label]");
    const liveStatus = workspace.querySelector("[data-live-status]");
    const liveStatusText = workspace.querySelector("[data-live-status-text]");
    const scrollBottomBtn = workspace.querySelector("[data-scroll-bottom]");
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
    let searchQuery = "";

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
        if (welcomeTemplate) {
            feed.replaceChildren(welcomeTemplate.cloneNode(true));
        } else {
            feed.replaceChildren();
        }
        feed.scrollTop = 0;
    }

    function scrollToLatest(behavior = "smooth") {
        feed.scrollTo({ top: feed.scrollHeight, behavior });
    }

    function checkScrollPosition() {
        if (!scrollBottomBtn) return;
        const distFromBottom = feed.scrollHeight - feed.scrollTop - feed.clientHeight;
        scrollBottomBtn.hidden = distFromBottom <= 140;
    }

    feed.addEventListener("scroll", checkScrollPosition, { passive: true });
    if (scrollBottomBtn) {
        scrollBottomBtn.addEventListener("click", () => scrollToLatest("smooth"));
    }

    function removeDocumentPreface(value) {
        return value.replace(
            /^\s*(?:(?:theo\s+(?:các\s+)?tài\s+liệu)|(?:dựa\s+(?:trên|theo)\s+(?:các\s+)?tài\s+liệu)|(?:theo\s+(?:các\s+)?nguồn)|(?:(?:các\s+)?tài\s+liệu\s+(?:cho\s+biết|mô\s+tả|nêu)))\s*(?:\[S\d+\])?\s*[:：,\-]?\s*/iu,
            "");
    }

    function appendInlineMarkdown(parent, value) {
        // Match bold, inline code, and source markers such as [1] or [S1].
        const tokenPattern = /(\*\*[^*\n]+\*\*|`[^`\n]+`|\[\d+\]|\[(?:Slide|Page|Trang|Chương|Doc|Document|S|Nguồn)[^\]\n]*\])/gi;
        let position = 0;
        for (const match of value.matchAll(tokenPattern)) {
            if (match.index > position) {
                parent.append(document.createTextNode(value.slice(position, match.index)));
            }

            const token = match[0];
            if (token.startsWith("**") && token.endsWith("**")) {
                const bold = document.createElement("strong");
                bold.textContent = token.slice(2, -2);
                parent.append(bold);
            } else if (token.startsWith("`") && token.endsWith("`")) {
                const code = document.createElement("code");
                code.className = "inline-code";
                code.textContent = token.slice(1, -1);
                parent.append(code);
            } else if (token.startsWith("[") && token.endsWith("]")) {
                const labelMatch = token.match(/^\[(?:S)?(\d+)\]$/i);
                const citation = document.createElement(labelMatch ? "button" : "span");
                citation.className = "citation-tag";
                citation.setAttribute("title", labelMatch
                    ? `Xem nguồn kiểm chứng ${token}`
                    : `Nguồn trích dẫn: ${token.slice(1, -1)}`);
                if (labelMatch) {
                    citation.type = "button";
                    citation.dataset.citationLabel = labelMatch[1];
                    citation.setAttribute("aria-label", `Xem nguồn kiểm chứng ${labelMatch[1]}`);
                }
                
                const text = document.createElement("span");
                text.textContent = token.slice(1, -1);

                citation.append(text);
                parent.append(citation);
            }

            position = match.index + token.length;
        }

        if (position < value.length) {
            parent.append(document.createTextNode(value.slice(position)));
        }
    }

    function renderAssistantMarkdown(container, content) {
        const raw = removeDocumentPreface(content).replace(/\r\n?/g, "\n");
        const lines = raw.split("\n");
        
        let paragraphLines = [];
        let list = null;
        let listType = null;
        let inCodeBlock = false;
        let codeBlockLang = "";
        let codeBlockLines = [];

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

        const flushCodeBlock = () => {
            if (!inCodeBlock) return;
            const wrapper = document.createElement("div");
            wrapper.className = "code-block-wrapper";

            const header = document.createElement("div");
            header.className = "code-block-header";

            const langLabel = document.createElement("span");
            langLabel.className = "code-block-lang";
            langLabel.textContent = codeBlockLang || "code";

            const copyBtn = document.createElement("button");
            copyBtn.type = "button";
            copyBtn.className = "code-copy-btn";
            copyBtn.innerHTML = `<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg><span>Sao chép mã</span>`;

            const codeText = codeBlockLines.join("\n");
            copyBtn.addEventListener("click", async () => {
                try {
                    await navigator.clipboard.writeText(codeText);
                    copyBtn.classList.add("copied");
                    copyBtn.querySelector("span").textContent = "Đã chép!";
                    setTimeout(() => {
                        copyBtn.classList.remove("copied");
                        copyBtn.querySelector("span").textContent = "Sao chép mã";
                    }, 2000);
                } catch (e) {
                    console.error("Clipboard copy failed", e);
                }
            });

            header.append(langLabel, copyBtn);

            const pre = document.createElement("pre");
            pre.className = "code-block-pre";
            const code = document.createElement("code");
            code.textContent = codeText;
            pre.append(code);

            wrapper.append(header, pre);
            container.append(wrapper);

            inCodeBlock = false;
            codeBlockLang = "";
            codeBlockLines = [];
        };

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const trimmed = line.trim();

            // Code block start / end
            if (trimmed.startsWith("```")) {
                if (inCodeBlock) {
                    flushCodeBlock();
                } else {
                    flushParagraph();
                    flushList();
                    inCodeBlock = true;
                    codeBlockLang = trimmed.slice(3).trim();
                    codeBlockLines = [];
                }
                continue;
            }

            if (inCodeBlock) {
                codeBlockLines.push(line);
                continue;
            }

            // Unordered list item
            const unordered = line.match(/^\s*[-*]\s+(.+)$/);
            // Ordered list item
            const ordered = line.match(/^\s*(\d+)[.)]\s+(.+)$/);
            const nextListType = unordered ? "ul" : ordered ? "ol" : null;

            if (nextListType) {
                flushParagraph();
                if (listType !== nextListType) {
                    flushList();
                    list = document.createElement(nextListType);
                    listType = nextListType;
                }
                const item = document.createElement("li");
                appendInlineMarkdown(item, (unordered ? unordered[1] : ordered[2]).trim());
                list.append(item);
                continue;
            }

            if (trimmed.length === 0) {
                flushParagraph();
                flushList();
                continue;
            }

            flushList();
            paragraphLines.push(trimmed);
        }

        if (inCodeBlock) flushCodeBlock();
        flushParagraph();
        flushList();
    }

    function createAssistantActions(rawContent) {
        const bar = document.createElement("div");
        bar.className = "message-action-bar";

        // Copy button
        const copyBtn = document.createElement("button");
        copyBtn.type = "button";
        copyBtn.className = "msg-action-btn";
        copyBtn.title = "Sao chép câu trả lời";
        copyBtn.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg><span>Sao chép</span>`;
        copyBtn.addEventListener("click", async () => {
            try {
                await navigator.clipboard.writeText(rawContent);
                copyBtn.classList.add("is-active");
                copyBtn.querySelector("span").textContent = "Đã sao chép";
                setTimeout(() => {
                    copyBtn.classList.remove("is-active");
                    copyBtn.querySelector("span").textContent = "Sao chép";
                }, 2000);
            } catch (err) {
                console.error("Copy error", err);
            }
        });

        // Like button
        const likeBtn = document.createElement("button");
        likeBtn.type = "button";
        likeBtn.className = "msg-action-btn msg-reaction-btn";
        likeBtn.title = "Câu trả lời hữu ích";
        likeBtn.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3zM7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3"></path></svg>`;

        // Dislike button
        const dislikeBtn = document.createElement("button");
        dislikeBtn.type = "button";
        dislikeBtn.className = "msg-action-btn msg-reaction-btn";
        dislikeBtn.title = "Câu trả lời chưa rõ";
        dislikeBtn.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M10 15v4a3 3 0 0 0 3 3l4-9V2H5.72a2 2 0 0 0-2 1.7l-1.38 9a2 2 0 0 0 2 2.3zm7-13h3a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-3"></path></svg>`;

        likeBtn.addEventListener("click", () => {
            likeBtn.classList.toggle("is-active");
            dislikeBtn.classList.remove("is-active");
        });

        dislikeBtn.addEventListener("click", () => {
            dislikeBtn.classList.toggle("is-active");
            likeBtn.classList.remove("is-active");
        });

        bar.append(copyBtn, likeBtn, dislikeBtn);
        return bar;
    }

    function formatCitationLocation(citation) {
        const parts = [];
        if (citation.chapter?.trim()) parts.push(citation.chapter.trim());
        if (citation.slideNumber != null) {
            parts.push(`Slide ${citation.slideNumber}`);
        } else if (citation.pageNumber != null) {
            parts.push(`Trang ${citation.pageNumber}`);
        }
        return parts.join(" · ");
    }

    function createCitationSection(citations) {
        const validCitations = (citations ?? [])
            .filter(citation => citation?.documentName?.trim());

        const section = document.createElement("section");
        section.className = "message-citations";
        section.setAttribute("aria-label", "Nguồn kiểm chứng");

        const heading = document.createElement("div");
        heading.className = "message-citations-heading";

        const title = document.createElement("strong");
        title.textContent = "Nguồn kiểm chứng";
        const count = document.createElement("span");
        count.textContent = validCitations.length.toString();
        count.setAttribute("aria-label", `${validCitations.length} nguồn`);
        heading.append(title, count);

        section.append(heading);

        if (validCitations.length === 0) {
            section.classList.add("message-citations-empty");
            const notice = document.createElement("p");
            notice.className = "citation-empty-notice";
            notice.textContent = "Phản hồi này chưa có nguồn kiểm chứng từ tài liệu môn học.";
            section.append(notice);
            return section;
        }

        const list = document.createElement("ol");
        list.className = "citation-source-list";

        validCitations.forEach((citation, index) => {
            const label = String(citation.label ?? index + 1);
            const item = document.createElement("li");

            const details = document.createElement("details");
            details.className = "citation-source";
            details.dataset.citationLabel = label;
            details.open = true;

            const summary = document.createElement("summary");
            const marker = document.createElement("span");
            marker.className = "citation-source-marker";
            marker.textContent = `[${label}]`;

            const identity = document.createElement("span");
            identity.className = "citation-source-identity";
            const documentName = document.createElement("strong");
            documentName.textContent = citation.documentName.trim();
            identity.append(documentName);

            const locationText = formatCitationLocation(citation);
            if (locationText) {
                const location = document.createElement("small");
                location.textContent = locationText;
                identity.append(location);
            }

            const chevron = document.createElement("span");
            chevron.className = "citation-source-chevron";
            chevron.setAttribute("aria-hidden", "true");
            chevron.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"></polyline></svg>`;

            summary.append(marker, identity, chevron);

            const excerpt = document.createElement("blockquote");
            excerpt.className = "citation-source-excerpt";
            excerpt.textContent = citation.excerpt?.trim() || "Không có đoạn trích để hiển thị.";
            details.append(summary, excerpt);
            item.append(details);
            list.append(item);
        });

        section.append(list);
        return section;
    }

    function appendMessage(role, content, options = {}) {
        feed.querySelector("[data-chat-empty]")?.remove();

        const row = document.createElement("article");
        row.className = `message-row message-row-${role}`;
        if (options.error) row.classList.add("message-row-error");

        const avatar = document.createElement("div");
        avatar.className = "message-avatar";
        avatar.setAttribute("aria-hidden", "true");
        if (role === "user") {
            avatar.innerHTML = `<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>`;
        } else {
            avatar.innerHTML = `<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2.2"><polygon points="12 2 2 7 12 12 22 7 12 2"></polygon><polyline points="2 17 12 22 22 17"></polyline><polyline points="2 12 12 17 22 12"></polyline></svg>`;
        }

        const contentWrap = document.createElement("div");
        contentWrap.className = "message-content";

        const meta = document.createElement("div");
        meta.className = "message-meta";
        const author = document.createElement("strong");
        author.textContent = options.error
            ? "Không thể trả lời"
            : role === "user" ? "Bạn" : "PRN222 Assistant";
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

        if (role === "assistant" && !options.error) {
            const citationSection = createCitationSection(options.citations);
            contentWrap.append(citationSection);
            body.querySelectorAll("[data-citation-label]").forEach(marker => {
                marker.addEventListener("click", () => {
                    const source = [...citationSection.querySelectorAll("[data-citation-label]")]
                        .find(item => item.dataset.citationLabel === marker.dataset.citationLabel);
                    if (!source) return;
                    source.open = true;
                    source.scrollIntoView({ behavior: "smooth", block: "nearest" });
                    source.classList.add("is-highlighted");
                    setTimeout(() => source.classList.remove("is-highlighted"), 1200);
                });
            });
            contentWrap.append(createAssistantActions(content));
        }

        row.append(avatar, contentWrap);
        feed.appendChild(row);
        if (options.scroll !== false) scrollToLatest();
        checkScrollPosition();
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
        let ordered = [...sessions.values()]
            .sort((left, right) => new Date(right.updatedAtUtc) - new Date(left.updatedAtUtc));

        if (sessionCountBadge) {
            sessionCountBadge.textContent = ordered.length.toString();
        }

        if (searchQuery.trim().length > 0) {
            const query = searchQuery.trim().toLowerCase();
            ordered = ordered.filter(s => s.title.toLowerCase().includes(query));
        }

        if (ordered.length === 0) {
            const empty = document.createElement("div");
            empty.className = "session-list-empty";
            empty.innerHTML = searchQuery.trim().length > 0
                ? `<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg><p>Không tìm thấy kết quả</p>`
                : `<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path></svg><p>Chưa có cuộc trò chuyện</p>`;
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
            icon.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path></svg>`;

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
            menuToggle.innerHTML = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.5"><circle cx="12" cy="12" r="1"></circle><circle cx="19" cy="12" r="1"></circle><circle cx="5" cy="12" r="1"></circle></svg>`;

            const menu = document.createElement("div");
            menu.className = "session-actions-menu";
            menu.dataset.sessionMenuPanel = session.id;
            menu.hidden = true;

            const renameButton = document.createElement("button");
            renameButton.type = "button";
            renameButton.dataset.sessionAction = "rename";
            renameButton.dataset.sessionId = session.id;
            renameButton.innerHTML = `<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path></svg><span>Đổi tên</span>`;

            const deleteButton = document.createElement("button");
            deleteButton.type = "button";
            deleteButton.className = "session-delete-action";
            deleteButton.dataset.sessionAction = "delete";
            deleteButton.dataset.sessionId = session.id;
            deleteButton.innerHTML = `<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg><span>Xóa</span>`;

            menu.append(renameButton, deleteButton);
            row.append(button, menuToggle, menu);
            fragment.appendChild(row);
        });
        sessionList.replaceChildren(fragment);
    }

    if (sessionSearch) {
        sessionSearch.addEventListener("input", (e) => {
            searchQuery = e.target.value;
            renderSessionList();
        });
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
                    citations: message.citations,
                    scroll: false
                });
            });
            scrollToLatest("auto");
        }
        updateControls();
    }

    async function openSession(id) {
        if (waitingForAnswer || id === activeSessionId) return;
        setWaiting(true, "Đang tải cuộc trò chuyện...");
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
        question.style.height = `${Math.min(question.scrollHeight, 160)}px`;
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
                    {
                        sentAtUtc: payload.assistantMessage.sentAtUtc,
                        citations: payload.assistantMessage.citations
                    });
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
        setWaiting(true, "Đang tìm trong tài liệu PRN222...");

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
