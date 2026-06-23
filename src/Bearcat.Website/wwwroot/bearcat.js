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

window.bearcat = {
    copyFromTarget,
    copyText,
    setCookie,
    updateScrollAwareHeader,
};
