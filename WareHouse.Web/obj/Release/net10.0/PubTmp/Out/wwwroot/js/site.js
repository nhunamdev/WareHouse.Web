(() => {
    "use strict";

    const body = document.body;
    const sidebar = document.getElementById("appSidebar");
    const collapseButton = document.getElementById("sidebarCollapseButton");
    const mobileButton = document.getElementById("mobileSidebarButton");
    const backdrop = document.getElementById("sidebarBackdrop");
    const desktopQuery = window.matchMedia("(min-width: 992px)");
    const storageKey = "warehouse.sidebar.collapsed";

    if (!sidebar || !collapseButton || !mobileButton || !backdrop) return;

    const storedCollapsed = () => {
        try {
            return localStorage.getItem(storageKey) === "true";
        } catch {
            return false;
        }
    };

    const rememberCollapsed = (collapsed) => {
        try {
            localStorage.setItem(storageKey, collapsed ? "true" : "false");
        } catch {
            // Storage may be disabled; the sidebar still works for the current page.
        }
    };

    const updateCollapseButton = () => {
        const collapsed = body.classList.contains("sidebar-collapsed");
        const label = collapsed ? "Mở rộng menu" : "Thu gọn menu";
        collapseButton.setAttribute("aria-label", label);
        collapseButton.setAttribute("title", label);
    };

    const setDesktopCollapsed = (collapsed, persist = true) => {
        body.classList.toggle("sidebar-collapsed", collapsed);
        if (persist) rememberCollapsed(collapsed);
        updateCollapseButton();
    };

    const openMobileSidebar = () => {
        body.classList.add("sidebar-mobile-open");
        mobileButton.setAttribute("aria-expanded", "true");
        mobileButton.setAttribute("aria-label", "Đóng menu");
    };

    const closeMobileSidebar = () => {
        body.classList.remove("sidebar-mobile-open");
        mobileButton.setAttribute("aria-expanded", "false");
        mobileButton.setAttribute("aria-label", "Mở menu");
    };

    const applyViewportMode = () => {
        closeMobileSidebar();
        if (desktopQuery.matches) {
            setDesktopCollapsed(storedCollapsed(), false);
        } else {
            body.classList.remove("sidebar-collapsed");
            updateCollapseButton();
        }
    };

    collapseButton.addEventListener("click", () => {
        if (desktopQuery.matches) {
            setDesktopCollapsed(!body.classList.contains("sidebar-collapsed"));
        } else {
            closeMobileSidebar();
            mobileButton.focus();
        }
    });

    mobileButton.addEventListener("click", () => {
        if (body.classList.contains("sidebar-mobile-open")) closeMobileSidebar();
        else openMobileSidebar();
    });

    backdrop.addEventListener("click", () => {
        closeMobileSidebar();
        mobileButton.focus();
    });

    sidebar.querySelectorAll("a").forEach((link) => {
        link.addEventListener("click", () => {
            if (!desktopQuery.matches) closeMobileSidebar();
        });
    });

    sidebar.querySelectorAll(".sidebar-group-toggle").forEach((toggle) => {
        toggle.addEventListener("click", (event) => {
            if (!desktopQuery.matches || !body.classList.contains("sidebar-collapsed")) return;

            event.preventDefault();
            event.stopPropagation();
            setDesktopCollapsed(false);

            const targetSelector = toggle.getAttribute("data-bs-target");
            const target = targetSelector ? document.querySelector(targetSelector) : null;
            if (target && window.bootstrap) {
                requestAnimationFrame(() => {
                    bootstrap.Collapse.getOrCreateInstance(target, { toggle: false }).show();
                });
            }
        }, true);
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && body.classList.contains("sidebar-mobile-open")) {
            closeMobileSidebar();
            mobileButton.focus();
        }
    });

    if (typeof desktopQuery.addEventListener === "function") {
        desktopQuery.addEventListener("change", applyViewportMode);
    } else {
        desktopQuery.addListener(applyViewportMode);
    }

    applyViewportMode();
})();

(() => {
    "use strict";

    const digitsOnly = (value) => String(value ?? "").replace(/\D/g, "");
    const formatMoney = (value) => {
        const digits = digitsOnly(value).replace(/^0+(?=\d)/, "");
        return (digits || "0").replace(/\B(?=(\d{3})+(?!\d))/g, ".");
    };

    const syncMoneyInput = (input) => {
        const hidden = input.parentElement?.querySelector(".money-value");
        if (!hidden) return;

        const digits = digitsOnly(input.value).replace(/^0+(?=\d)/, "") || "0";
        input.value = formatMoney(digits);
        hidden.value = digits;
    };

    const bindMoneyInputs = (root = document) => {
        root.querySelectorAll(".money-input").forEach((input) => {
            const hidden = input.parentElement?.querySelector(".money-value");
            input.value = formatMoney(input.value || hidden?.value);
            syncMoneyInput(input);
        });
    };

    bindMoneyInputs();
    document.addEventListener("input", (event) => {
        if (event.target instanceof HTMLInputElement && event.target.matches(".money-input")) {
            syncMoneyInput(event.target);
        }
    }, true);

    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            mutation.addedNodes.forEach((node) => {
                if (!(node instanceof Element)) return;
                if (node.matches(".money-input")) syncMoneyInput(node);
                bindMoneyInputs(node);
            });
        });
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();

(() => {
    "use strict";

    document.addEventListener("submit", (event) => {
        const form = event.target;
        const submitter = event.submitter;
        const message = submitter?.dataset.confirm || form?.dataset.confirm;

        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    });
})();
