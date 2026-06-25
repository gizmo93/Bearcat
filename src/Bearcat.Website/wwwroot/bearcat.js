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
        const copied = document.execCommand("copy");
        if (!copied) {
            throw new Error("Clipboard copy failed");
        }

        return true;
    } finally {
        document.body.removeChild(textarea);
    }
}

function setButtonLabel(button, label) {
    const labelElement = button.querySelector(".bearcat-copy-button-label");
    if (labelElement) {
        labelElement.textContent = label;
    }
}

export async function copyFromTarget(button) {
    const targetId = button.dataset.copyTarget;
    const target = targetId ? document.getElementById(targetId) : null;
    const originalLabel = button.dataset.copyLabel || button.textContent;

    try {
        await copyText(target?.value || "");
        setButtonLabel(button, button.dataset.copySuccessLabel || originalLabel);
    } catch {
        setButtonLabel(button, button.dataset.copyFailureLabel || originalLabel);
    } finally {
        window.setTimeout(() => setButtonLabel(button, originalLabel), 1400);
    }
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

window.bearcat = {
    copyFromTarget,
    copyText,
    setCookie,
    updateScrollAwareHeader,
    lineNumberedTextarea,
};
