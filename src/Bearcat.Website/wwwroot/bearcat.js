function copyWithTextarea(text) {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.setAttribute("readonly", "");
    textarea.style.position = "fixed";
    textarea.style.inset = "0 auto auto 0";
    textarea.style.opacity = "0";
    document.body.appendChild(textarea);
    textarea.focus({ preventScroll: true });
    textarea.select();
    textarea.setSelectionRange(0, text.length);

    try {
        return document.execCommand("copy");
    } finally {
        document.body.removeChild(textarea);
    }
}

export async function copyText(text) {
    if (!text) {
        throw new Error("No clipboard text provided");
    }

    if (navigator.clipboard?.writeText) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            // Fall through to the textarea copy path.
        }
    }

    if (!copyWithTextarea(text)) {
        throw new Error("Clipboard copy failed");
    }

    return true;
}

const pendingCopies = [];
const maxPendingCopies = 8;

function copyInGesture(text) {
    if (!text) {
        return Promise.resolve(false);
    }

    if (window.isSecureContext && navigator.clipboard?.writeText) {
        return navigator.clipboard.writeText(text).then(() => true, () => false);
    }

    return Promise.resolve(copyWithTextarea(text));
}

document.addEventListener(
    "click",
    event => {
        const element = event.target.closest?.("[data-bearcat-copy]");
        if (!element || element.disabled || element.getAttribute("aria-disabled") === "true") {
            return;
        }

        while (pendingCopies.length >= maxPendingCopies) {
            pendingCopies.shift();
        }

        pendingCopies.push(copyInGesture(element.dataset.bearcatCopy));
    },
    true
);

function isSelectableCommandItem(option) {
    return option.getClientRects().length > 0
        && option.getAttribute("aria-disabled") !== "true"
        && option.getAttribute("data-disabled") !== "true";
}

document.addEventListener("keydown", event => {
    if (event.key !== "Enter" || event.isComposing) {
        return;
    }

    const inputRow = event.target.closest?.("[data-bearcat-select-first-command-item-on-enter]");
    const dialog = inputRow?.closest("[role='dialog']");
    if (!dialog) {
        return;
    }

    const options = [...dialog.querySelectorAll("[role='option']")];
    if (options.some(option => option.getAttribute("aria-selected") === "true")) {
        return;
    }

    options.find(isSelectableCommandItem)?.click();
}, true);

export async function takeCopyResult() {
    const pending = pendingCopies.shift();
    return pending === undefined ? null : await pending;
}

export function setCookie(key, value) {
    try {
        const oneYearInSeconds = 60 * 60 * 24 * 365;
        document.cookie = `${key}=${encodeURIComponent(value)}; path=/; max-age=${oneYearInSeconds}; samesite=lax`;
    } catch {
        // Ignore cookie failures (e.g. disabled cookies).
    }
}

function updateScrollAwareHeader() {
    const header = document.querySelector(".bearcat-app-header");
    if (!header) {
        return;
    }

    header.classList.toggle("bearcat-app-header-scrolled", window.scrollY > 2);
}

function initScrollAwareHeader() {
    updateScrollAwareHeader();

    window.addEventListener("scroll", updateScrollAwareHeader, { passive: true });
    window.addEventListener("resize", updateScrollAwareHeader);
}

initScrollAwareHeader();

const lineNumberedTextarea = (() => {
    const registry = new WeakMap();

    function copyTextStyle(textarea, mirror) {
        const style = window.getComputedStyle(textarea);
        mirror.style.fontFamily = style.fontFamily;
        mirror.style.fontSize = style.fontSize;
        mirror.style.fontWeight = style.fontWeight;
        mirror.style.lineHeight = style.lineHeight;
        mirror.style.letterSpacing = style.letterSpacing;
        mirror.style.tabSize = style.tabSize;
        mirror.style.overflowWrap = style.overflowWrap;
        mirror.style.wordBreak = style.wordBreak;

        const contentWidth =
            textarea.clientWidth -
            parseFloat(style.paddingLeft) -
            parseFloat(style.paddingRight);
        mirror.style.width = Math.max(0, contentWidth) + "px";

        mirror.textContent = "x";
        return mirror.offsetHeight || parseFloat(style.fontSize) * 1.2;
    }

    function writeGutter(gutter, numbers, errorLines) {
        if (errorLines.size === 0) {
            gutter.textContent = numbers.join("\n");
            return;
        }

        const fragment = document.createDocumentFragment();
        let pendingText = "";

        numbers.forEach((number, index) => {
            const separator = index === 0 ? "" : "\n";
            if (number !== "" && errorLines.has(Number(number))) {
                fragment.append(pendingText + separator);
                pendingText = "";
                const marker = document.createElement("span");
                marker.className = "bearcat-code-editor-gutter-error";
                marker.textContent = number;
                fragment.append(marker);
                return;
            }

            pendingText += separator + number;
        });

        fragment.append(pendingText);
        gutter.replaceChildren(fragment);
    }

    function paint(textarea, gutter, mirror, errorLines) {
        if (!textarea || !gutter) {
            return;
        }

        const rowHeight = copyTextStyle(textarea, mirror);
        const lines = textarea.value.split("\n");
        const numbers = [];

        for (let i = 0; i < lines.length; i++) {
            numbers.push(String(i + 1));

            const line = lines[i];
            mirror.textContent = line.length > 0 ? line : " ";
            const rows = Math.max(1, Math.round(mirror.offsetHeight / rowHeight));

            for (let row = 1; row < rows; row++) {
                numbers.push("");
            }
        }

        writeGutter(gutter, numbers, errorLines);
        gutter.scrollTop = textarea.scrollTop;
    }

    function writeCursorPosition(textarea, cursorPosition) {
        if (!cursorPosition) {
            return;
        }

        const target = document.getElementById(cursorPosition.targetId);
        if (!target) {
            return;
        }

        const textBeforeCursor = textarea.value.slice(0, textarea.selectionStart);
        const lineStart = textBeforeCursor.lastIndexOf("\n") + 1;
        const line = textBeforeCursor.split("\n").length;
        const column = textBeforeCursor.length - lineStart + 1;
        target.textContent = cursorPosition.format
            .replace("{0}", String(line))
            .replace("{1}", String(column));
    }

    function attach(textarea, gutter, mirror, cursorPositionTargetId, cursorPositionFormat, errorLineNumbers) {
        if (!textarea || !gutter || !mirror) {
            return;
        }

        if (registry.has(textarea)) {
            setErrorLines(textarea, errorLineNumbers);
            return;
        }

        const cursorPosition = cursorPositionTargetId && cursorPositionFormat
            ? { targetId: cursorPositionTargetId, format: cursorPositionFormat }
            : null;
        const entry = {
            gutter,
            mirror,
            cursorPosition,
            errorLines: new Set(errorLineNumbers ?? []),
            resizeObserver: null,
            hasBeenFocused: false,
        };
        const onInput = () => {
            paint(textarea, gutter, mirror, entry.errorLines);
            writeCursorPosition(textarea, cursorPosition);
        };
        const onCursorMove = () => writeCursorPosition(textarea, cursorPosition);
        const onScroll = () => {
            gutter.scrollTop = textarea.scrollTop;
        };
        entry.resizeObserver = new ResizeObserver(() =>
            paint(textarea, gutter, mirror, entry.errorLines)
        );

        registry.set(textarea, entry);

        textarea.addEventListener("input", onInput);
        textarea.addEventListener("scroll", onScroll, { passive: true });
        textarea.addEventListener("focus", () => {
            entry.hasBeenFocused = true;
        });
        if (cursorPosition) {
            for (const eventName of ["click", "keyup", "select", "focus"]) {
                textarea.addEventListener(eventName, onCursorMove);
            }
        }
        entry.resizeObserver.observe(textarea);

        paint(textarea, gutter, mirror, entry.errorLines);
        writeCursorPosition(textarea, cursorPosition);
    }

    function refresh(textarea) {
        const entry = registry.get(textarea);
        if (entry) {
            paint(textarea, entry.gutter, entry.mirror, entry.errorLines);
            writeCursorPosition(textarea, entry.cursorPosition);
        }
    }

    function setErrorLines(textarea, errorLineNumbers) {
        const entry = registry.get(textarea);
        if (!entry) {
            return;
        }

        entry.errorLines = new Set(errorLineNumbers ?? []);
        paint(textarea, entry.gutter, entry.mirror, entry.errorLines);
    }

    function getOffset(value, line, column) {
        const lines = value.split("\n");
        const lineIndex = Math.min(Math.max(line, 1), lines.length) - 1;
        let offset = 0;

        for (let i = 0; i < lineIndex; i++) {
            offset += lines[i].length + 1;
        }

        return offset + Math.min(Math.max(column, 1) - 1, lines[lineIndex].length);
    }

    function scrollToOffset(textarea, entry, offset) {
        const rowHeight = copyTextStyle(textarea, entry.mirror);
        entry.mirror.textContent = textarea.value.slice(0, offset) + "\u200b";
        const cursorTop = Math.max(0, entry.mirror.offsetHeight - rowHeight);
        textarea.scrollTop = Math.max(0, cursorTop - textarea.clientHeight / 2);
        entry.gutter.scrollTop = textarea.scrollTop;
    }

    function focusPosition(textarea, line, column) {
        const entry = registry.get(textarea);
        if (!entry) {
            return;
        }

        const offset = getOffset(textarea.value, line, column);
        textarea.focus({ preventScroll: true });
        textarea.setSelectionRange(offset, offset);
        scrollToOffset(textarea, entry, offset);
        writeCursorPosition(textarea, entry.cursorPosition);
    }

    function insertText(textarea, text, cursorOffset) {
        const entry = registry.get(textarea);
        if (!entry) {
            return;
        }

        if (!entry.hasBeenFocused) {
            textarea.setSelectionRange(textarea.value.length, textarea.value.length);
        }

        textarea.focus({ preventScroll: true });
        const insertStart = textarea.selectionStart;
        const insertEnd = textarea.selectionEnd;
        const isBlock = text.includes("\n");
        const needsLeadingNewline =
            isBlock && insertStart > 0 && textarea.value[insertStart - 1] !== "\n";
        const needsTrailingNewline =
            isBlock && insertEnd < textarea.value.length && textarea.value[insertEnd] !== "\n";
        const leadingText = needsLeadingNewline ? "\n" : "";
        const insertedText = leadingText + text + (needsTrailingNewline ? "\n" : "");

        if (!document.execCommand("insertText", false, insertedText)) {
            textarea.setRangeText(insertedText, insertStart, insertEnd, "end");
            textarea.dispatchEvent(new Event("input", { bubbles: true }));
        }

        const cursor = insertStart + leadingText.length + cursorOffset;
        textarea.setSelectionRange(cursor, cursor);
        scrollToOffset(textarea, entry, cursor);
        writeCursorPosition(textarea, entry.cursorPosition);
    }

    return { attach, refresh, setErrorLines, focusPosition, insertText };
})();

const saveShortcut = (() => {
    function attach(dotNetReference) {
        const onKeyDown = event => {
            if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) {
                return;
            }

            if (event.key?.toLowerCase() !== "s") {
                return;
            }

            event.preventDefault();
            if (event.repeat) {
                return;
            }

            dotNetReference.invokeMethodAsync("SaveFromShortcutAsync").catch(() => {});
        };

        document.addEventListener("keydown", onKeyDown, true);

        return {
            detach: () => document.removeEventListener("keydown", onKeyDown, true),
        };
    }

    return { attach };
})();

const logView = (() => {
    const registry = new WeakMap();
    const bottomThreshold = 24;

    function isAtBottom(element) {
        return (
            element.scrollHeight - element.scrollTop - element.clientHeight <= bottomThreshold
        );
    }

    function attach(element) {
        if (!element || registry.has(element)) {
            return;
        }

        const state = { pinned: true };
        const onScroll = () => {
            state.pinned = isAtBottom(element);
        };

        registry.set(element, state);
        element.addEventListener("scroll", onScroll, { passive: true });
        element.scrollTop = element.scrollHeight;
    }

    function scrollToBottomIfPinned(element) {
        if (!element) {
            return;
        }

        const state = registry.get(element);
        if (state && !state.pinned) {
            return;
        }

        element.scrollTop = element.scrollHeight;
    }

    return { attach, scrollToBottomIfPinned };
})();

window.bearcat = {
    copyText,
    takeCopyResult,
    setCookie,
    updateScrollAwareHeader,
    lineNumberedTextarea,
    saveShortcut,
    logView,
};
