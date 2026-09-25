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

    function paint(textarea, gutter, mirror) {
        if (!textarea || !gutter) {
            return;
        }

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
        const rowHeight = mirror.offsetHeight || parseFloat(style.fontSize) * 1.2;

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

        gutter.textContent = numbers.join("\n");
        gutter.scrollTop = textarea.scrollTop;
    }

    function attach(textarea, gutter, mirror) {
        if (!textarea || !gutter || !mirror) {
            return;
        }

        if (registry.has(textarea)) {
            refresh(textarea);
            return;
        }

        const onInput = () => paint(textarea, gutter, mirror);
        const onScroll = () => {
            gutter.scrollTop = textarea.scrollTop;
        };
        const resizeObserver = new ResizeObserver(() => paint(textarea, gutter, mirror));

        registry.set(textarea, { gutter, mirror, onInput, onScroll, resizeObserver });

        textarea.addEventListener("input", onInput);
        textarea.addEventListener("scroll", onScroll, { passive: true });
        resizeObserver.observe(textarea);

        paint(textarea, gutter, mirror);
    }

    function refresh(textarea) {
        const entry = registry.get(textarea);
        if (entry) {
            paint(textarea, entry.gutter, entry.mirror);
        }
    }

    return { attach, refresh };
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
    logView,
};
